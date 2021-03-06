// This code is licensed under the Keep It Free License V1.
// You may find a full copy of this license at root project directory\LICENSE
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using IOPath = System.IO.Path;
using System.IO;
using System;


namespace DAZ_Installer.DP {

    internal enum ArchiveType
    {
        Product, Bundle, Unknown
    }

    internal enum ArchiveFormat {
        SevenZ, WinZip, RAR, Unknown
    }
    
    internal abstract class DPAbstractArchive : DPAbstractFile {

        protected enum Mode {
            Peek, Extract
        }
        /// <summary>
        /// The name that will be used for the hierachy. Equivalent to Path.GetFileName(Path);
        /// </summary>
        /// <value>The file name of the <c>Path</c> of this archive with the extension.</value>
        internal string HierachyName { get; set; }
        /// <summary>
        /// The file name of this archive. Equivalent to Path.GetFileName(Path);
        /// </summary>
        /// <value>The file name of the <c>Path</c> of this archive with the extension.</value>
        internal string FileName { get; set; }
        /// <summary>
        /// The name that will be used for the list view. The list name is the working archive's <c>FileName</c> + $"\\{Path}".
        /// </summary>
        /// <value>The working archive's FileName + $"\\{Path}".</value>
        internal string ListName { get; set; }
        /// <summary>
        /// A global static dictionary of available archives
        /// </summary>
        /// <typeparam name="string">The name of the archive.</typeparam>
        /// <typeparam name="DPAbstractArchive">The archive.</typeparam>
        internal static Dictionary<string, DPAbstractArchive> Archives { get; } = new Dictionary<string, DPAbstractArchive>(); 
        /// <summary>
        /// A list of archives that are children of this archive.
        /// </summary>
        internal List<DPAbstractArchive> InternalArchives { get; init; } = new List<DPAbstractArchive>();
        /// <summary>
        /// The archive that is the parent of this archive. This can be null.
        /// </summary>
        internal DPAbstractArchive? ParentArchive { get; set; }
        /// <summary>
        /// A file that has been detected as a manifest file. This can be null.
        /// </summary>
        internal DPDSXFile? ManifestFile { get; set; }
        /// <summary>
        /// A file that has been detected as a supplement file. This can be null.
        /// </summary>
        internal DPDSXFile? SupplementFile { get; set; }
        /// <summary>
        /// A boolean value to describe if this archive is a child of another archive. Default is false.
        /// </summary>
        internal bool IsInnerArchive { get; set; } = false;
        /// <summary>
        /// The type of this archive. Default is <c>ArchiveType.Unknown</c>.
        /// </summary>
        internal ArchiveType Type { get; set; } = ArchiveType.Unknown;
        /// <summary>
        /// A list of files that errored during the extraction stage.
        /// </summary>
        internal LinkedList<string> ErroredFiles { get; set; } = new LinkedList<string>();
        /// <summary>
        /// The product info connected to this archive.
        /// </summary>
        internal DPProductInfo ProductInfo = new DPProductInfo();

        /// <summary>
        /// A map of all of the folders parented to this archive.
        /// </summary>
        /// <typeparam name="string">The `Path` of the Folder.</typeparam>
        /// <typeparam name="DPFolder">The folder.</typeparam>

        public Dictionary<string, DPFolder> Folders { get; } = new Dictionary<string, DPFolder>();

        /// <summary>
        /// A list of folders at the root level of this archive.
        /// </summary>
        public List<DPFolder> RootFolders { get; } = new List<DPFolder>();
        /// <summary>
        /// A list of all of the contents (DPAbstractFiles) in this archive.
        /// </summary>
        /// <typeparam name="DPAbstractFile">The file content in this archive.</typeparam>
        public List<DPAbstractFile> Contents { get; } = new List<DPAbstractFile>();
        
