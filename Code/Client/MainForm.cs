
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

        private bool uploading;

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
            btnUpload.Click += btnUpload_Click;
        }

        private async void btnUpload_Click(object? sender, EventArgs e)
        {
            if (uploading)
                return;

            string host = txtServerIp.Text.Trim();
            int port = (int)numServerPort.Value;
            if (host.Length == 0 || port < 1 || port > 65535)
            {
                MessageBox.Show(this, "Nhập địa chỉ Server và cổng hợp lệ.");
                return;
            }

            uploading = true;
            btnUpload.Enabled = false;
            btnSelectFiles.Enabled = false;
            pnlDropZone.AllowDrop = false;
            lblDropHint.AllowDrop = false;

            using var cancellation = new CancellationTokenSource();
            FormClosedEventHandler cancelOnClose = (_, _) => cancellation.Cancel();
            FormClosed += cancelOnClose;
            try
            {
                foreach (DataGridViewRow row in dgvFiles.Rows)
                {
                    if (row.Tag is not string path ||
                        Equals(row.Cells[colStatus.Index].Value, "Hoàn tất"))
                    {
                        continue;
                    }

                    await UploadRowAsync(row, path, host, port, cancellation.Token);
                    if (cancellation.IsCancellationRequested || IsDisposed)
                        return;
                }
            }
            finally
            {
                FormClosed -= cancelOnClose;
                uploading = false;
                if (!IsDisposed)
                {
                    btnSelectFiles.Enabled = true;
                    pnlDropZone.AllowDrop = true;
                    lblDropHint.AllowDrop = true;
                    UpdateUploadButton();
                }
            }
        }

        private async Task UploadRowAsync(
            DataGridViewRow row, string path, string host, int port, CancellationToken ct)
        {
            var statusCell = row.Cells[colStatus.Index];
            var progressCell = row.Cells[colProgress.Index];
            var speedCell = row.Cells[colSpeed.Index];
            var errorCell = row.Cells[colError.Index];

            statusCell.Value = "Đang tính SHA-256";
            progressCell.Value = "0%";
            speedCell.Value = "—";
            errorCell.Value = "";

            bool finished = false;
            var state = new Progress<UploadState>(value =>
            {
                if (finished || IsDisposed || ct.IsCancellationRequested)
                    return;

                statusCell.Value = value switch
                {
                    UploadState.Hashing => "Đang tính SHA-256",
                    UploadState.Connecting => "Đang kết nối",
                    UploadState.Uploading => "Đang gửi",
                    _ => statusCell.Value
                };
            });

            var progress = new Progress<UploadProgress>(update =>
            {
                if (finished || IsDisposed || ct.IsCancellationRequested)
                    return;

                int percent = update.TotalBytes == 0
                    ? 100
                    : (int)(100.0 * update.SentBytes / update.TotalBytes);
                progressCell.Value = $"{percent}%";
                if (update.SentBytes >= update.TotalBytes)
                    statusCell.Value = "Chờ Server xác nhận";

                if (update.BytesPerSecond > 0)
                    speedCell.Value = $"{update.BytesPerSecond / 1_000_000:N2} MB/s";
            });

            var result = await FileUploader.UploadAsync(
                host, port, path, TimeSpan.FromSeconds(30), progress, state, ct);
            finished = true;
            if (IsDisposed || ct.IsCancellationRequested)
                return;

            if (result.Ok)
            {
                statusCell.Value = "Hoàn tất";
                progressCell.Value = "100%";
            }
            else
            {
                statusCell.Value = result.Canceled ? "Đã hủy" : "Lỗi";
                errorCell.Value = result.Message;
            }
        }

        private void UpdateUploadButton()
        {
            btnUpload.Enabled = false;
            if (uploading)
                return;

            foreach (DataGridViewRow row in dgvFiles.Rows)
            {
                if (row.Tag is string &&
                    !Equals(row.Cells[colStatus.Index].Value, "Hoàn tất"))
                {
                    btnUpload.Enabled = true;
                    return;
                }
            }
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
            UpdateUploadButton();
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
