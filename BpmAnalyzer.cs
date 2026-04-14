using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NWaves.Signals;
using NWaves.Transforms;
using NWaves.Windows;

namespace bpmGenie
{
    /// <summary>
    /// BPM detection helper that uses ffmpeg to decode audio to raw PCM
    /// and NWaves for highly accurate Spectral Flux beat analysis (Mixxx style).
    /// </summary>
    public static class BpmAnalyzer
    {
        private const int SampleRate = 11025; // Lower sample rate focuses on bass/rhythm and speeds up processing
        private const int NumChannels = 1; // mono

        /// <summary>
        /// Detects the BPM of an audio file.
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Detected BPM rounded to nearest integer, or 0 if detection failed.</returns>
        public static async Task<int> DetectBpmAsync(string filePath, CancellationToken ct, int? startSeconds = null, int? durationSeconds = null)
        {
            // Use ffmpeg to decode audio to raw 32-bit float mono PCM
            string tempPcm = Path.Combine(Path.GetTempPath(), $"bg_bpm_{Guid.NewGuid():N}.raw");

            try
            {
                // Note: user said "ffmpeg is in the project folder", so it will be beside the executable or in the CWD
                string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                if (!File.Exists(ffmpegPath))
                {
                    // Fallback to project dir if running from IDE
                    ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe");
                }
                
                if (!File.Exists(ffmpegPath))
                {
                    throw new FileNotFoundException("ffmpeg.exe not found. Please ensure it is in the project folder.", ffmpegPath);
                }

                string timeArgs = "";
                if (startSeconds.HasValue) timeArgs += $"-ss {startSeconds.Value} ";
                if (durationSeconds.HasValue) timeArgs += $"-t {durationSeconds.Value} ";

                // Decode to raw 32-bit float, mono, 11025 Hz
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-y -nostdin {timeArgs}-i \"{filePath}\" -vn -ac {NumChannels} -ar {SampleRate} -f f32le -acodec pcm_f32le \"{tempPcm}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = false
                };

                using var process = Process.Start(psi);
                if (process == null)
                    throw new InvalidOperationException("Failed to start ffmpeg process.");

                // Concurrently read stderr to prevent pipe buffer deadlocks
                var stderrTask = process.StandardError.ReadToEndAsync((CancellationToken)default);
                
                // Wait for ffmpeg to finish
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}: {stderr}");
                }

                ct.ThrowIfCancellationRequested();

