using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;

namespace DAZ_Installer.DP {
    internal class DPProgressCombo {
        internal static Stack<DPProgressCombo> ProgressCombos = new Stack<DPProgressCombo>(3);
        internal TableLayoutPanel Panel {get; private set; }
        internal Label ProgressBarLbl { get; private set; }
        internal ProgressBar ProgressBar { get; private set; }

        internal bool IsMarqueueProgressBar { get => ProgressBar.Style == ProgressBarStyle.Marquee; }

        internal DPProgressCombo() {
            extractControl.extractPage.Invoke(CreateProgressCombo);
            extractControl.extractPage.Invoke(extractControl.extractPage.AddNewProgressCombo, this);
            ProgressCombos.Push(this);
        }

        private void CreateProgressCombo() {
            
            // Panel
            Panel = new TableLayoutPanel();
            Panel.Dock = DockStyle.Fill;
            Panel.ColumnCount = 1;
            Panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Panel.RowCount = 2;
            Panel.RowStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Panel.RowStyles.Add(new ColumnStyle(SizeType.AutoSize));

            ProgressBarLbl = new Label();
            ProgressBarLbl.Text = "Processing ...";
            ProgressBarLbl.Dock = DockStyle.Fill;
            ProgressBarLbl.AutoEllipsis = true;
            ProgressBarLbl.TextAlign = ContentAlignment.BottomLeft;
            ProgressBarLbl.MinimumSize = new Size(0, 25);
            Panel.Controls.Add(ProgressBarLbl, 0, 0);

            ProgressBar = new ProgressBar();
            ProgressBar.Value = 50;
            ProgressBar.Dock = DockStyle.Fill;
            ProgressBar.MinimumSize = new Size(0, 18);
            ProgressBar.MarqueeAnimationSpeed /= 5;
            Panel.Controls.Add(ProgressBar, 0, 1);

            // ProgressBar.CheckForIllegalCrossThreadCalls = false;
        }


        internal void ChangeProgressBarStyle(bool marqueue) {
            if (IsMarqueueProgressBar == marqueue) return;
            if (extractControl.extractPage.InvokeRequired) {
                extractControl.extractPage.Invoke(ChangeProgressBarStyle, marqueue);
                return;
            }

            ProgressBar.SuspendLayout();
            if (marqueue) {
                ProgressBar.Value = 10;
                ProgressBar.Style = ProgressBarStyle.Marquee;
            } else {
                ProgressBar.Value = 50;
                ProgressBar.Style = ProgressBarStyle.Blocks;
            }
            ProgressBar.ResumeLayout();

        }

        internal static void RemoveAll() {
            extractControl.extractPage.Invoke(extractControl.extractPage.ResetExtractPage);
            ProgressCombos.Clear();
        }

        internal void Remove() {
            ProgressCombos.TryPop(out _);
            extractControl.extractPage.DeleteProgressionCombo(this);
        }

        internal void UpdateText(string text) {
            ProgressBarLbl.Text = 
            extractControl.extractPage.mainProcLbl.Text =
            text;
        }

        ~DPProgressCombo() {
            ProgressBar.Dispose();
            ProgressBarLbl.Dispose();
            Panel.Dispose();
        }
    }
}