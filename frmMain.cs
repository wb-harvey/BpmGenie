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
    private CancellationTokenSource? _applyCancellation;

    // Set when the window is closed during a scan or Apply; it closes once the current file is done
    private bool _closeWhenIdle;

    public frmMain()
    {
        InitializeComponent();
        FormClosing += frmMain_FormClosing;

        // Set TagLib to save ID3v2.3 by default
        TagLib.Id3v2.Tag.DefaultVersion = 3;
        TagLib.Id3v2.Tag.ForceDefaultVersion = false;
    }

    private void frmMain_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // Let a tag save finish rather than end the process in the middle of writing a file,
        // and let a scan stop its ffmpeg process
        if (_applyCancellation != null || _cancellationTokenSource != null)
        {
            e.Cancel = true;
            _closeWhenIdle = true;
            _applyCancellation?.Cancel();
            _cancellationTokenSource?.Cancel();
            lblStatus.Text = "Finishing the current file before closing...";
        }
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

        // Not clickable while the folder is read; it only becomes Stop Scanning once the scan starts
        btnSelectFolder.Enabled = false;

        var splash = new frmSplash();
        splash.Show(this);
        splash.Refresh(); // Force immediate paint to avoid white-box freezing

        List<(string file, string artist, string title)> fileDataList;
        try
        {
            // Push heavy file IO and TagLib reading to background thread
            fileDataList = await Task.Run(() => FindMp3Files(folderPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            lblStatus.Text = "Could not read the folder: " + ex.Message;
            return;
        }
        finally
        {
            splash.Close();
            btnSelectFolder.Enabled = true;
        }

        if (fileDataList.Count == 0)
        {
            lblStatus.Text = "No MP3 files found in the directory.";
            return;
        }

        progressBar.Maximum = fileDataList.Count;
        btnSelectFolder.Text = "Stop Scanning";
        btnSelectFolder.BackColor = Color.FromArgb(200, 50, 50);

        // Keep each file's row object: sorting the grid reorders rows, so an index would
        // point at another file's row by the time its result arrives
        var rows = new List<DataGridViewRow>(fileDataList.Count);
        gridFiles.SuspendLayout();
        foreach (var data in fileDataList)
        {
            int rowIndex = gridFiles.Rows.Add(Path.GetFileName(data.file), data.title, data.artist, "Pending...", "", data.file);
            rows.Add(gridFiles.Rows[rowIndex]);
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
                var row = rows[i];

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

            if (_closeWhenIdle) Close();
        }
    }

    /// <summary>
    /// Every MP3 under <paramref name="folderPath"/> with its artist and title.
    /// Folders that can't be opened, and system folders such as the Recycle Bin
    /// and System Volume Information, are skipped.
    /// </summary>
    private static List<(string file, string artist, string title)> FindMp3Files(string folderPath)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };
        var mp3Files = Directory.EnumerateFiles(folderPath, "*.mp3", options).ToList();
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
    }

    private async void btnApply_Click(object sender, EventArgs e)
    {
        if (_applyCancellation != null)
        {
            // Saving: stop after the current file
            _applyCancellation.Cancel();
            return;
        }

        // Only attempt to save if we have a successful BPM computation
        var pending = new List<(DataGridViewRow row, uint bpm, string filePath)>();
        foreach (DataGridViewRow row in gridFiles.Rows)
        {
            string status = row.Cells[3].Value?.ToString() ?? "";
            string bpmStr = row.Cells[4].Value?.ToString() ?? "";
            string filePath = row.Cells[5].Value?.ToString() ?? "";

            if (status == "Completed" && uint.TryParse(bpmStr, out uint bpmVal) && !string.IsNullOrEmpty(filePath))
                pending.Add((row, bpmVal, filePath));
        }

        int savedCount = 0;
        int errorCount = 0;

        _applyCancellation = new CancellationTokenSource();
        var token = _applyCancellation.Token;
        btnApply.Text = "Stop Saving";
        btnSelectFolder.Enabled = false;
        progressBar.Value = 0;
        progressBar.Maximum = Math.Max(1, pending.Count);

        try
        {
            foreach (var (row, bpmVal, filePath) in pending)
            {
                if (token.IsCancellationRequested) break;

                row.Cells[3].Value = "Saving...";

                // Save off the UI thread so the window stays responsive
                string? error = await Task.Run(() => SaveBpm(filePath, bpmVal));
                if (error == null)
                {
                    row.Cells[3].Value = "Saved!";
                    savedCount++;
                }
                else
                {
                    row.Cells[3].Value = "Save Error";
                    row.Cells[4].ToolTipText = error;
                    errorCount++;
                }

                progressBar.Value = savedCount + errorCount;
                lblStatus.Text = $"Saving {savedCount + errorCount} of {pending.Count} files...";
            }
        }
        finally
        {
            bool stopped = token.IsCancellationRequested;
            _applyCancellation.Dispose();
            _applyCancellation = null;
            btnApply.Text = "Apply";
            btnSelectFolder.Enabled = true;
            lblStatus.Text = $"Apply operation {(stopped ? "stopped" : "complete")}. Saved: {savedCount}, Errors: {errorCount}";

            if (_closeWhenIdle) Close();
        }
    }

    /// <summary>
    /// Writes <paramref name="bpm"/> to the file's tag. Returns null on success,
    /// or the error message.
    /// </summary>
    private static string? SaveBpm(string filePath, uint bpm)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);

            // Creates ID3v2.3 if not found, or updates v2.3/2.4 if present
            file.Tag.BeatsPerMinute = bpm;

            file.Save();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        this.Close();
    }
}