        /// <summary>
        /// A list of the root contents/ the contents at root level (DPAbstractFiles) of this archive.
        /// </summary>
        /// <typeparam name="DPAbstractFile">The file content in this archive.</typeparam>
        public List<DPAbstractFile> RootContents { get; } = new List<DPAbstractFile>();
        /// <summary>
        /// A list of all .dsx files in this archive.
        /// </summary>
        /// <typeparam name="DPDSXFile">A file that is either a manifest, supplementary, or support file (.dsx).</typeparam>
        internal List<DPDSXFile> DSXFiles { get; } = new List<DPDSXFile>();
        /// <summary>
        /// A list of all readable daz files in this archive. This consists of types with extension: .duf, .dsf.
        /// </summary>
        /// <typeparam name="DPDazFile">A file with the extension .duf OR .dsf.</typeparam>
        /// <returns></returns>
        internal List<DPDazFile> DazFiles { get; } = new List<DPDazFile>();

        /// <summary>
        /// A boolean to determine if the processor can read the contents of the archive without extracting to disk.
        /// </summary>
        internal virtual bool CanReadWithoutExtracting { get; init; } = false;
        /// <summary>
        /// The true uncompressed size of the archive contents in bytes.
        /// </summary>
        internal ulong TrueArchiveSize { get; set; } = 0;
        /// <summary>
        /// The expected tag count for this archive. This value is updated when an applicable file has discovered new tags.
        /// </summary>
        internal uint ExpectedTagCount { get; set; } = 0;

        /// <summary>
        /// The progress combo that is visible on the extraction page. This is typically null when the file is firsted discovered
        /// inside another archive (and therefore before it is processed) and/or after the extraction has completed.
        /// </summary>
        internal DPProgressCombo? ProgressCombo { get; set; }

        protected Mode mode { get; set; } = Mode.Extract;

        internal static Regex ProductNameRegex = new Regex(@"([^+|-|_|\s]+)", RegexOptions.Compiled);
        internal DPAbstractArchive(string _path, bool innerArchive = false, string? relativePathBase = null) : base(_path)
        {
            IsInnerArchive = innerArchive; // Order matters.
            // Make a file but we don't want to check anything.
            if (IsInnerArchive) Parent = null;
            else _parent = null;
            FileName = IOPath.GetFileName(_path);
            if (relativePathBase != null)
            {
                RelativePath = IOPath.GetRelativePath(relativePathBase, Path);
            }
            else RelativePath = FileName;
            if (DPProcessor.workingArchive != this && DPProcessor.workingArchive != null)
            {
                ListName = DPProcessor.workingArchive.FileName + '\\' + Path;
            }
            Ext = GetExtension(Path);
            HierachyName = IOPath.GetFileName(Path);
            ProductInfo = new DPProductInfo(IOPath.GetFileNameWithoutExtension(Path));

            if (IsInnerArchive)
                DPProcessor.workingArchive.Contents.Add(this);

            Archives.Add(Path, this);
        }

        ~DPAbstractArchive()
        {
            Archives.Remove(Path ??= string.Empty);
        }
        #region Abstract methods
        /// <summary>
        /// Peeks the archive contents if possible and will extract the archive contents to the destination path. 
        /// </summary>
        internal abstract void Extract();

        /// <summary>
        /// Previews the archive by discovering files in this archive.
        /// </summary>
        internal abstract void Peek();
        
        /// <summary>
        /// Reads the files listed in <c>DSXFiles</c>. If <c>CanReadWithoutExtracting</c> is true, the file won't be extracted.
        /// Otherwise, the file will be extracted to the <c>TEMP_LOCATION</c> of <c>DPProcessor</c>. 
        /// </summary>
        internal abstract void ReadMetaFiles();
        
        /// <summary>
        /// Reads files that have the extension .dsf and .duf after it has been extracted. 
        /// </summary>
        internal abstract void ReadContentFiles();
        /// <summary>
        /// Calls the derived archive class to dispose of the file handle.
        /// </summary>
        internal abstract void ReleaseArchiveHandles();
        #endregion
        #region Internal Methods
        /// <summary>
        ///  Checks whether or not the given ext is what is expected. Checks file headers.
        /// </summary>
        /// <returns>Returns an extension of the appropriate archive extraction method. Otherwise, null.</returns>