                // Read the raw PCM data and perform Spectral Flux BPM analysis
                return await Task.Run(() => AnalyzePcmFile(tempPcm, ct), ct).ConfigureAwait(false);
            }
            finally
            {
                // Clean up temp file
                try { if (File.Exists(tempPcm)) File.Delete(tempPcm); } catch { }
            }
        }

        private static int AnalyzePcmFile(string pcmPath, CancellationToken ct)
        {
            // Read all bytes (requires reasonable memory suitable for 30s-60s mono 11025Hz snippets)
            byte[] rawBytes = File.ReadAllBytes(pcmPath);
            int samplesCount = rawBytes.Length / sizeof(float);
            
            if (samplesCount == 0) return 0;
            
            float[] floatBuffer = new float[samplesCount];
            Buffer.BlockCopy(rawBytes, 0, floatBuffer, 0, rawBytes.Length);

            ct.ThrowIfCancellationRequested();

            var signal = new DiscreteSignal(SampleRate, floatBuffer);

            int fftSize = 1024;
            int hopSize = 128; // Reduced from 256 to double the temporal resolution (~11.6ms precision)
            var stft = new Stft(fftSize, hopSize, WindowType.Hann);
            var spectrogram = stft.Spectrogram(signal);

            if (spectrogram == null || spectrogram.Count < 2) return 0;

            int frames = spectrogram.Count;
            int bins = spectrogram[0].Length;
            
            // Nyquist is SampleRate/2 (5512.5 Hz). Bin 0 is DC, Bin 512 is Nyquist.
            // Frequency per bin = 5512.5 / 512 = ~10.76 Hz
            // We want to limit Onset Detection to frequencies below ~250Hz where the main beat (kick/bass) occurs
            int maxBin = (int)Math.Min(bins, Math.Ceiling(250.0 / (SampleRate / 2.0) * bins));

            float[] flux = new float[frames];
            
            // Calculate Spectral Flux (positive energy differences)
            for (int i = 1; i < frames; i++) 
            {
                ct.ThrowIfCancellationRequested();
                float sum = 0f;
                // Only loop over the lower frequency bins
                for (int j = 0; j < maxBin; j++) 
                {
                    float diff = spectrogram[i][j] - spectrogram[i-1][j];
                    if (diff > 0) sum += diff;
                }
                flux[i] = sum;
            }
            ct.ThrowIfCancellationRequested();

            float envFs = (float)SampleRate / hopSize; // Envelope sampling rate

            // ----------------------------------------------------
            // Inter-Onset-Interval (IOI) Histogram Analysis
            // Extremely robust against Syncopation, Triplets, and Half-Time rhythms
            // ----------------------------------------------------
            
            // 1. Peak Picking algorithm over the Spectral Flux
            List<int> peakFrames = new List<int>();
            int minFramesBetweenBeats = (int)(envFs * 0.15f); // ~0.15s minimum gap between beats
            int historySize = (int)(envFs * 1.5f); // 1.5-second history for local moving average
            
            for (int i = 0; i < frames; i++) {
                float sumAvg = 0;
                int countAvg = 0;
                for (int k = Math.Max(0, i - historySize); k <= i; k++) {
                    sumAvg += flux[k]; 
                    countAvg++;
                }
                float movingAvg = countAvg > 0 ? sumAvg / countAvg : 0;
                
                // If flux sharply exceeds the local history average
                if (flux[i] > movingAvg * 1.35f) { 
                    bool isLocalMax = true;
                    int neighborhood = 2; // ~0.04s local peak isolation
                    for (int k = Math.Max(0, i - neighborhood); k <= Math.Min(frames - 1, i + neighborhood); k++) {
                        if (flux[k] > flux[i]) isLocalMax = false;
                    }
                    if (isLocalMax) {
                        if (peakFrames.Count == 0 || i - peakFrames[peakFrames.Count - 1] >= minFramesBetweenBeats) {
                            peakFrames.Add(i);
                        }
                    }
                }
            }

            // 2. IOI Histogram Clustering
            var bpmHistogram = new Dictionary<int, float>();
            float maxWeight = 0;
            int bestBpm = 0;

            for (int i = 0; i < peakFrames.Count; i++) {
                // Check intervals between the current peak and the next 8 peaks
                int searchDepth = Math.Min(8, peakFrames.Count - i - 1);
                for (int j = 1; j <= searchDepth; j++) { 
                    int deltaFrames = peakFrames[i + j] - peakFrames[i];
                    float deltaSec = deltaFrames / envFs;
                    if (deltaSec <= 0) continue;

                    float bpmRaw = 60f / deltaSec;
                    
                    var candidates = new List<(float bpm, float weight)>();
                    candidates.Add((bpmRaw * j, 1.0f));                     // 1 beat per peak
                    candidates.Add((bpmRaw * (j / 2f), 0.8f));              // 2 peaks per beat (8th notes)
                    candidates.Add((bpmRaw * (j * 2f), 0.8f));              // 1 peak per 2 beats (half time)
                    candidates.Add((bpmRaw * (j / 4f), 0.4f));              // 4 peaks per beat (16th notes)
                    candidates.Add((bpmRaw * (j / 3f), 0.4f));              // 3 peaks per beat (triplets)
                    candidates.Add((bpmRaw * (j * 0.6666f), 0.4f));         // 3 peaks per 2 beats (triplets)
                    candidates.Add((bpmRaw * (j * 1.5f), 0.25f));           // dotted / tresillo
                    candidates.Add((bpmRaw * (j * 0.75f), 0.25f));          // dotted
                    candidates.Add((bpmRaw * (j * 1.3333f), 0.25f));        // polyrhythm
                    candidates.Add((bpmRaw * (j + 1f), 0.4f));              // missed peak
                    candidates.Add((bpmRaw * Math.Max(0.5f, j - 1f), 0.4f));// extra peak
                    
                    foreach(var cand in candidates) {
                        float candBpm = cand.bpm;
                        if (candBpm >= 70 && candBpm <= 170) {
                            int b = (int)Math.Round(candBpm);
                            if (!bpmHistogram.ContainsKey(b)) bpmHistogram[b] = 0;
                            
                            // Standard log-normal penalty preventing extremes (Gaussian centered at 120 BPM)
                            double log2 = Math.Log(candBpm / 120.0, 2.0);
                            double priorWeight = Math.Exp(-0.5 * Math.Pow(log2 / 0.4, 2.0));
                            
                            // Combine Prior with the intrinsic candidate trust weight
                            bpmHistogram[b] += (float)(priorWeight * cand.weight / Math.Sqrt(j));
                            
                            if (bpmHistogram[b] > maxWeight) {
                                maxWeight = bpmHistogram[b];
                                bestBpm = b;
                            }
                        }
                    }
                }
            }
            
            // Fallback for silence
            if (bestBpm == 0) return 120;

            return bestBpm;
        }
    }
}
