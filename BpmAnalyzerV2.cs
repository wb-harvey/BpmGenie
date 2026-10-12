using System;
using System.Threading;
using NWaves.Transforms;
using NWaves.Windows;

namespace bpmGenie
{
    /// <summary>
    /// BPM analysis for bpmGenie 2.0: the V1 method (complex domain onset
    /// detection and autocorrelation) with the framing, windowing and FFT done by
    /// the NWaves short-time Fourier transform.
    /// </summary>
    public static class BpmAnalyzerV2
    {
        private const int FftSize = 1024;
        private const int HopSize = 256;

        /// <summary>
        /// Detects the BPM of mono samples at <see cref="AudioDecoder.SampleRate"/>.
        /// </summary>
        /// <returns>Detected BPM rounded to nearest integer, or 0 if detection failed.</returns>
        public static int Analyze(float[] signal, CancellationToken ct)
        {
            if (signal.Length == 0) return 0;

            ct.ThrowIfCancellationRequested();

            int framesCount = (signal.Length - FftSize) / HopSize + 1;
            if (framesCount < 10) return 0;

            // Hann-windowed frames of FftSize every HopSize samples, as magnitude and phase per bin
            var stft = new Stft(FftSize, HopSize, WindowType.Hann, FftSize);
            var spectrogram = stft.MagnitudePhaseSpectrogram(signal);

            // NWaves adds a zero-padded frame past the last full one; use the full frames only
            framesCount = Math.Min(framesCount, spectrogram.Magnitudes.Count);

            ct.ThrowIfCancellationRequested();

            float[] odf = OnsetDetectionFunction(spectrogram, framesCount, ct);
            float[] smoothedOdf = SubtractLocalMean(odf);
            return EstimateTempo(smoothedOdf);
        }

        /// <summary>
        /// Complex domain onset detection: for each frame, the summed distance
        /// between every bin and the value predicted from the previous two frames
        /// (previous magnitude, phase advanced at the same rate).
        /// </summary>
        private static float[] OnsetDetectionFunction(MagnitudePhaseList spectrogram, int framesCount, CancellationToken ct)
        {
            int bins = FftSize / 2 + 1;

            float[] odf = new float[framesCount];
            float[] prevMag = new float[bins];
            float[] prevPhase = new float[bins];
            float[] prevPrevPhase = new float[bins];

            for (int i = 0; i < framesCount; i++)
            {
                if (i % 100 == 0) ct.ThrowIfCancellationRequested();

                float[] magnitudes = spectrogram.Magnitudes[i];
                float[] phases = spectrogram.Phases[i];
                float frameOdfValue = 0f;

                for (int k = 0; k < bins; k++)
                {
                    float mag = magnitudes[k];
                    float phase = phases[k];
                    float predictedPhase = 2 * prevPhase[k] - prevPrevPhase[k];

                    // Distance between mag at phase and prevMag at predictedPhase (law of cosines)
                    float distanceSq = mag * mag + prevMag[k] * prevMag[k]
                        - 2 * mag * prevMag[k] * (float)Math.Cos(phase - predictedPhase);
                    frameOdfValue += (float)Math.Sqrt(Math.Max(0f, distanceSq));

                    prevPrevPhase[k] = prevPhase[k];
                    prevPhase[k] = phase;
                    prevMag[k] = mag;
                }

                odf[i] = frameOdfValue;
            }

            return odf;
        }

        /// <summary>
        /// Subtracts the mean of the surrounding ±10 frames and keeps only the
        /// positive part, so the result rises at onsets and is zero elsewhere.
        /// </summary>
        private static float[] SubtractLocalMean(float[] odf)
        {
            const int LocalMeanSize = 10;

            float[] smoothedOdf = new float[odf.Length];
            for (int i = 0; i < odf.Length; i++)
            {
                int start = Math.Max(0, i - LocalMeanSize);
                int end = Math.Min(odf.Length - 1, i + LocalMeanSize);
                float sum = 0f;
                for (int j = start; j <= end; j++) sum += odf[j];
                float mean = sum / (end - start + 1);

                smoothedOdf[i] = Math.Max(0, odf[i] - mean);
            }

            return smoothedOdf;
        }

        /// <summary>
        /// Autocorrelates the onset function over lags covering 60-200 BPM,
        /// weighted toward 120 BPM, and returns the best scoring tempo.
        /// </summary>
        private static int EstimateTempo(float[] smoothedOdf)
        {
            const int MinBpm = 60;
            const int MaxBpm = 200;

            float frameRate = (float)AudioDecoder.SampleRate / HopSize;
            int maxLag = (int)(frameRate * 60 / MinBpm);
            int minLag = (int)(frameRate * 60 / MaxBpm);

            if (maxLag >= smoothedOdf.Length) maxLag = smoothedOdf.Length - 1;

            float bestBpm = 0f;
            float maxCorrelation = -1f;

            for (int lag = minLag; lag <= maxLag; lag++)
            {
                float correlation = 0f;
                for (int i = 0; i < smoothedOdf.Length - lag; i++)
                {
                    correlation += smoothedOdf[i] * smoothedOdf[i + lag];
                }

                // Gaussian weighting (in octaves) around 120 BPM so extreme lags don't always win
                float currentBpm = 60f * frameRate / lag;
                double log2 = Math.Log(currentBpm / 120.0, 2.0);
                double priorWeight = Math.Exp(-0.5 * Math.Pow(log2 / 0.5, 2.0));

                correlation *= (float)priorWeight;

                if (correlation > maxCorrelation)
                {
                    maxCorrelation = correlation;
                    bestBpm = currentBpm;
                }
            }

            return (int)Math.Round(bestBpm);
        }
    }
}
