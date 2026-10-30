using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Client
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void txtServerIp_Click(object sender, EventArgs e)
        {

        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void btnSelectFiles_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Chọn file để upload",
                Multiselect = true,
                Filter = "Tất cả file (*.*)|*.*"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            foreach (string path in dialog.FileNames)
            {
                try
                {
                    var file = new System.IO.FileInfo(path);

                    int rowIndex = dgvFiles.Rows.Add(
                        file.Name,
                        $"{file.Length:N0} byte",
                        "Chờ",
                        "0%",
                        "—",
                        ""
                    );

                    dgvFiles.Rows[rowIndex].Tag = file.FullName;
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

        private void lblDropHint_Click(object sender, EventArgs e)
        {

        }

        private void lblConnectionStatus_Click(object sender, EventArgs e)
        {

        }

        private void pnlDropZone_Paint(object sender, PaintEventArgs e)
        {

        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}
