using System.Threading;
using System.Threading.Tasks;

namespace bpmGenie
{
    /// <summary>
    /// BPM detection helper that uses ffmpeg to decode audio to raw PCM and
    /// complex domain onset detection with autocorrelation to find the tempo.
    /// </summary>
    public static class BpmAnalyzer
    {
        /// <summary>
        /// Detects the BPM of an audio file.
        /// </summary>
        /// <param name="filePath">Path to the audio file.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Detected BPM rounded to nearest integer, or 0 if the audio is silent or has no beat.</returns>
        /// <exception cref="System.InvalidOperationException">ffmpeg failed or decoded no audio.</exception>
        public static async Task<int> DetectBpmAsync(string filePath, CancellationToken ct, int? startSeconds = null, int? durationSeconds = null)
        {
            float[] signal = await AudioDecoder.DecodeAsync(filePath, ct, startSeconds, durationSeconds).ConfigureAwait(false);

            // No samples means no audio stream (or a broken file), not silence; 0 would clear its tag
            if (signal.Length == 0)
                throw new System.InvalidOperationException("ffmpeg decoded no audio from this file.");

            return await Task.Run(() => BpmAnalyzerV2.Analyze(signal, ct), ct).ConfigureAwait(false);
        }
    }
}