        internal static ArchiveFormat CheckArchiveLegitmacy(DPAbstractArchive archive) {
            FileStream stream;
            // Open file.
            if (archive.IsInnerArchive) stream = File.OpenRead(archive.ExtractedPath);
            else stream = File.OpenRead(archive.Path);
            
            var bytes = new byte[8];
            stream.Read(bytes, 0, 8);
            stream.Close();
            // ZIP File Header
            // 	50 4B OR 	57 69
            if ((bytes[0] == 80 || bytes[0] == 87) && (bytes[1] == 75 || bytes[2] == 105))
            {
                return ArchiveFormat.WinZip;
            }
            // RAR 5 consists of 8 bytes.  0x52 0x61 0x72 0x21 0x1A 0x07 0x01 0x00
            // RAR 4.x consists of 7. 0x52 0x61 0x72 0x21 0x1A 0x07 0x00
            // Rar!
            if (bytes[0] == 82 && bytes[1] == 97 && bytes[2] == 114 && bytes[3] == 33)
            {
                return ArchiveFormat.RAR;
            }

            if (bytes[0] == 55 && bytes[1] == 122 && bytes[2] == 188 && bytes[3] == 175)
            {
                return ArchiveFormat.SevenZ;
            }
            return ArchiveFormat.Unknown;
        }

        /// <summary>
        /// Returns an enum describing the archive's format based on the file extension.
        /// </summary>
        /// <param name="path">The path of the archive.</param>
        /// <returns>A ArchiveFormat enum determining the archive format.</returns>
        internal static ArchiveFormat DetermineArchiveFormat(string ext) {
            ext = ext.ToLower();
            switch (ext) {
                case "7z":
                    return ArchiveFormat.SevenZ;
                case "rar":
                    return ArchiveFormat.RAR;
                case "zip":
                    return ArchiveFormat.WinZip;
                default:
                    return ArchiveFormat.Unknown;
            }
        }

