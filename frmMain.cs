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

        // New ID3v2 tags are 2.3; an existing 2.3 or 2.4 tag keeps its version when saved
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

        List<(string file, string artist, string title, uint tagBpm)> fileDataList;
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
            int rowIndex = gridFiles.Rows.Add(Path.GetFileName(data.file), data.title, data.artist, "Pending...", "",
                data.tagBpm > 0 ? data.tagBpm.ToString() : "", data.file);
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

                row.Cells[colStatus.Index].Value = "Scanning...";

                // Scrolling throws when the grid is too short to show any row
                if (gridFiles.DisplayedRowCount(true) > 0)
                    gridFiles.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index - 5);

                try
                {
                    // Call the BpmAnalyzer (timeout/cancellation supported)
                    int predictedBpm = await BpmAnalyzer.DetectBpmAsync(file, _cancellationTokenSource.Token, 0, 60);

                    if (predictedBpm > 0)
                    {
                        row.Cells[colStatus.Index].Value = "Completed";
                        row.Cells[colBpm.Index].Value = predictedBpm.ToString();
                    }
                    else
                    {
                        row.Cells[colStatus.Index].Value = "Failed - No beat detected";
                    }
                }
                catch (OperationCanceledException)
                {
                    row.Cells[colStatus.Index].Value = "Canceled";
                    throw;
                }
                catch (Exception ex)
                {
                    row.Cells[colStatus.Index].Value = "Error";
                    row.Cells[colBpm.Index].ToolTipText = ex.Message;
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
    /// Every MP3 under <paramref name="folderPath"/> with its artist, title and
    /// the BPM already in its tag (0 if none).
    /// Folders that can't be opened, and system folders such as the Recycle Bin
    /// and System Volume Information, are skipped.
    /// </summary>
    private static List<(string file, string artist, string title, uint tagBpm)> FindMp3Files(string folderPath)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };
        var mp3Files = Directory.EnumerateFiles(folderPath, "*.mp3", options).ToList();
        var results = new List<(string file, string artist, string title, uint tagBpm)>();

        foreach (var file in mp3Files)
        {
            string artist = "";
            string title = "";
            uint tagBpm = 0;
            try
            {
                using var tFile = TagLib.File.Create(file);
                if (tFile.Tag != null)
                {
                    artist = tFile.Tag.FirstPerformer ?? "";
                    title = tFile.Tag.Title ?? "";
                }

                // The BPM from the ID3v2 tag (any version), the tag Apply writes
                if (tFile.GetTag(TagLib.TagTypes.Id3v2) is TagLib.Id3v2.Tag id3v2)
                    tagBpm = id3v2.BeatsPerMinute;
            }
            catch { /* Ignore unreadable tags */ }

            results.Add((file, artist, title, tagBpm));
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

        // Only attempt to save if we have a successful BPM computation that differs from the tag
        var pending = new List<(DataGridViewRow row, uint bpm, string filePath)>();
        int unchangedCount = 0;
        foreach (DataGridViewRow row in gridFiles.Rows)
        {
            string status = row.Cells[colStatus.Index].Value?.ToString() ?? "";
            string bpmStr = row.Cells[colBpm.Index].Value?.ToString() ?? "";
            string tagStr = row.Cells[colTag.Index].Value?.ToString() ?? "";
            string filePath = row.Cells[colPath.Index].Value?.ToString() ?? "";

            if (status == "Completed" && uint.TryParse(bpmStr, out uint bpmVal) && !string.IsNullOrEmpty(filePath))
            {
                if (uint.TryParse(tagStr, out uint tagVal) && tagVal == bpmVal)
                {
                    // The file already has this BPM; skip rewriting it
                    row.Cells[colStatus.Index].Value = "Unchanged";
                    unchangedCount++;
                }
                else
                {
                    pending.Add((row, bpmVal, filePath));
                }
            }
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

                row.Cells[colStatus.Index].Value = "Saving...";

                // Save off the UI thread so the window stays responsive
                string? error = await Task.Run(() => SaveBpm(filePath, bpmVal));
                if (error == null)
                {
                    row.Cells[colStatus.Index].Value = "Saved!";
                    row.Cells[colTag.Index].Value = bpmVal.ToString();
                    savedCount++;
                }
                else
                {
                    row.Cells[colStatus.Index].Value = "Save Error";
                    row.Cells[colBpm.Index].ToolTipText = error;
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
            lblStatus.Text = $"Apply operation {(stopped ? "stopped" : "complete")}. Saved: {savedCount}, Unchanged: {unchangedCount}, Errors: {errorCount}";

            if (_closeWhenIdle) Close();
        }
    }

    /// <summary>
    /// Writes <paramref name="bpm"/> to the file's ID3v2 tag, keeping its version
    /// (2.3 or 2.4), or to a new ID3v2.3 tag if the file has none. Other tags are
    /// left as they are. Returns null on success, or the error message.
    /// </summary>
    private static string? SaveBpm(string filePath, uint bpm)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            var tagsOnDisk = file.TagTypesOnDisk;

            if (file.GetTag(TagLib.TagTypes.Id3v2, true) is not TagLib.Id3v2.Tag id3v2)
                return "This file can't hold an ID3v2 tag";

            id3v2.BeatsPerMinute = bpm;

            // TagLib adds an empty ID3v1 tag to every MP3 it opens; don't write one the file didn't have
            if (!tagsOnDisk.HasFlag(TagLib.TagTypes.Id3v1))
                file.RemoveTags(TagLib.TagTypes.Id3v1);

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
