namespace bpmGenie;

partial class frmMain
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
        System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
        System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
        System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
        this.pnlTop = new System.Windows.Forms.Panel();
        this.lblTitle = new System.Windows.Forms.Label();
        this.btnSelectFolder = new System.Windows.Forms.Button();
        this.pnlBottom = new System.Windows.Forms.Panel();
        this.lblStatus = new System.Windows.Forms.Label();
        this.progressBar = new System.Windows.Forms.ProgressBar();
        this.btnApply = new System.Windows.Forms.Button();
        this.btnClose = new System.Windows.Forms.Button();
        this.gridFiles = new System.Windows.Forms.DataGridView();
        this.colFileName = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colTitle = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colArtist = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colBpm = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.colPath = new System.Windows.Forms.DataGridViewTextBoxColumn();
        this.pnlTop.SuspendLayout();
        this.pnlBottom.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.gridFiles)).BeginInit();
        this.SuspendLayout();
        // 
        // pnlTop
        // 
        this.pnlTop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(35)))));
        this.pnlTop.Controls.Add(this.lblTitle);
        this.pnlTop.Controls.Add(this.btnSelectFolder);
        this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlTop.Location = new System.Drawing.Point(0, 0);
        this.pnlTop.Name = "pnlTop";
        this.pnlTop.Size = new System.Drawing.Size(900, 70);
        this.pnlTop.TabIndex = 0;
        // 
        // lblTitle
        // 
        this.lblTitle.AutoSize = true;
        this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
        this.lblTitle.Location = new System.Drawing.Point(20, 20);
        this.lblTitle.Name = "lblTitle";
        this.lblTitle.Size = new System.Drawing.Size(117, 30);
        this.lblTitle.TabIndex = 1;
        this.lblTitle.Text = "BpmGenie";
        // 
        // btnSelectFolder
        // 
        this.btnSelectFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnSelectFolder.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
        this.btnSelectFolder.FlatAppearance.BorderSize = 0;
        this.btnSelectFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnSelectFolder.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnSelectFolder.ForeColor = System.Drawing.Color.White;
        this.btnSelectFolder.Location = new System.Drawing.Point(745, 18);
        this.btnSelectFolder.Name = "btnSelectFolder";
        this.btnSelectFolder.Size = new System.Drawing.Size(130, 35);
        this.btnSelectFolder.TabIndex = 0;
        this.btnSelectFolder.Text = "Select Folder";
        this.btnSelectFolder.UseVisualStyleBackColor = false;
        this.btnSelectFolder.Click += new System.EventHandler(this.btnSelectFolder_Click);
        // 
        // pnlBottom
        // 
        this.pnlBottom.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(40)))));
        this.pnlBottom.Controls.Add(this.lblStatus);
        this.pnlBottom.Controls.Add(this.progressBar);
        this.pnlBottom.Controls.Add(this.btnApply);
        this.pnlBottom.Controls.Add(this.btnClose);
        this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
        this.pnlBottom.Location = new System.Drawing.Point(0, 530);
        this.pnlBottom.Name = "pnlBottom";
        this.pnlBottom.Size = new System.Drawing.Size(900, 70);
        this.pnlBottom.TabIndex = 1;
        // 
        // lblStatus
        // 
        this.lblStatus.AutoSize = true;
        this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
        this.lblStatus.Location = new System.Drawing.Point(20, 27);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(42, 15);
        this.lblStatus.TabIndex = 1;
        this.lblStatus.Text = "Ready.";
        // 
        // progressBar
        // 
        this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
        | System.Windows.Forms.AnchorStyles.Right)));
        this.progressBar.Location = new System.Drawing.Point(220, 30);
        this.progressBar.Name = "progressBar";
        this.progressBar.Size = new System.Drawing.Size(380, 10);
        this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
        this.progressBar.TabIndex = 0;
        // 
        // btnApply
        // 
        this.btnApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnApply.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(150)))), ((int)(((byte)(136)))));
        this.btnApply.FlatAppearance.BorderSize = 0;
        this.btnApply.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnApply.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnApply.ForeColor = System.Drawing.Color.White;
        this.btnApply.Location = new System.Drawing.Point(630, 18);
        this.btnApply.Name = "btnApply";
        this.btnApply.Size = new System.Drawing.Size(120, 35);
        this.btnApply.TabIndex = 2;
        this.btnApply.Text = "Apply";
        this.btnApply.UseVisualStyleBackColor = false;
        this.btnApply.Enabled = false;
        this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
        // 
        // btnClose
        // 
        this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
        this.btnClose.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
        this.btnClose.FlatAppearance.BorderSize = 0;
        this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnClose.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnClose.ForeColor = System.Drawing.Color.White;
        this.btnClose.Location = new System.Drawing.Point(760, 18);
        this.btnClose.Name = "btnClose";
        this.btnClose.Size = new System.Drawing.Size(115, 35);
        this.btnClose.TabIndex = 3;
        this.btnClose.Text = "Close";
        this.btnClose.UseVisualStyleBackColor = false;
        this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
        // 
        // gridFiles
        // 
        this.gridFiles.AllowUserToAddRows = false;
        this.gridFiles.AllowUserToDeleteRows = false;
        this.gridFiles.AllowUserToResizeRows = false;
        this.gridFiles.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(45)))));
        this.gridFiles.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this.gridFiles.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
        this.gridFiles.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
        dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(35)))));
        dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        dataGridViewCellStyle1.ForeColor = System.Drawing.Color.DarkGray;
        dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(35)))));
        dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
        dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
        this.gridFiles.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
        this.gridFiles.ColumnHeadersHeight = 35;
        this.gridFiles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        this.gridFiles.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
        this.colFileName,
        this.colTitle,
        this.colArtist,
        this.colStatus,
        this.colBpm,
        this.colPath});
        dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(45)))));
        dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        dataGridViewCellStyle2.ForeColor = System.Drawing.Color.White;
        dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(65)))));
        dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.White;
        dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
        this.gridFiles.DefaultCellStyle = dataGridViewCellStyle2;
        this.gridFiles.Dock = System.Windows.Forms.DockStyle.Fill;
        this.gridFiles.EnableHeadersVisualStyles = false;
        this.gridFiles.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(55)))));
        this.gridFiles.Location = new System.Drawing.Point(0, 70);
        this.gridFiles.Name = "gridFiles";
        this.gridFiles.ReadOnly = true;
        this.gridFiles.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
        dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(45)))));
        dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
        dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
        dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
        dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
        this.gridFiles.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
        this.gridFiles.RowHeadersVisible = false;
        this.gridFiles.RowTemplate.Height = 35;
        this.gridFiles.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        this.gridFiles.Size = new System.Drawing.Size(900, 460);
        this.gridFiles.TabIndex = 2;
        // 
        // colFileName
        // 
        this.colFileName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
        this.colFileName.FillWeight = 40F;
        this.colFileName.HeaderText = "File Name";
        this.colFileName.Name = "colFileName";
        this.colFileName.ReadOnly = true;
        // 
        // colTitle
        // 
        this.colTitle.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
        this.colTitle.FillWeight = 30F;
        this.colTitle.HeaderText = "Title";
        this.colTitle.Name = "colTitle";
        this.colTitle.ReadOnly = true;
        // 
        // colArtist
        // 
        this.colArtist.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
        this.colArtist.FillWeight = 30F;
        this.colArtist.HeaderText = "Artist";
        this.colArtist.Name = "colArtist";
        this.colArtist.ReadOnly = true;
        // 
        // colStatus
        // 
        this.colStatus.HeaderText = "Status";
        this.colStatus.Name = "colStatus";
        this.colStatus.ReadOnly = true;
        this.colStatus.Width = 140;
        // 
        // colBpm
        // 
        dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
        dataGridViewCellStyle4.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        dataGridViewCellStyle4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(190)))), ((int)(((byte)(255)))));
        this.colBpm.DefaultCellStyle = dataGridViewCellStyle4;
        this.colBpm.HeaderText = "BPM";
        this.colBpm.Name = "colBpm";
        this.colBpm.ReadOnly = true;
        this.colBpm.Width = 80;
        // 
        // colPath
        // 
        this.colPath.HeaderText = "Path";
        this.colPath.Name = "colPath";
        this.colPath.ReadOnly = true;
        this.colPath.Visible = false;
        // 
        // frmMain
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(45)))));
        this.ClientSize = new System.Drawing.Size(900, 600);
        this.Controls.Add(this.gridFiles);
        this.Controls.Add(this.pnlBottom);
        this.Controls.Add(this.pnlTop);
        this.Name = "frmMain";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "BPM Genie - MP3 BPM Scanner";
        this.pnlTop.ResumeLayout(false);
        this.pnlTop.PerformLayout();
        this.pnlBottom.ResumeLayout(false);
        this.pnlBottom.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.gridFiles)).EndInit();
        this.ResumeLayout(false);

    }

    private System.Windows.Forms.Panel pnlTop;
    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.Button btnSelectFolder;
    private System.Windows.Forms.Panel pnlBottom;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.Button btnApply;
    private System.Windows.Forms.Button btnClose;
    private System.Windows.Forms.DataGridView gridFiles;
    private System.Windows.Forms.DataGridViewTextBoxColumn colFileName;
    private System.Windows.Forms.DataGridViewTextBoxColumn colTitle;
    private System.Windows.Forms.DataGridViewTextBoxColumn colArtist;
    private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
    private System.Windows.Forms.DataGridViewTextBoxColumn colBpm;
    private System.Windows.Forms.DataGridViewTextBoxColumn colPath;
}
