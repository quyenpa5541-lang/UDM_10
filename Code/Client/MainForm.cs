
using System;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Shared;

namespace Client
{
    public partial class MainForm : Form
    {
        private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);
        private readonly AppSettings _settings;
        private bool _serverReady;
        private bool _isUploading;
        private CancellationTokenSource? _uploadCancellation;

        public MainForm()
        {
            InitializeComponent();

            // Dùng timeout/số luồng trong cấu hình Client, không hard-code địa chỉ Server.
            try
            {
                _settings = AppSettings.Load();
            }
            catch (Exception ex)
            {
                _settings = new AppSettings();
                MessageBox.Show(this,
                    "Cấu hình Client không hợp lệ, sử dụng giá trị mặc định:\n" + ex.Message,
                    "Lỗi cấu hình", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            txtServerIp.Text = _settings.Host;
            numServerPort.Value = _settings.Port;

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
            txtServerIp.TextChanged += (_, _) => InvalidatePing();
            numServerPort.ValueChanged += (_, _) => InvalidatePing();
            FormClosing += MainForm_FormClosing;
            UpdateUploadButton();
        }

        // KẾT NỐI SERVER: dùng cùng hàm PING trong FileUploader để thống nhất protocol.
        private async void btnConnect_Click(object? sender, EventArgs e)
        {
            if (_isUploading) return;

            btnConnect.Enabled = false;
            _serverReady = false;
            UpdateUploadButton();
            lblConnectionStatus.Text = "Đang kết nối...";
            lblConnectionStatus.ForeColor = Color.DarkOrange;

            try
            {
                string host = txtServerIp.Text.Trim();
                int port = (int)numServerPort.Value;
                if (string.IsNullOrWhiteSpace(host))
                    throw new ArgumentException("Bạn chưa nhập IP Server.");

                var (ok, message) = await FileUploader.PingAsync(
                    host, port, ConnectionTimeout, CancellationToken.None);

                if (IsDisposed || Disposing) return;

                if (!ok)
                {
                    ShowConnectionError(message);
                    return;
                }

                _serverReady = true;
                lblConnectionStatus.Text = "Server sẵn sàng (PONG)";
                lblConnectionStatus.ForeColor = Color.Green;
                MessageBox.Show(this, "Kết nối Server thành công!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing) ShowConnectionError(ex.Message);
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    btnConnect.Enabled = true;
                    UpdateUploadButton();
                }
            }
        }

        // HIỂN THỊ LỖI KẾT NỐI
        private void ShowConnectionError(string message)
        {
            _serverReady = false;
            lblConnectionStatus.Text = "Chưa kết nối";
            lblConnectionStatus.ForeColor = Color.Red;
            UpdateUploadButton();

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

            UpdateSummary();
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

        // Nút Upload đồng thời cho phép hủy các phiên upload đang chạy.
        private async void btnUpload_Click(object? sender, EventArgs e)
        {
            if (_isUploading)
            {
                _uploadCancellation?.Cancel();
                btnUpload.Enabled = false;
                btnUpload.Text = "Đang hủy...";
                return;
            }

            if (!_serverReady) return;

            // File thành công không gửi lại; file lỗi có thể thử upload lần nữa.
            var pending = dgvFiles.Rows.Cast<DataGridViewRow>()
                .Where(row => row.Tag is string &&
                    !string.Equals(Convert.ToString(row.Cells[colStatus.Index].Value),
                        "Thành công", StringComparison.Ordinal))
                .ToArray();
            if (pending.Length == 0) return;

            _isUploading = true;
            _uploadCancellation = new CancellationTokenSource();
            var cancellation = _uploadCancellation;
            var host = txtServerIp.Text.Trim();
            var port = (int)numServerPort.Value;
            var timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

            btnConnect.Enabled = false;
            btnSelectFiles.Enabled = false;
            txtServerIp.Enabled = false;
            numServerPort.Enabled = false;
            pnlDropZone.AllowDrop = false;
            lblDropHint.AllowDrop = false;
            UpdateUploadButton();

            // Mỗi file dùng một kết nối TCP riêng; số tác vụ đồng thời lấy từ appsettings.
            using var semaphore = new SemaphoreSlim(Math.Max(1, _settings.ParallelUploads));
            try
            {
                var jobs = pending.Select(async row =>
                {
                    await semaphore.WaitAsync(cancellation.Token);
                    try
                    {
                        await UploadOneFileAsync(row, host, port, timeout, cancellation.Token);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                await Task.WhenAll(jobs);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                if (!IsDisposed && !Disposing)
                {
                    foreach (var row in pending)
                    {
                        string status = Convert.ToString(row.Cells[colStatus.Index].Value) ?? "";
                        if (status != "Thành công" && status != "Lỗi" && status != "Đã hủy")
                            row.Cells[colStatus.Index].Value = "Đã hủy";
                    }
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                    MessageBox.Show(this, "Lỗi khi upload: " + ex.Message,
                        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                cancellation.Dispose();
                _uploadCancellation = null;
                _isUploading = false;
                if (!IsDisposed && !Disposing)
                {
                    btnConnect.Enabled = true;
                    btnSelectFiles.Enabled = true;
                    txtServerIp.Enabled = true;
                    numServerPort.Enabled = true;
                    pnlDropZone.AllowDrop = true;
                    lblDropHint.AllowDrop = true;
                    UpdateSummary();
                    UpdateUploadButton();
                }
            }
        }

        private async Task UploadOneFileAsync(
            DataGridViewRow row, string host, int port, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (row.Tag is not string path) return;

            row.Cells[colStatus.Index].Value = "Đang băm SHA-256";
            row.Cells[colProgress.Index].Value = "0%";
            row.Cells[colSpeed.Index].Value = "—";
            row.Cells[colError.Index].Value = "";

            // Progress<T> được tạo trên UI thread nên cập nhật DataGridView an toàn.
            // Bỏ qua callback đến muộn sau khi Server đã trả kết quả cuối.
            bool finished = false;
            var state = new Progress<UploadState>(next =>
            {
                if (finished || IsDisposed || Disposing || row.DataGridView == null) return;
                row.Cells[colStatus.Index].Value = next switch
                {
                    UploadState.Hashing => "Đang băm SHA-256",
                    UploadState.Connecting => "Đang kết nối",
                    UploadState.Uploading => "Đang gửi",
                    _ => "Đang xử lý"
                };
            });
            var hashProgress = new Progress<int>(percent =>
            {
                if (finished || IsDisposed || Disposing || row.DataGridView == null) return;
                // Hash và upload là hai giai đoạn riêng; không tính hash vào % đã gửi.
                if (Convert.ToString(row.Cells[colStatus.Index].Value)?.StartsWith("Đang băm") == true)
                    row.Cells[colStatus.Index].Value = $"Đang băm SHA-256 ({percent}%)";
            });
            var progress = new Progress<UploadProgress>(current =>
            {
                if (finished || IsDisposed || Disposing || row.DataGridView == null) return;
                double percent = current.TotalBytes <= 0 ? 0 :
                    Math.Clamp(current.SentBytes * 100.0 / current.TotalBytes, 0, 100);
                row.Cells[colProgress.Index].Value = $"{percent:0}%";
                if (current.BytesPerSecond > 0)
                    row.Cells[colSpeed.Index].Value =
                        $"{current.BytesPerSecond / (1024 * 1024):0.00} MB/s";
                if (current.SentBytes == current.TotalBytes && current.TotalBytes > 0)
                    row.Cells[colStatus.Index].Value = "Đang xác nhận";
            });

            UploadResult result = await FileUploader.UploadAsync(
                host, port, path, timeout, progress, state,
                cancellationToken, hashProgress);
            finished = true;

            if (IsDisposed || Disposing || row.DataGridView == null) return;

            if (result.Ok)
            {







                row.Cells[colStatus.Index].Value = "Thành công";
                row.Cells[colProgress.Index].Value = "100%";
                if (!string.IsNullOrWhiteSpace(result.SavedAs))
                    row.Cells[colStatus.Index].ToolTipText =
                        "Đã lưu trên Server: " + result.SavedAs;
            }
            else
            {
                row.Cells[colStatus.Index].Value = result.Canceled ? "Đã hủy" : "Lỗi";
                row.Cells[colError.Index].Value = result.Canceled ? result.Message :
                    $"Mã {result.Code}: {result.Message}";

                // Kết nối TCP đã lỗi: yêu cầu PING lại trước lần upload tiếp theo.
                if (result.Code == Protocol.ErrorUnavailable ||
                    result.Code == Protocol.ErrorTimeout ||
                    result.Code == Protocol.ErrorClientClosed)
                {
                    _serverReady = false;
                    lblConnectionStatus.Text = "Mất kết nối - hãy PING lại";
                    lblConnectionStatus.ForeColor = Color.Red;
                }
            }
            UpdateSummary();
        }

        private void InvalidatePing()
        {
            if (_isUploading) return;
            _serverReady = false;
            lblConnectionStatus.Text = "Chưa kết nối";
            lblConnectionStatus.ForeColor = Color.Red;
            UpdateUploadButton();
        }

        private void UpdateUploadButton()
        {
            if (_isUploading)
            {
                btnUpload.Text = _uploadCancellation?.IsCancellationRequested == true
                    ? "Đang hủy..." : "Hủy upload";
                btnUpload.Enabled = _uploadCancellation?.IsCancellationRequested != true;
                return;
            }

            btnUpload.Text = "Upload";
            btnUpload.Enabled = _serverReady && dgvFiles.Rows.Cast<DataGridViewRow>()
                .Any(row => row.Tag is string &&
                    !string.Equals(Convert.ToString(row.Cells[colStatus.Index].Value),
                        "Thành công", StringComparison.Ordinal));
        }

        private void UpdateSummary()
        {
            if (IsDisposed || Disposing) return;
            int total = dgvFiles.Rows.Count;
            int completed = dgvFiles.Rows.Cast<DataGridViewRow>().Count(row =>
                string.Equals(Convert.ToString(row.Cells[colStatus.Index].Value),
                    "Thành công", StringComparison.Ordinal));
            lblSummary.Text = $"{total} file | {completed} thành công";
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Hủy thao tác mạng khi đóng cửa sổ; FileUploader sẽ dispose socket/file.
            _uploadCancellation?.Cancel();
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

        private void btnUpload_Click_1(object sender, EventArgs e)
        {

        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }
    }
}
