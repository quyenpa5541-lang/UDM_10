namespace Client
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnSelectFiles = new Button();
            lblServerIp = new Label();
            txtServerIp = new TextBox();
            label1 = new Label();
            numServerPort = new NumericUpDown();
            btnConnect = new Button();
            lblConnectionStatus = new Label();
            pnlDropZone = new Panel();
            lblDropHint = new Label();
            dgvFiles = new DataGridView();
            colFileName = new DataGridViewTextBoxColumn();
            colSize = new DataGridViewTextBoxColumn();
            colStatus = new DataGridViewTextBoxColumn();
            colProgress = new DataGridViewTextBoxColumn();
            colSpeed = new DataGridViewTextBoxColumn();
            colError = new DataGridViewTextBoxColumn();
            btnUpload = new Button();
            lblSummary = new Label();
            ((System.ComponentModel.ISupportInitialize)numServerPort).BeginInit();
            pnlDropZone.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFiles).BeginInit();
            SuspendLayout();
            // 
            // btnSelectFiles
            // 
            btnSelectFiles.Location = new Point(17, 198);
            btnSelectFiles.Margin = new Padding(2, 2, 2, 2);
            btnSelectFiles.Name = "btnSelectFiles";
            btnSelectFiles.Size = new Size(90, 27);
            btnSelectFiles.TabIndex = 0;
            btnSelectFiles.Text = "Chọn file";
            btnSelectFiles.UseVisualStyleBackColor = true;
            btnSelectFiles.Click += btnSelectFiles_Click;
            // 
            // lblServerIp
            // 
            lblServerIp.AutoSize = true;
            lblServerIp.Location = new Point(17, 24);
            lblServerIp.Margin = new Padding(2, 0, 2, 0);
            lblServerIp.Name = "lblServerIp";
            lblServerIp.Size = new Size(69, 20);
            lblServerIp.TabIndex = 1;
            lblServerIp.Text = "IP Server:";
            lblServerIp.Click += txtServerIp_Click;
            // 
            // txtServerIp
            // 
            txtServerIp.Location = new Point(90, 22);
            txtServerIp.Margin = new Padding(2, 2, 2, 2);
            txtServerIp.Name = "txtServerIp";
            txtServerIp.Size = new Size(121, 27);
            txtServerIp.TabIndex = 2;
            txtServerIp.Text = "127.0.0.1";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(222, 24);
            label1.Margin = new Padding(2, 0, 2, 0);
            label1.Name = "label1";
            label1.Size = new Size(38, 20);
            label1.TabIndex = 3;
            label1.Text = "Port:";
            // 
            // numServerPort
            // 
            numServerPort.Location = new Point(265, 22);
            numServerPort.Margin = new Padding(2, 2, 2, 2);
            numServerPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numServerPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numServerPort.Name = "numServerPort";
            numServerPort.Size = new Size(144, 27);
            numServerPort.TabIndex = 4;
            numServerPort.Value = new decimal(new int[] { 9000, 0, 0, 0 });
            numServerPort.ValueChanged += numericUpDown1_ValueChanged;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(426, 22);
            btnConnect.Margin = new Padding(2, 2, 2, 2);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(90, 27);
            btnConnect.TabIndex = 5;
            btnConnect.Text = "Kết nối";
            btnConnect.UseVisualStyleBackColor = true;
            // 
            // lblConnectionStatus
            // 
            lblConnectionStatus.AutoSize = true;
            lblConnectionStatus.Location = new Point(17, 57);
            lblConnectionStatus.Margin = new Padding(2, 0, 2, 0);
            lblConnectionStatus.Name = "lblConnectionStatus";
            lblConnectionStatus.Size = new Size(92, 20);
            lblConnectionStatus.TabIndex = 6;
            lblConnectionStatus.Text = "Chưa kết nối";
            lblConnectionStatus.Click += lblConnectionStatus_Click;
            // 
            // pnlDropZone
            // 
            pnlDropZone.AllowDrop = true;
            pnlDropZone.BorderStyle = BorderStyle.FixedSingle;
            pnlDropZone.Controls.Add(lblDropHint);
            pnlDropZone.Location = new Point(17, 94);
            pnlDropZone.Margin = new Padding(2, 2, 2, 2);
            pnlDropZone.Name = "pnlDropZone";
            pnlDropZone.Size = new Size(985, 85);
            pnlDropZone.TabIndex = 7;
            pnlDropZone.Paint += pnlDropZone_Paint;
            // 
            // lblDropHint
            // 
            lblDropHint.AutoSize = true;
            lblDropHint.Location = new Point(394, 29);
            lblDropHint.Margin = new Padding(2, 0, 2, 0);
            lblDropHint.Name = "lblDropHint";
            lblDropHint.Size = new Size(141, 20);
            lblDropHint.TabIndex = 0;
            lblDropHint.Text = "Kéo thả file vào đây";
            lblDropHint.Click += lblDropHint_Click;
            // 
            // dgvFiles
            // 
            dgvFiles.AllowUserToAddRows = false;
            dgvFiles.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvFiles.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvFiles.Columns.AddRange(new DataGridViewColumn[] { colFileName, colSize, colStatus, colProgress, colSpeed, colError });
            dgvFiles.Location = new Point(11, 229);
            dgvFiles.Margin = new Padding(2, 2, 2, 2);
            dgvFiles.Name = "dgvFiles";
            dgvFiles.ReadOnly = true;
            dgvFiles.RowHeadersWidth = 62;
            dgvFiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvFiles.Size = new Size(985, 122);
            dgvFiles.TabIndex = 8;
            // 
            // colFileName
            // 
            colFileName.HeaderText = "\tTên file";
            colFileName.MinimumWidth = 8;
            colFileName.Name = "colFileName";
            colFileName.ReadOnly = true;
            // 
            // colSize
            // 
            colSize.HeaderText = "Dung lượng";
            colSize.MinimumWidth = 8;
            colSize.Name = "colSize";
            colSize.ReadOnly = true;
            // 
            // colStatus
            // 
            colStatus.HeaderText = "\tTrạng thái";
            colStatus.MinimumWidth = 8;
            colStatus.Name = "colStatus";
            colStatus.ReadOnly = true;
            // 
            // colProgress
            // 
            colProgress.HeaderText = "Tiến trình";
            colProgress.MinimumWidth = 8;
            colProgress.Name = "colProgress";
            colProgress.ReadOnly = true;
            // 
            // colSpeed
            // 
            colSpeed.HeaderText = "Tốc độ";
            colSpeed.MinimumWidth = 8;
            colSpeed.Name = "colSpeed";
            colSpeed.ReadOnly = true;
            // 
            // colError
            // 
            colError.HeaderText = "Chi tiết lỗi";
            colError.MinimumWidth = 8;
            colError.Name = "colError";
            colError.ReadOnly = true;
            // 
            // btnUpload
            // 
            btnUpload.Enabled = false;
            btnUpload.Location = new Point(887, 379);
            btnUpload.Margin = new Padding(2, 2, 2, 2);
            btnUpload.Name = "btnUpload";
            btnUpload.Size = new Size(90, 27);
            btnUpload.TabIndex = 9;
            btnUpload.Text = "Upload";
            btnUpload.UseVisualStyleBackColor = true;
            btnUpload.Click += btnUpload_Click_1;
            // 
            // lblSummary
            // 
            lblSummary.AutoSize = true;
            lblSummary.Location = new Point(28, 379);
            lblSummary.Margin = new Padding(2, 0, 2, 0);
            lblSummary.Name = "lblSummary";
            lblSummary.Size = new Size(42, 20);
            lblSummary.TabIndex = 10;
            lblSummary.Text = "0 file";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            ClientSize = new Size(1011, 470);
            Controls.Add(lblSummary);
            Controls.Add(btnUpload);
            Controls.Add(dgvFiles);
            Controls.Add(pnlDropZone);
            Controls.Add(lblConnectionStatus);
            Controls.Add(btnConnect);
            Controls.Add(numServerPort);
            Controls.Add(label1);
            Controls.Add(txtServerIp);
            Controls.Add(lblServerIp);
            Controls.Add(btnSelectFiles);
            Margin = new Padding(2, 2, 2, 2);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "UDM_10 - Upload nhiều file";
            ((System.ComponentModel.ISupportInitialize)numServerPort).EndInit();
            pnlDropZone.ResumeLayout(false);
            pnlDropZone.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvFiles).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnSelectFiles;
        private Label lblServerIp;
        private TextBox txtServerIp;
        private Label label1;
        private NumericUpDown numServerPort;
        private Button btnConnect;
        private Label lblConnectionStatus;
        private Panel pnlDropZone;
        private Label lblDropHint;
        private DataGridView dgvFiles;
        private DataGridViewTextBoxColumn colFileName;
        private DataGridViewTextBoxColumn colSize;
        private DataGridViewTextBoxColumn colStatus;
        private DataGridViewTextBoxColumn colProgress;
        private DataGridViewTextBoxColumn colSpeed;
        private DataGridViewTextBoxColumn colError;
        private Button btnUpload;
        private Label lblSummary;
    }
}