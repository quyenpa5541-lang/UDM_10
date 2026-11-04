using System;
using System.Windows.Forms;

namespace Client
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            dgvFiles.AllowUserToAddRows = false;

            pnlDropZone.AllowDrop = true;
            pnlDropZone.DragEnter += UploadArea_DragEnter;
            pnlDropZone.DragDrop += UploadArea_DragDrop;

            lblDropHint.AllowDrop = true;
            lblDropHint.DragEnter += UploadArea_DragEnter;
            lblDropHint.DragDrop += UploadArea_DragDrop;
        }

        private void btnSelectFiles_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Chọn file để upload",
                Multiselect = true,
                Filter = "Tất cả file (*.*)|*.*"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                AddFiles(dialog.FileNames);
            }
        }

        private void AddFiles(string[] paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    if (System.IO.Directory.Exists(path))
                        continue;

                    if (!System.IO.File.Exists(path))
                    {
                        throw new System.IO.FileNotFoundException(
                            "File không còn tồn tại hoặc không truy cập được.");
                    }

                    string fullPath = System.IO.Path.GetFullPath(path);
                    bool alreadyAdded = false;

                    foreach (DataGridViewRow row in dgvFiles.Rows)
                    {
                        if (string.Equals(
                            row.Tag as string,
                            fullPath,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }

                    if (alreadyAdded)
                        continue;

                    var file = new System.IO.FileInfo(fullPath);

                    int rowIndex = dgvFiles.Rows.Add(
                        file.Name,
                        $"{file.Length:N0} byte",
                        "Chờ",
                        "0%",
                        "—",
                        ""
                    );

                    dgvFiles.Rows[rowIndex].Tag = fullPath;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Không thêm được file:\n{path}\n\n{ex.Message}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }

            lblSummary.Text = $"{dgvFiles.Rows.Count} file";
        }

        private void UploadArea_DragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void UploadArea_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
            {
                AddFiles(paths);
            }
        }

        private void txtServerIp_Click(object sender, EventArgs e)
        {
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
        }

        private void lblDropHint_Click(object sender, EventArgs e)
        {
        }

        private void lblConnectionStatus_Click(object sender, EventArgs e)
        {
        }

        private void pnlDropZone_Paint(object sender, PaintEventArgs e)
        {
        }

        private void dataGridView1_CellContentClick(
            object sender, DataGridViewCellEventArgs e)
        {
        }
    }
}