using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Shared;

namespace Server
{
    public class ServerForm : Form
    {
        private readonly Label lblStatus;
        private readonly Label lblConnections;
        private readonly ListBox lstFiles;
        private readonly ListBox lstLogs;
        private readonly Button btnStart;
        private readonly Button btnStop;
        private readonly System.Windows.Forms.Timer refreshTimer;

        private readonly AppSettings settings;
        private readonly FileLogger logger;
        private ManageClients? server;
        private string? lastLogFile;
        private int lastLogLineCount;
        private bool isClosing;

        public ServerForm()
        {
            settings = AppSettings.Load();

            // Resolve relative upload paths from the application's output folder,
            // so the storage service and the form display the same directory.
            if (!Path.IsPathRooted(settings.UploadDir))
            {
                settings.UploadDir = Path.Combine(
                    AppContext.BaseDirectory, settings.UploadDir);
            }

            logger = new FileLogger();

            // Keep the original Server form's title, size, positions and control sizes.
            Text = "Server Management";
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;

            lblStatus = new Label
            {
                Text = "Trạng thái: Chưa chạy",
                Location = new Point(20, 20),
                AutoSize = true
            };

            lblConnections = new Label
            {
                Text = "Số kết nối: 0",
                Location = new Point(20, 50),
                AutoSize = true
            };

            btnStart = new Button
            {
                Text = "Start Server",
                Location = new Point(20, 85),
                Size = new Size(120, 35),
                Enabled = true
            };
            btnStart.Click += async (sender, e) => await StartServerAsync();

            btnStop = new Button
            {
                Text = "Stop Server",
                Location = new Point(150, 85),
                Size = new Size(120, 35),
                Enabled = false
            };
            btnStop.Click += async (sender, e) => await StopServerAsync();

            Label lblFiles = new Label
            {
                Text = "Danh sách file đã nhận",
                Location = new Point(20, 140),
                AutoSize = true
            };

            lstFiles = new ListBox
            {
                Location = new Point(20, 170),
                Size = new Size(350, 330)
            };

            Label lblLogs = new Label
            {
                Text = "Nhật ký hoạt động",
                Location = new Point(400, 140),
                AutoSize = true
            };

            lstLogs = new ListBox
            {
                Location = new Point(400, 170),
                Size = new Size(350, 330)
            };

            Controls.AddRange(new Control[]
            {
                lblStatus,
                lblConnections,
                btnStart,
                btnStop,
                lblFiles,
                lstFiles,
                lblLogs,
                lstLogs
            });

            refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 2000
            };

            refreshTimer.Tick += (sender, e) => RefreshServerInfo();
            refreshTimer.Start();

            AddLog("Giao diện Server đã khởi tạo.");
            AddLog("Server sẵn sàng. Nhấn Start Server để bắt đầu lắng nghe.");
            RefreshServerInfo();

            FormClosing += ServerForm_FormClosing;
        }

        private async System.Threading.Tasks.Task StartServerAsync()
        {
            if (server?.IsRunning == true)
                return;

            btnStart.Enabled = false;

            try
            {
                server = new ManageClients(settings, logger);
                await server.StartAsync();

                lblStatus.Text =
                    $"Trạng thái: Đang chạy tại {settings.Host}:{settings.Port}";
                lblStatus.ForeColor = Color.Green;
                btnStop.Enabled = true;

                AddLog(
                    $"Server đã khởi động tại {settings.Host}:{settings.Port}");
            }
            catch (Exception ex)
            {
                if (server != null)
                {
                    try
                    {
                        await server.DisposeAsync();
                    }
                    catch
                    {
                        // Preserve the original startup error for the user.
                    }
                }

                server = null;
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                lblStatus.Text = "Trạng thái: Không thể khởi động";
                lblStatus.ForeColor = Color.Red;

                AddLog("Không thể khởi động Server: " + ex.Message);
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Lỗi khởi động Server",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task StopServerAsync()
        {
            if (server == null)
                return;

            btnStop.Enabled = false;

            try
            {
                await server.StopAsync();
                await server.DisposeAsync();

                AddLog("Server đã dừng.");
            }
            catch (Exception ex)
            {
                AddLog("Lỗi khi dừng Server: " + ex.Message);
            }
            finally
            {
                server = null;
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                lblStatus.Text = "Trạng thái: Đã dừng";
                lblStatus.ForeColor = Color.DarkOrange;
                RefreshServerInfo();
            }
        }

        private void RefreshServerInfo()
        {
            if (IsDisposed)
                return;

            lblConnections.Text =
                $"Số kết nối: {server?.ActiveConnections ?? 0}";

            string uploadFolder = Path.GetFullPath(settings.UploadDir);

            if (Directory.Exists(uploadFolder))
            {
                var files = Directory.GetFiles(uploadFolder)
                    .Where(path =>
                        !path.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (lstFiles.Items.Count != files.Length ||
                    !lstFiles.Items.Cast<string>().SequenceEqual(files))
                {
                    lstFiles.BeginUpdate();
                    lstFiles.Items.Clear();
                    lstFiles.Items.AddRange(files);
                    lstFiles.EndUpdate();
                }
            }

            RefreshLogView();
        }

        private void RefreshLogView()
        {
            string logDirectory = Path.Combine(
                AppContext.BaseDirectory, "logs");
            string currentLogFile = Path.Combine(
                logDirectory, $"server-{DateTime.Now:yyyyMMdd}.log");

            if (!File.Exists(currentLogFile))
                return;

            try
            {
                if (!string.Equals(
                    lastLogFile,
                    currentLogFile,
                    StringComparison.OrdinalIgnoreCase))
                {
                    lastLogFile = currentLogFile;
                    lastLogLineCount = 0;
                }

                string[] lines = File.ReadAllLines(currentLogFile);

                if (lastLogLineCount > lines.Length)
                    lastLogLineCount = 0;

                if (lastLogLineCount < lines.Length)
                {
                    lstLogs.BeginUpdate();

                    for (int i = lastLogLineCount; i < lines.Length; i++)
                        lstLogs.Items.Add(lines[i]);

                    lastLogLineCount = lines.Length;

                    while (lstLogs.Items.Count > 500)
                        lstLogs.Items.RemoveAt(0);

                    if (lstLogs.Items.Count > 0)
                        lstLogs.TopIndex = lstLogs.Items.Count - 1;

                    lstLogs.EndUpdate();
                }
            }
            catch (IOException)
            {
                // The log may be written concurrently; retry on the next refresh.
            }
            catch (UnauthorizedAccessException ex)
            {
                AddLog("Không thể đọc nhật ký Server: " + ex.Message);
            }
        }

        public void AddLog(string message)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AddLog(message)));
                return;
            }

            lstLogs.Items.Add(
                $"[{DateTime.Now:HH:mm:ss}] {message}");

            if (lstLogs.Items.Count > 500)
                lstLogs.Items.RemoveAt(0);

            if (lstLogs.Items.Count > 0)
                lstLogs.TopIndex = lstLogs.Items.Count - 1;
        }

        private async void ServerForm_FormClosing(
            object? sender, FormClosingEventArgs e)
        {
            if (isClosing)
                return;

            if (server != null)
            {
                e.Cancel = true;
                await StopServerAsync();
                isClosing = true;
                Close();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            refreshTimer.Stop();
            refreshTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
