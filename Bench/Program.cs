using System.Diagnostics;
using System.Globalization;
using System.Text;
using bpmGenie;

namespace bpmGenie.Bench;

/// <summary>
/// Runs BpmAnalyzerV1 and BpmAnalyzerV2 over a folder of songs, decoding each
/// song once so both versions analyse the same samples, and writes
/// results.csv (one row per song) and summary.txt. Never writes to the songs.
/// </summary>
static class Program
{
    private const string Usage =
        "Usage: bpmGenie.Bench <song folder> [--reference <csv>] [--out <folder>]\n" +
        "                      [--start <s>] [--duration <s>] [--jobs <n>] [--limit <n>]\n" +
        "\n" +
        "  --reference  CSV of Name,BPM where Name is the file name without .mp3\n" +
        "               (default: <song folder>\\<folder name>.csv if it exists)\n" +
        "  --out        Results folder (default: bench-results\\<date-time>)\n" +
        "  --start      Seconds into each song to start decoding (default 0, as the app)\n" +
        "  --duration   Seconds to decode (default 60, as the app)\n" +
        "  --jobs       Songs analysed at once (default: half the CPU threads)\n" +
        "  --limit      Only the first n songs, by name";

    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "/?")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        string songFolder = Path.GetFullPath(args[0]);
        string? referencePath = null;
        string? outFolder = null;
        int startSeconds = 0;
        int durationSeconds = 60;
        int jobs = Math.Max(1, Environment.ProcessorCount / 2);
        int? limit = null;

        try
        {
            for (int i = 1; i < args.Length; i++)
            {
                string value = i + 1 < args.Length ? args[i + 1] : throw new ArgumentException($"{args[i]} needs a value");
                switch (args[i].ToLowerInvariant())
                {
                    case "--reference": referencePath = Path.GetFullPath(value); break;
                    case "--out": outFolder = Path.GetFullPath(value); break;
                    case "--start": startSeconds = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--duration": durationSeconds = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "--jobs": jobs = Math.Max(1, int.Parse(value, CultureInfo.InvariantCulture)); break;
                    case "--limit": limit = int.Parse(value, CultureInfo.InvariantCulture); break;
                    default: throw new ArgumentException($"Unknown option {args[i]}");
                }
                i++;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(Usage);
            return 1;
        }

        if (!Directory.Exists(songFolder))
        {
            Console.Error.WriteLine($"Song folder not found: {songFolder}");
            return 1;
        }

        if (referencePath == null)
        {
            string defaultReference = Path.Combine(songFolder, Path.GetFileName(songFolder.TrimEnd('\\')) + ".csv");
            if (File.Exists(defaultReference)) referencePath = defaultReference;
        }
        else if (!File.Exists(referencePath))
        {
            Console.Error.WriteLine($"Reference CSV not found: {referencePath}");
            return 1;
        }

        outFolder ??= Path.GetFullPath(Path.Combine("bench-results", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(outFolder);

        var reference = referencePath != null ? ReferenceList.Load(referencePath) : ReferenceList.Empty;

        var files = Directory.GetFiles(songFolder, "*.mp3", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (limit.HasValue) files = files.Take(limit.Value).ToList();

        Console.WriteLine($"Songs:     {songFolder} ({files.Count} MP3s)");
        Console.WriteLine($"Reference: {(referencePath ?? "none")} ({reference.WithBpm} rows with a BPM)");
        Console.WriteLine($"Results:   {outFolder}");
        Console.WriteLine($"Analysing {startSeconds} s for {durationSeconds} s of each song, {jobs} at a time. Ctrl+C stops and keeps what is done.");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var results = new List<SongResult>(files.Count);
        var resultsLock = new object();
        int done = 0;
        var wallClock = Stopwatch.StartNew();
        bool canceled = false;

        try
        {
            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = jobs, CancellationToken = cts.Token }, async (file, ct) =>
            {
                var result = await AnalyseSongAsync(file, songFolder, reference, startSeconds, durationSeconds, ct);
                lock (resultsLock) results.Add(result);

                int count = Interlocked.Increment(ref done);
                if (count % 25 == 0 || count == files.Count)
                    Console.Write($"\r  {count} of {files.Count}");
            });
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        wallClock.Stop();
        Console.WriteLine();

        results.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.File, b.File));

        string resultsPath = Path.Combine(outFolder, "results.csv");
        string summaryPath = Path.Combine(outFolder, "summary.txt");
        WriteResults(resultsPath, results);

        string summary = Summary.Build(songFolder, files.Count, referencePath, reference, results,
            startSeconds, durationSeconds, jobs, wallClock.Elapsed, canceled);
        File.WriteAllText(summaryPath, summary, Encoding.UTF8);

        Console.WriteLine();
        Console.WriteLine(summary);
        Console.WriteLine($"Wrote {resultsPath}");
        Console.WriteLine($"Wrote {summaryPath}");
        return canceled ? 2 : 0;
    }

    private static async Task<SongResult> AnalyseSongAsync(string file, string songFolder, ReferenceList reference,
        int startSeconds, int durationSeconds, CancellationToken ct)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        var result = new SongResult
        {
            File = Path.GetRelativePath(songFolder, file),
            Reference = reference.Find(name),
            Tag = ReadBpmTag(file)
        };

        try
        {
            var sw = Stopwatch.StartNew();
            float[] signal = await AudioDecoder.DecodeAsync(file, ct, startSeconds, durationSeconds);
            result.DecodeMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            result.V1 = BpmAnalyzerV1.Analyze(signal, ct);
            result.V1Ms = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            result.V2 = BpmAnalyzerV2.Analyze(signal, ct);
            result.V2Ms = sw.Elapsed.TotalMilliseconds;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message.ReplaceLineEndings(" ").Trim();
        }

        return result;
    }

    /// <summary>
    /// The BPM already in the file's tag, or null if it has none or can't be read.
    /// </summary>
    private static int? ReadBpmTag(string file)
    {
        try
        {
            using var tagFile = TagLib.File.Create(file);
            uint bpm = tagFile.Tag?.BeatsPerMinute ?? 0;
            return bpm > 0 ? (int)bpm : null;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteResults(string path, List<SongResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("File,Reference,Tag,V1,V2,V1Result,V2Result,V1Diff,V2Diff,V1V2Same,DecodeMs,V1Ms,V2Ms,Error");

        foreach (var r in results)
        {
            sb.AppendJoin(',',
                Csv.Field(r.File),
                Csv.Number(r.Reference),
                Csv.Number(r.Tag),
                Csv.Number(r.V1),
                Csv.Number(r.V2),
                r.Error == null && r.Reference.HasValue ? Accuracy.Classify(r.V1, r.Reference.Value) : "",
                r.Error == null && r.Reference.HasValue ? Accuracy.Classify(r.V2, r.Reference.Value) : "",
                Csv.Number(r.V1 - r.Reference),
                Csv.Number(r.V2 - r.Reference),
                r.Error == null ? (r.V1 == r.V2 ? "yes" : "no") : "",
                Csv.Number(r.Error == null ? Math.Round(r.DecodeMs) : null),
                Csv.Number(r.Error == null ? Math.Round(r.V1Ms) : null),
                Csv.Number(r.Error == null ? Math.Round(r.V2Ms) : null),
                Csv.Field(r.Error ?? ""));
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); // BOM so Excel reads accented names
    }
}

sealed class SongResult
{
    public string File { get; init; } = "";
    public double? Reference { get; init; }
    public int? Tag { get; init; }
    public int? V1 { get; set; }
    public int? V2 { get; set; }
    public double DecodeMs { get; set; }
    public double V1Ms { get; set; }
    public double V2Ms { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Reference BPMs from a CSV of Name,BPM. Rows without a BPM (including a
/// header row) are counted but ignored.
/// </summary>
sealed class ReferenceList
{
    public static readonly ReferenceList Empty = new();

    private readonly Dictionary<string, double> _bpmByName = new(StringComparer.OrdinalIgnoreCase);

    public int Rows { get; private set; }
    public int WithBpm => _bpmByName.Count;
    public int Duplicates { get; private set; }

    public static ReferenceList Load(string path)
    {
        var list = new ReferenceList();
        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            list.Rows++;

            var fields = Csv.Parse(line);
            if (fields.Count < 2) continue;

            string name = fields[0].Trim();
            if (name.Length == 0
                || !double.TryParse(fields[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm)
                || bpm <= 0)
                continue;

            if (!list._bpmByName.TryAdd(name, bpm)) list.Duplicates++;
        }
        return list;
    }

    public double? Find(string name) => _bpmByName.TryGetValue(name.Trim(), out double bpm) ? bpm : null;
}

static class Accuracy
{
    public const string Exact = "exact";
    public const string Within1 = "within 1";
    public const string Within4Pct = "within 4%";
    public const string None = "no result";
    public const string Wrong = "wrong";

    // Tempo mistakes by a whole factor, each judged within 4% of reference x factor
    public static readonly (double Factor, string Label)[] Multiples =
    [
        (2.0, "double"),
        (0.5, "half"),
        (3.0, "triple"),
        (1.0 / 3.0, "third"),
    ];

    public static readonly string[] AllClasses =
        [Exact, Within1, Within4Pct, .. Multiples.Select(m => m.Label), Wrong, None];

    public static string Classify(int? estimate, double reference)
    {
        if (!estimate.HasValue || estimate.Value <= 0) return None;

        double est = estimate.Value;
        double diff = Math.Abs(est - reference);

        if (est == Math.Round(reference, MidpointRounding.AwayFromZero)) return Exact;
        if (diff <= 1.0) return Within1;
        if (diff <= 0.04 * reference) return Within4Pct;

        foreach (var (factor, label) in Multiples)
        {
            double target = reference * factor;
            if (Math.Abs(est - target) <= 0.04 * target) return label;
        }

        return Wrong;
    }
}

static class Summary
{
    public static string Build(string songFolder, int fileCount, string? referencePath, ReferenceList reference,
        List<SongResult> results, int startSeconds, int durationSeconds, int jobs, TimeSpan wallClock, bool canceled)
    {
        var sb = new StringBuilder();
        var ok = results.Where(r => r.Error == null).ToList();
        var errors = results.Where(r => r.Error != null).ToList();

        sb.AppendLine($"bpmGenie bench, {DateTime.Now:yyyy-MM-dd HH:mm}{(canceled ? "  (STOPPED EARLY)" : "")}");
        sb.AppendLine($"Songs:      {songFolder} ({fileCount} MP3s, {results.Count} analysed)");
        if (referencePath != null)
        {
            int matched = ok.Count(r => r.Reference.HasValue);
            sb.AppendLine($"Reference:  {referencePath}");
            sb.AppendLine($"            {reference.Rows} rows, {reference.WithBpm} with a BPM, {matched} matched to an analysed MP3" +
                          (reference.Duplicates > 0 ? $", {reference.Duplicates} duplicate names ignored" : ""));
        }
        else
        {
            sb.AppendLine("Reference:  none");
        }
        sb.AppendLine($"Window:     {startSeconds} s for {durationSeconds} s, {AudioDecoder.SampleRate} Hz mono, {jobs} songs at a time");
        sb.AppendLine($"Errors:     {errors.Count} (see Error column in results.csv)");
        sb.AppendLine();

        // V1 against V2 needs no reference
        sb.AppendLine($"V1 vs V2 ({ok.Count} songs)");
        int same = ok.Count(r => r.V1 == r.V2);
        int byOne = ok.Count(r => r.V1 != r.V2 && Math.Abs((r.V1 ?? 0) - (r.V2 ?? 0)) == 1);
        int more = ok.Count - same - byOne;
        sb.AppendLine($"  Same BPM          {Count(same, ok.Count)}");
        sb.AppendLine($"  Differ by 1       {Count(byOne, ok.Count)}");
        sb.AppendLine($"  Differ by more    {Count(more, ok.Count)}");

        var largest = ok.Where(r => r.V1 != r.V2)
            .OrderByDescending(r => Math.Abs((r.V1 ?? 0) - (r.V2 ?? 0)))
            .ThenBy(r => r.File, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        if (largest.Count > 0)
        {
            sb.AppendLine("  Largest differences (V1 -> V2):");
            foreach (var r in largest)
                sb.AppendLine($"    {r.V1,4} -> {r.V2,-4} {r.File}");
        }
        sb.AppendLine();

        // Accuracy against the reference
        var scored = ok.Where(r => r.Reference.HasValue).ToList();
        if (scored.Count == 0)
        {
            sb.AppendLine("Accuracy: no songs have a reference BPM yet.");
        }
        else
        {
            var v1 = scored.Select(r => Accuracy.Classify(r.V1, r.Reference!.Value)).ToList();
            var v2 = scored.Select(r => Accuracy.Classify(r.V2, r.Reference!.Value)).ToList();
            string[] withinOne = [Accuracy.Exact, Accuracy.Within1];
            string[] withinFour = [.. withinOne, Accuracy.Within4Pct];
            string[] anyMultiple = [.. withinFour, .. Accuracy.Multiples.Select(m => m.Label)];

            sb.AppendLine($"Accuracy against reference ({scored.Count} songs)");
            sb.AppendLine($"  {"",-38}{"V1",-16}{"V2",-16}");
            Row(sb, "Exact", v1.Count(c => c == Accuracy.Exact), v2.Count(c => c == Accuracy.Exact), scored.Count);
            Row(sb, "Within 1 BPM", v1.Count(withinOne.Contains), v2.Count(withinOne.Contains), scored.Count);
            Row(sb, "Within 4%", v1.Count(withinFour.Contains), v2.Count(withinFour.Contains), scored.Count);
            Row(sb, "Within 4%, or x2 / x1/2 / x3 / x1/3", v1.Count(anyMultiple.Contains), v2.Count(anyMultiple.Contains), scored.Count);
            sb.AppendLine();
            sb.AppendLine("  Breakdown");
            foreach (string c in Accuracy.AllClasses)
                Row(sb, "  " + c, v1.Count(x => x == c), v2.Count(x => x == c), scored.Count);
        }
        sb.AppendLine();

        // The tags in the bank were written by an earlier bpmGenie; V1 should reproduce them
        var tagged = ok.Where(r => r.Tag.HasValue).ToList();
        if (tagged.Count > 0)
        {
            sb.AppendLine($"V1 vs BPM tags already in the files ({tagged.Count} tagged)");
            sb.AppendLine($"  Same BPM          {Count(tagged.Count(r => r.V1 == r.Tag), tagged.Count)}");
            sb.AppendLine();
        }

        if (ok.Count > 0)
        {
            sb.AppendLine("Time per song (average)");
            sb.AppendLine($"  Decode            {ok.Average(r => r.DecodeMs),8:F0} ms");
            sb.AppendLine($"  V1                {ok.Average(r => r.V1Ms),8:F0} ms");
            sb.AppendLine($"  V2                {ok.Average(r => r.V2Ms),8:F0} ms");
        }
        sb.AppendLine($"Total run time      {wallClock:h\\:mm\\:ss}");

        return sb.ToString();
    }

    private static string Count(int n, int total) =>
        total == 0 ? $"{n}" : $"{n,5}  ({100.0 * n / total:F1}%)";

    private static void Row(StringBuilder sb, string label, int v1, int v2, int total) =>
        sb.AppendLine($"  {label,-38}{Count(v1, total),-16}{Count(v2, total),-16}");
}

static class Csv
{
    public static string Field(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    public static string Number(double? value) =>
        value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "";

    /// <summary>
    /// Splits one CSV line, honouring double-quoted fields with "" escapes.
    /// </summary>
    public static List<string> Parse(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }

        fields.Add(field.ToString());
        return fields;
    }
}
