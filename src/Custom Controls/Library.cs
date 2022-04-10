﻿// This code is licensed under the Keep It Free License V1.
// You may find a full copy of this license at root project directory\LICENSE
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
namespace DAZ_Installer
{
    /// <summary>
    /// The Library class is responsible for the loading, adding & removing LibraryItems. It is also responsible for controlling the LibraryPanel and effectively managing image resources. It also controls search interactions. 
    /// </summary>
    public partial class Library : UserControl
    {
        public static Library self;
        protected static Image arrowRight;
        protected static Image arrowDown;
        protected static Image noImageFound;
        protected static Size lastClientSize;
        protected const byte maxImagesLoad = byte.MaxValue;
        protected const byte maxListSize = 25;
        protected byte maxImageFit;
        protected LibraryItem[] libraryItems { get => libraryPanel1.LibraryItems;  }
        protected Dictionary<uint, List<LibraryItem>> pages = new Dictionary<uint, List<LibraryItem>>();
        
        protected bool mainImagesLoaded = false;
        // Quick Library Info 
        public Library()
        {
            InitializeComponent();
            self = this;
            libraryPanel1.LibraryItems = new LibraryItem[25];
            libraryPanel1.CurrentPage = 1;

            Initalize();
            // Am listening...
            libraryPanel1.AddPageChangeListener(UpdatePage);
        }

        // Tasks could be done on main thread or (usually) on another thread.
        private void Initalize()
        {
            var t1 = Task.Run(LoadLibraryItemImages);
            t1.ContinueWith(t => LoadLibraryItems());
            Task.Run(DP.DPDatabase.Initalize);
        }

        // Called only when visible. Can be loaded but but visible.
        private void Library_Load(object sender, EventArgs e)
        {

        }

        // Called on a different thread.
        public void LoadLibraryItemImages()
        {
            arrows.Images.Add(Properties.Resources.ArrowRight);
            arrows.Images.Add(Properties.Resources.ArrowDown);
            thumbnails.Images.Add(Properties.Resources.NoImageFound);

            arrowRight = arrows.Images[0];
            arrowDown = arrows.Images[1];
            noImageFound = thumbnails.Images[0];
            mainImagesLoaded = true;
            DPCommon.WriteToLog("Loaded images.");

        }

        private void LoadLibraryItems()
        {
            // Read library items file.
            // ReadLibraryItemsFile.

            // Wait for main image loading to be true.
            // TODO: Remove while loop.
            // Shouldn't sleep anymore since its chained with ContinueWith.
            while (!mainImagesLoaded || libraryItems == null)
            {
                Thread.Sleep(50);
                DPCommon.WriteToLog("Sleeping.");
            }

            InitalizeDictionary();
            GenerateLibraryItemsFromDisk();

            Invoke(ForcePageUpdate);
            DPCommon.WriteToLog("Loaded library items.");

        }

        // We will refer to library item as records.
        internal void GenerateLibraryItemsFromDisk()
        {
            //if (!LibraryIO.initalized) 
                LibraryIO.Initalize();
            var startIndex = 25 * (libraryPanel1.CurrentPage - 1);
            var recordCount = LibraryIO.ProductRecords.Count;
            for (var i = startIndex; i < startIndex + 25 && i < recordCount; i++)
            {
                var record = LibraryIO.ProductRecords[i];
                var lb = AddNewLibraryItem(record);
                var imageLocation = record.expectedImageLocation;
                if (File.Exists(imageLocation))
                {
                    lb.Image = AddReferenceImage(imageLocation);
                    lb.ProductRecord = record;
                } else
                {
                    lb.Image = noImageFound;
                    lb.ProductRecord = record;
                }
            }
        }

        /// <summary>
        ///  Clears the current page library items and handles removing image references.
        /// </summary>
        private void ClearPageContents()
        {
            libraryPanel1.EditMode = true;
            foreach (var lb in libraryPanel1.LibraryItems)
            {
                if (lb == null || lb.ProductRecord == null) continue;

                lb.Image = null;
                RemoveReferenceImage(Path.GetFileName(lb.ProductRecord.expectedImageLocation));
                lb.Dispose();
            }
            ArrayHelper.ClearArray(libraryPanel1.LibraryItems);
            libraryPanel1.EditMode = false;
            // Get the current page contents.
        }
        
        private void InitalizeDictionary()
        {
            pages.Clear();
            for (int i = 0, p = 1; i < libraryItems.Length; i++)
            {
                if (i % 4 == 0 && i != 0) p++;
                if (pages.ContainsKey((uint)p)) pages[(uint)p].Add(libraryItems[i]);
                else
                {
                    pages[(uint)p] = new List<LibraryItem>(4);
                    pages[(uint)p].Add(libraryItems[i]);
                }
                
            }
        }

