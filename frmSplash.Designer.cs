namespace bpmGenie;

partial class frmSplash
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
        this.lblLoading = new System.Windows.Forms.Label();
        this.SuspendLayout();
        // 
        // lblLoading
        // 
        this.lblLoading.Dock = System.Windows.Forms.DockStyle.Fill;
        this.lblLoading.Font = new System.Drawing.Font("Segoe UI Semibold", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblLoading.ForeColor = System.Drawing.Color.White;
        this.lblLoading.Location = new System.Drawing.Point(0, 0);
        this.lblLoading.Name = "lblLoading";
        this.lblLoading.Size = new System.Drawing.Size(300, 100);
        this.lblLoading.TabIndex = 0;
        this.lblLoading.Text = "Scanning Directory...\r\nPlease Wait";
        this.lblLoading.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
        // 
        // frmSplash
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(50)))));
        this.ClientSize = new System.Drawing.Size(300, 100);
        this.Controls.Add(this.lblLoading);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        this.Name = "frmSplash";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Loading";
        this.ResumeLayout(false);

    }

    private System.Windows.Forms.Label lblLoading;
}