        internal static DPAbstractArchive CreateNewArchive(string fileName, bool innerArchive = false, string? relativePathBase = null) {
            string ext = GetExtension(fileName);
            switch (DetermineArchiveFormat(ext)) {
                case ArchiveFormat.RAR:
                    return new DPRARArchive(fileName, innerArchive, relativePathBase);
                case ArchiveFormat.SevenZ:
                    return new DP7zArchive(fileName, innerArchive, relativePathBase);
                case ArchiveFormat.WinZip:
                    return new DPZipArchive(fileName, innerArchive, relativePathBase);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Finds files that were supposedly extracted to disk.
        /// </summary>
        /// <returns>The file paths of successful extracted files.</returns>
        private string[] GetSuccessfulFiles()
        {
            List<string> foundFiles = new List<string>(Contents.Count);
            foreach (var file in Contents) {
                if (file.WasExtracted) foundFiles.Add(file.Path);
            }
            return foundFiles.ToArray();
        }

        internal static bool FindArchiveViaName(string path, out DPAbstractArchive archive)
        {
            if (Archives.TryGetValue(path, out archive)) return true;

            archive = null;
            return false;
        }

        internal DPProductRecord CreateRecords()
        {
            string imageLocation = string.Empty;
            var workingExtractionRecord = 
                new DPExtractionRecord(System.IO.Path.GetFileName(FileName), DPSettings.destinationPath, GetSuccessfulFiles(), ErroredFiles.ToArray(), 
                null, ConvertDPFoldersToStringArr(Folders), 0);

            if (Type != ArchiveType.Bundle)
            {
                if (DPSettings.downloadImages == SettingOptions.Yes)
                {
                    imageLocation = DPNetwork.DownloadImage(workingExtractionRecord.ArchiveFileName);
                }
                else if (DPSettings.downloadImages == SettingOptions.Prompt)
                {
                    // TODO: Use more reliable method! Support files!
                    // Pre-check if the archive file name starts with "IM"
                    if (workingExtractionRecord.ArchiveFileName.StartsWith("IM"))
                    {
                        var result = DAZ_Installer.Extract.ExtractPage.DoPromptMessage("Do you wish to download the thumbnail for this product?", "Download Thumbnail Prompt", MessageBoxButtons.YesNo);
                        if (result == DialogResult.Yes) imageLocation = DPNetwork.DownloadImage(workingExtractionRecord.ArchiveFileName);
                    }
                }
                var author = ProductInfo.Authors.Count != 0 ? ProductInfo.Authors.First() : null;
                var workingProductRecord = new DPProductRecord(ProductInfo.ProductName, ProductInfo.Tags.ToArray(), author, 
                                            null, DateTime.Now, imageLocation, 0, 0);
                DPDatabase.AddNewRecordEntry(workingProductRecord, workingExtractionRecord);
                return workingProductRecord;
            }
            return null;
        }

        internal DPAbstractFile? FindFileViaNameContains(string name)
        {
            foreach (var file in Contents)
            {
                if (file.Path.Contains(name)) return file;
            }
            return null;
        }

        private static string[] ConvertDPFoldersToStringArr(Dictionary<string, DPFolder> folders)
        {
            string[] strFolders = new string[folders.Count];
            string[] keys = folders.Keys.ToArray();
            for (var i = 0; i < strFolders.Length; i++)
            {
                strFolders[i] = folders[keys[i]].Path;
            }
            return strFolders;
        }

        /// <summary>
        /// This function should be called after all the files have been extracted. If no content folders have been found, this is a bundle.
        /// </summary>
        internal ArchiveType DetermineArchiveType()
        {
            foreach (var folder in Folders.Values)
            {
                if (folder.isContentFolder)
                {
                    return ArchiveType.Product;
                }
            }
            foreach (var content in Contents)
            {
                if (content is DPAbstractArchive) return ArchiveType.Bundle;
            }
            return ArchiveType.Unknown;
        }

        internal void GetTags()
        {
            // First is always author.
            // Next is folder names.
            var productNameTokens = SplitProductName();
            ReadContentFiles();
            ReadMetaFiles();
            var tagsSet = new HashSet<string>(GetEstimateTagCount() + productNameTokens.Length);

            tagsSet.UnionWith(ProductInfo.Authors);
            if (ProductInfo.SKU.Length != 0) tagsSet.Add(ProductInfo.SKU);

            ProductInfo.Tags = tagsSet;
        
        }

        internal int GetEstimateTagCount() {
            int count = 0;
            foreach (var content in Contents) {
                if (content is DPFile) {
                    count += ((DPFile) content).Tags.Count;
                }
            }
            count += ProductInfo.Authors.Count;
            return count;
        }

        internal DPFolder FindParent(DPAbstractFile obj)
        {
            var fileName = PathHelper.GetFileName(obj.Path);
            if (fileName == string.Empty) fileName = IOPath.GetFileName(obj.Path.TrimEnd(PathHelper.GetSeperator(obj.Path)));
            string relativePathOnly = "";
            try
            {
                relativePathOnly = PathHelper.GetAbsoluteUpPath(obj.Path.Remove(obj.Path.LastIndexOf(fileName)));
            }
            catch { }
            if (RecursivelyFindFolder(relativePathOnly, out DPFolder folder))
            {
                return folder;
            }
            return null;
        }

        internal bool FolderExists(string fPath) => Folders.ContainsKey(fPath);

        internal bool RecursivelyFindFolder(string relativePath, out DPFolder folder)
        {

            foreach (var _folder in Folders.Values)
            {
                if (_folder.Path == relativePath || _folder.Path == PathHelper.SwitchSeperators(relativePath))
                {
                    folder = _folder;
                    return true;
                }
            }
            folder = null;
            return false;
        }


        internal string[] SplitProductName() {
            var matches = ProductNameRegex.Matches(ProductInfo.ProductName);
            List<string> tokens = new List<string>(matches.Count);
            foreach (Match match in matches) {
                tokens.Add(match.Value);
            }
            return tokens.ToArray();
        }
        #endregion
        
    }
}