        internal LibraryItem AddNewLibraryItem(DPProductRecord record)
        {
            if (InvokeRequired)
            {
                return (LibraryItem)Invoke(new Func<DPProductRecord, LibraryItem>(AddNewLibraryItem), record);
            }
            var lb = new LibraryItem();
            lb.TitleText = record.productName;
            lb.Tags = record.tags;
            lb.Folders = record.directories;
            lb.Dock = DockStyle.Top;
            lb.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            lb.ArrowRightImage = arrowRight;
            lb.ArrowDownImage = arrowDown;
            lb.Image = noImageFound;

            var openSlot = ArrayHelper.GetNextOpenSlot(libraryItems);
            if (openSlot != -1) libraryItems[openSlot] = lb;
            
            return lb;
        }

        public LibraryItem AddNewLibraryItem(string title, string[] tags, string[] folders)
        {
            if (InvokeRequired)
            {
                return (LibraryItem)Invoke(new Func<string, string[], string[], LibraryItem>(AddNewLibraryItem), title, tags, folders);
            }
            var lb = new LibraryItem();
            lb.TitleText = title;
            lb.Tags = tags;
            lb.Folders = folders;
            lb.Dock = DockStyle.Top;
            lb.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            lb.ArrowRightImage = arrowRight;
            lb.ArrowDownImage = arrowDown;
            lb.Image = noImageFound;

            var openSlot = ArrayHelper.GetNextOpenSlot(libraryItems);
            if (openSlot != -1) libraryItems[openSlot] = lb;
            
            return lb;
        }

        public Image AddReferenceImage(string filePath)
        {
            if (filePath == null) return noImageFound;
            // Key = FileName
            var fileName = Path.GetFileName(filePath);
            if (thumbnails.Images.ContainsKey(fileName))
            {
                var i = thumbnails.Images.IndexOfKey(fileName);
                return thumbnails.Images[i];
            } else
            {
                // 125, 119
                var icon = Image.FromFile(filePath);
                thumbnails.Images.Add(icon);
                // Get the last index.
                var i = thumbnails.Images.Count - 1;
                thumbnails.Images.SetKeyName(i, fileName);
                return thumbnails.Images[i];
            }
        }

        public void RemoveReferenceImage(string imageName)
        {
            if (thumbnails.Images.ContainsKey(imageName))
            {
                thumbnails.Images.RemoveByKey(imageName);
                thumbnails.Images.Keys.Remove(imageName);
            }
        }

        // Used whenever a change has been made
        // 
        
        // Try page update
        internal void TryPageUpdate()
        {
            if (!LibraryIO.initalized) LibraryIO.Initalize();
            try
            {
                ClearPageContents();
                AddLibraryItems();
                // TO DO : Check if we need to move to the left page.
                // Example - There are no library items on current page (invalid page) and no pages above it.
                UpdatePageCount();
                libraryPanel1.UpdateMainContent();
            } catch { }
        }
        public void ForcePageUpdate()
        {
            DPCommon.WriteToLog("force page update called.");
            if (InvokeRequired) {Invoke(ForcePageUpdate); return; }
            ClearPageContents();
            AddLibraryItems();
            // TO DO : Check if we need to move to the left page.
            // Example - There are no library items on current page (invalid page) and no pages above it.
            UpdatePageCount();
            libraryPanel1.UpdateMainContent();
        }

        // Used for handling page events.
        public void UpdatePage(int page) {
            DPCommon.WriteToLog("page update called.");
            if (page == libraryPanel1.PreviousPage) return;

            ClearPageContents();
            AddLibraryItems();
            libraryPanel1.UpdateMainContent();
        }

        private void UpdatePageCount()
        {
            decimal pageCalculation = LibraryIO.ProductRecords.Count / 25m;
            int pageCount = (int) Math.Ceiling(pageCalculation);

            if (pageCount != libraryPanel1.PageCount) libraryPanel1.PageCount = pageCount;
        }

        private void AddLibraryItems()
        {
            DPCommon.WriteToLog("Add library items.");
            var startRecordIndex = (libraryPanel1.CurrentPage - 1) * 25; // Current Page never 0.
            byte count = 0;
            libraryPanel1.EditMode = true;
            // Loop while i is less than records count and count is less than 25.
            for (var i = startRecordIndex; i < LibraryIO.ProductRecords.Count && count < 25; i++, count++)
            {
                var record = LibraryIO.ProductRecords[i];
                var lb = AddNewLibraryItem(record);
                libraryItems[count] = lb;

                // Check if image exists.
                if (File.Exists(record.expectedImageLocation))
                {
                    var image = AddReferenceImage(record.expectedImageLocation);
                    lb.Image = image;
                } else lb.Image = noImageFound;
            }
            libraryPanel1.EditMode = false;
        }
        
        // Possible race condition: ForcePageUpdate() from initailization.
        // TODO: Fix ^

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {

        }
    }
}
