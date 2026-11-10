using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

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

        public ServerForm()
        {
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
                Enabled = false
            };

            btnStop = new Button
            {
                Text = "Stop Server",
                Location = new Point(150, 85),
                Size = new Size(120, 35),
                Enabled = false
            };

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
            AddLog("Chờ tích hợp chức năng Start/Stop Server.");
            RefreshServerInfo();
        }

        private void RefreshServerInfo()
        {
            lblConnections.Text =
                $"Số kết nối: {ManageClients.CurrentConnection}";

            lstFiles.Items.Clear();

            string uploadFolder = Path.Combine(
                AppContext.BaseDirectory, "Uploads");

            if (Directory.Exists(uploadFolder))
            {
                foreach (string file in Directory.GetFiles(uploadFolder))
                {
                    lstFiles.Items.Add(Path.GetFileName(file));
                }
            }
        }

        public void AddLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AddLog(message)));
                return;
            }

            lstLogs.Items.Add(
                $"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            refreshTimer.Stop();
            refreshTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}

