using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace bpmGenie;

public partial class frmMain : Form
{
    private CancellationTokenSource? _cancellationTokenSource;

    public frmMain()
    {
        InitializeComponent();
        
        // Ensure TagLib saves ID3v2.3 by default per user request
        TagLib.Id3v2.Tag.DefaultVersion = 3;
        TagLib.Id3v2.Tag.ForceDefaultVersion = false;
    }

    private async void btnSelectFolder_Click(object sender, EventArgs e)
    {
        if (_cancellationTokenSource != null)
        {
            // If already running, cancel it
            _cancellationTokenSource.Cancel();
            btnSelectFolder.Text = "Select Folder";
            btnSelectFolder.BackColor = Color.FromArgb(0, 120, 215);
            return;
        }

        using var fbd = new FolderBrowserDialog
        {
            Description = "Select a folder containing MP3 files to scan",
            UseDescriptionForTitle = true
        };

        if (fbd.ShowDialog() == DialogResult.OK)
        {
            await StartScanningAsync(fbd.SelectedPath);
        }
    }

    private async Task StartScanningAsync(string folderPath)
    {
        lblTitle.Text = folderPath;
        gridFiles.Rows.Clear();
        lblStatus.Text = "Finding MP3 files...";
        progressBar.Value = 0;
        btnApply.Enabled = false;
        
        var splash = new frmSplash();
        splash.Show(this);
        splash.Refresh(); // Force immediate paint to avoid white-box freezing
        
        // Push heavy file IO and TagLib reading to background thread
        var fileDataList = await Task.Run(() => 
        {
            var mp3Files = Directory.GetFiles(folderPath, "*.mp3", SearchOption.AllDirectories).ToList();
            var results = new List<(string file, string artist, string title)>();
            
            foreach (var file in mp3Files)
            {
                string artist = "";
                string title = "";
                try 
                {
                    using var tFile = TagLib.File.Create(file);
                    if (tFile.Tag != null)
                    {
                        artist = tFile.Tag.FirstPerformer ?? "";
                        title = tFile.Tag.Title ?? "";
                    }
                }
                catch { /* Ignore unreadable tags */ }
                
                results.Add((file, artist, title));
            }
            return results;
        });

        splash.Close();

        if (fileDataList.Count == 0)
        {
            lblStatus.Text = "No MP3 files found in the directory.";
            return;
        }

        progressBar.Maximum = fileDataList.Count;
        btnSelectFolder.Text = "Stop Scanning";
        btnSelectFolder.BackColor = Color.FromArgb(200, 50, 50);

        gridFiles.SuspendLayout();
        foreach (var data in fileDataList)
        {
            int rowIndex = gridFiles.Rows.Add(Path.GetFileName(data.file), data.title, data.artist, "Pending...", "", data.file);
            gridFiles.Rows[rowIndex].Tag = rowIndex;
        }
        gridFiles.ResumeLayout();

        lblStatus.Text = $"Scanning {fileDataList.Count} files...";

        _cancellationTokenSource = new CancellationTokenSource();
        int completedCount = 0;

        try
        {
            // Process files sequentially (safer for UI and simpler ffmpeg concurrency)
            for (int i = 0; i < fileDataList.Count; i++)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                var file = fileDataList[i].file;
                var row = gridFiles.Rows[i];

                row.Cells[3].Value = "Scanning..."; // Status col is now index 3
                gridFiles.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index - 5);

                try
                {
                    // Call the BpmAnalyzer (timeout/cancellation supported)
                    int predictedBpm = await BpmAnalyzer.DetectBpmAsync(file, _cancellationTokenSource.Token, 0, 60);

                    if (predictedBpm > 0)
                    {
                        row.Cells[3].Value = "Completed";
                        row.Cells[4].Value = predictedBpm.ToString(); // BPM col is now index 4
                    }
                    else
                    {
                        row.Cells[3].Value = "Failed - No beat detected";
                    }
                }
                catch (OperationCanceledException)
                {
                    row.Cells[3].Value = "Canceled";
                    throw;
                }
                catch (Exception ex)
                {
                    row.Cells[3].Value = "Error";
                    row.Cells[4].ToolTipText = ex.Message;
                }

                completedCount++;
                progressBar.Value = completedCount;
                lblStatus.Text = $"Scanned {completedCount} of {fileDataList.Count} files...";
            }

            lblStatus.Text = "Scan Complete.";
            btnApply.Enabled = true;
        }
        catch (OperationCanceledException)
        {
            lblStatus.Text = "Scan Canceled by user.";
            btnApply.Enabled = true; // allow saving whatever was completed
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            btnSelectFolder.Text = "Select Folder";
            btnSelectFolder.BackColor = Color.FromArgb(0, 120, 215);
        }
    }

    private void btnApply_Click(object sender, EventArgs e)
    {
        int savedCount = 0;
        int errorCount = 0;

        foreach (DataGridViewRow row in gridFiles.Rows)
        {
            string status = row.Cells[3].Value?.ToString() ?? "";
            string bpmStr = row.Cells[4].Value?.ToString() ?? "";
            string filePath = row.Cells[5].Value?.ToString() ?? "";

            // Only attempt to save if we have a successful BPM computation
            if (status == "Completed" && uint.TryParse(bpmStr, out uint bpmVal) && !string.IsNullOrEmpty(filePath))
            {
                try
                {
                    using var file = TagLib.File.Create(filePath);
                    
                    // Creates ID3v2.3 if not found, or updates v2.3/2.4 if present
                    file.Tag.BeatsPerMinute = bpmVal;
                    
                    file.Save();
                    row.Cells[3].Value = "Saved!";
                    savedCount++;
                }
                catch (Exception ex)
                {
                    row.Cells[3].Value = "Save Error";
                    row.Cells[4].ToolTipText = ex.Message;
                    errorCount++;
                }
            }
        }

        lblStatus.Text = $"Apply operation complete. Saved: {savedCount}, Errors: {errorCount}";
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        this.Close();
    }
}
