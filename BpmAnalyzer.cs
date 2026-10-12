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
        /// <returns>Detected BPM rounded to nearest integer, or 0 if detection failed.</returns>
        public static async Task<int> DetectBpmAsync(string filePath, CancellationToken ct, int? startSeconds = null, int? durationSeconds = null)
        {
            float[] signal = await AudioDecoder.DecodeAsync(filePath, ct, startSeconds, durationSeconds).ConfigureAwait(false);

            // V1 until BpmAnalyzerV2 passes the comparison against the test bank (see Bench)
            return await Task.Run(() => BpmAnalyzerV1.Analyze(signal, ct), ct).ConfigureAwait(false);
        }
    }
}
