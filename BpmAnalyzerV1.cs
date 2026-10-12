using System;
using System.Threading;
using NWaves.Transforms;
using NWaves.Windows;

namespace bpmGenie
{
    /// <summary>
    /// The BPM analysis shipped in bpmGenie 1.0: hand-framed FFT, complex domain
    /// onset detection and autocorrelation. Kept unchanged so its results can be
    /// reproduced and compared against later versions.
    /// </summary>
    public static class BpmAnalyzerV1
    {
        /// <summary>
        /// Detects the BPM of mono samples at <see cref="AudioDecoder.SampleRate"/>.
        /// </summary>
        /// <returns>Detected BPM rounded to nearest integer, or 0 if detection failed.</returns>
        public static int Analyze(float[] signal, CancellationToken ct)
        {
            const int SampleRate = AudioDecoder.SampleRate;

            if (signal.Length == 0) return 0;

            ct.ThrowIfCancellationRequested();

            int fftSize = 1024;
            int hopSize = 256;

            var fft = new Fft(fftSize);
            float[] window = Window.OfType(WindowType.Hann, fftSize);

            int framesCount = (signal.Length - fftSize) / hopSize + 1;
            if (framesCount < 10) return 0;

            float[] odf = new float[framesCount]; // Onset Detection Function
            int halfFft = fftSize / 2 + 1;

            float[] prevMag = new float[halfFft];
            float[] prevPhase = new float[halfFft];
            float[] prevPrevPhase = new float[halfFft];

            float[] real = new float[fftSize];
            float[] imag = new float[fftSize];

            // 1. Complex Domain ODF Computation
            for (int i = 0; i < framesCount; i++)
            {
                if (i % 100 == 0) ct.ThrowIfCancellationRequested();

                int offset = i * hopSize;

                // Copy frame and apply window
                for (int j = 0; j < fftSize; j++)
                {
                    real[j] = signal[offset + j] * window[j];
                    imag[j] = 0f;
                }

                // Perform FFT
                fft.Direct(real, imag);

                float frameOdfValue = 0f;

                for (int k = 0; k < halfFft; k++)
                {
                    // Magnitude and Phase of current frame
                    float re = real[k];
                    float im = imag[k];
                    float mag = (float)Math.Sqrt(re * re + im * im);
                    float phase = (float)Math.Atan2(im, re);

                    // Predict phase based on phase propagation
                    float predictedPhase = 2 * prevPhase[k] - prevPrevPhase[k];

                    // Predict complex value (using previous magnitude and predicted phase)
                    float predRe = prevMag[k] * (float)Math.Cos(predictedPhase);
                    float predIm = prevMag[k] * (float)Math.Sin(predictedPhase);

                    // Compute complex difference magnitude
                    float diffRe = re - predRe;
                    float diffIm = im - predIm;
                    float complexDiff = (float)Math.Sqrt(diffRe * diffRe + diffIm * diffIm);

                    frameOdfValue += complexDiff;

                    // Update memory
                    prevPrevPhase[k] = prevPhase[k];
                    prevPhase[k] = phase;
                    prevMag[k] = mag;
                }

                odf[i] = frameOdfValue;
            }

            ct.ThrowIfCancellationRequested();

            // Optional: Smooth ODF or subtract local mean (median filter or simple mean)
            float[] smoothedOdf = new float[odf.Length];
            int localMeanSize = 10;
            for (int i = 0; i < odf.Length; i++)
            {
                int start = Math.Max(0, i - localMeanSize);
                int end = Math.Min(odf.Length - 1, i + localMeanSize);
                float sum = 0f;
                for (int j = start; j <= end; j++) sum += odf[j];
                float mean = sum / (end - start + 1);

                // Half-wave rectification
                smoothedOdf[i] = Math.Max(0, odf[i] - mean);
            }

            // 2. Autocorrelation for Periodicity (Tempo Estimation)
            float frameRate = (float)SampleRate / hopSize;

            // Limit BPM search space (e.g., 60 BPM to 200 BPM)
            int minBpm = 60;
            int maxBpm = 200;
            int maxLag = (int)(frameRate * 60 / minBpm);
            int minLag = (int)(frameRate * 60 / maxBpm);

            if (maxLag >= smoothedOdf.Length) maxLag = smoothedOdf.Length - 1;

            float bestBpm = 0f;
            float maxCorrelation = -1f;

            // Simple Auto-correlation function over the smoothed ODF
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                float correlation = 0f;
                for (int i = 0; i < smoothedOdf.Length - lag; i++)
                {
                    correlation += smoothedOdf[i] * smoothedOdf[i + lag];
                }

                // Apply a simple gaussian weighting around 120BPM so that extreme lags don't always win
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
