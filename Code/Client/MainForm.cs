
using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Shared;

namespace Client
{
    public partial class MainForm : Form
    {
        private static readonly TimeSpan ConnectionTimeout =
            TimeSpan.FromSeconds(5);

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

            // Gắn sự kiện kết nối
            btnConnect.Click -= btnConnect_Click;
            btnConnect.Click += btnConnect_Click;
        }

        // KẾT NỐI SERVER
        private async void btnConnect_Click(object? sender, EventArgs e)
        {
            btnConnect.Enabled = false;

            lblConnectionStatus.Text = "Đang kết nối...";
            lblConnectionStatus.ForeColor = Color.DarkOrange;

            try
            {
                string ip = txtServerIp.Text.Trim();
                int port = (int)numServerPort.Value;

                if (string.IsNullOrWhiteSpace(ip))
                {
                    throw new ArgumentException(
                        "Bạn chưa nhập IP Server.");
                }

                using var tcp = new TcpClient();
                using var cts =
                    new CancellationTokenSource(ConnectionTimeout);

                await tcp.ConnectAsync(ip, port, cts.Token);

                using NetworkStream stream = tcp.GetStream();

                // Gửi PING
                await Protocol.WriteJsonAsync(
                    stream,
                    new RequestHeader
                    {
                        Command = Protocol.CmdPing
                    },
                    ConnectionTimeout,
                    cts.Token
                );

                // Nhận PONG
                ResponseHeader? response =
                    await Protocol.ReadJsonAsync<ResponseHeader>(
                        stream,
                        ConnectionTimeout,
                        cts.Token
                    );

                if (response?.Code == 200 &&
                    string.Equals(
                        response.Message,
                        "PONG",
                        StringComparison.OrdinalIgnoreCase))
                {
                    lblConnectionStatus.Text = "Đã kết nối";
                    lblConnectionStatus.ForeColor = Color.Green;

                    MessageBox.Show(
                        this,
                        "Kết nối Server thành công!",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    ShowConnectionError(
                        "Server không phản hồi PONG hợp lệ.");
                }
            }
            catch (OperationCanceledException)
            {
                ShowConnectionError(
                    "Hết thời gian chờ kết nối (5 giây).");
            }
            catch (TimeoutException)
            {
                ShowConnectionError(
                    "Server không phản hồi trong 5 giây.");
            }
            catch (Exception ex)
            {
                ShowConnectionError(ex.Message);
            }
            finally
            {
                btnConnect.Enabled = true;
            }
        }

        // HIỂN THỊ LỖI KẾT NỐI
        private void ShowConnectionError(string message)
        {
            lblConnectionStatus.Text = "Chưa kết nối";
            lblConnectionStatus.ForeColor = Color.Red;

            MessageBox.Show(
                this,
                "Không thể kết nối Server:\n" + message,
                "Lỗi kết nối",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        // CHỌN FILE
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

        // THÊM FILE VÀO DANH SÁCH
        private void AddFiles(string[] paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    if (Directory.Exists(path))
                        continue;

                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException(
                            "File không còn tồn tại.");
                    }

                    string fullPath = Path.GetFullPath(path);
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

                    var file = new FileInfo(fullPath);

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
                        this,
                        $"Không thêm được file:\n{path}\n\n{ex.Message}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }

            lblSummary.Text = $"{dgvFiles.Rows.Count} file";
        }

        // KÉO FILE VÀO GIAO DIỆN
        private void UploadArea_DragEnter(
            object? sender,
            DragEventArgs e)
        {
            e.Effect =
                e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
        }

        // THẢ FILE
        private void UploadArea_DragDrop(
            object? sender,
            DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop)
                is string[] paths)
            {
                AddFiles(paths);
            }
        }

        // CÁC SỰ KIỆN GIAO DIỆN CŨ
        private void txtServerIp_Click(
            object sender, EventArgs e)
        {
        }

        private void numericUpDown1_ValueChanged(
            object sender, EventArgs e)
        {
        }

        private void lblDropHint_Click(
            object sender, EventArgs e)
        {
        }

        private void lblConnectionStatus_Click(
            object sender, EventArgs e)
        {
        }

        private void pnlDropZone_Paint(
            object sender, PaintEventArgs e)
        {
        }

        private void dataGridView1_CellContentClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
        }
    }
}
