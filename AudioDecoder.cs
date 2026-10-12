using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace bpmGenie
{
    /// <summary>
    /// Decodes audio files to mono 32-bit float PCM with ffmpeg.
    /// </summary>
    public static class AudioDecoder
    {
        public const int SampleRate = 11025; // Lower sample rate focuses on bass/rhythm and speeds up processing
        private const int NumChannels = 1; // mono

        /// <summary>
        /// Decodes an audio file to mono float samples at <see cref="SampleRate"/>.
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="startSeconds">Optional start offset in seconds.</param>
        /// <param name="durationSeconds">Optional length to decode in seconds.</param>
        public static async Task<float[]> DecodeAsync(string filePath, CancellationToken ct, int? startSeconds = null, int? durationSeconds = null)
        {
            string tempPcm = Path.Combine(Path.GetTempPath(), $"bg_bpm_{Guid.NewGuid():N}.raw");

            try
            {
                // ffmpeg ships in an "ffmpeg" folder next to the EXE; an older
                // install may have it next to the EXE or in the working directory
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string ffmpegPath = Path.Combine(appDir, "ffmpeg", "ffmpeg.exe");
                if (!File.Exists(ffmpegPath))
                    ffmpegPath = Path.Combine(appDir, "ffmpeg.exe");
                if (!File.Exists(ffmpegPath))
                {
                    // Fallback to project dir if running from IDE
                    ffmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe");
                }

                if (!File.Exists(ffmpegPath))
                {
                    throw new FileNotFoundException("ffmpeg.exe not found in " + Path.Combine(appDir, "ffmpeg"), ffmpegPath);
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

                byte[] rawBytes = await File.ReadAllBytesAsync(tempPcm, ct).ConfigureAwait(false);
                float[] signal = new float[rawBytes.Length / sizeof(float)];
                Buffer.BlockCopy(rawBytes, 0, signal, 0, signal.Length * sizeof(float));
                return signal;
            }
            finally
            {
                // Clean up temp file
                try { if (File.Exists(tempPcm)) File.Delete(tempPcm); } catch { }
            }
        }
    }
}
