// BpmGenie.Core - BPM detection of bpmGenie
// Copyright (c) 2026 William Harvey
//
// Licensed under the MIT License. See LICENSE for the full text.

using System;
using System.Threading;
using NWaves.Transforms;
using NWaves.Windows;

namespace bpmGenie
{
    /// <summary>
    /// BPM analysis for bpmGenie 2.0: the V1 method (complex domain onset
    /// detection and autocorrelation) with the framing, windowing and FFT done by
    /// the NWaves short-time Fourier transform, and the autocorrelation peak
    /// refined to a fractional lag.
    /// </summary>
    public static class BpmAnalyzerV2
    {
        /// <summary>The sample rate Analyze expects (mono): low, which favours bass and rhythm and is fast.</summary>
        public const int SampleRate = 11025;

        private const int FftSize = 1024;
        private const int HopSize = 256;

        // Peak level (-60 dBFS) below which the audio counts as silent
        private const float SilencePeak = 0.001f;

        /// <summary>
        /// Detects the BPM of mono samples at <see cref="SampleRate"/>.
        /// </summary>
        /// <returns>Detected BPM rounded to nearest integer, or 0 if the audio is silent or has no beat.</returns>
        public static int Analyze(float[] signal, CancellationToken ct)
        {
            if (signal.Length == 0) return 0;

            ct.ThrowIfCancellationRequested();

            // Silence (or noise far below music level) has no tempo; its onsets would be random
            float peak = 0f;
            foreach (float sample in signal) peak = Math.Max(peak, Math.Abs(sample));
            if (peak < SilencePeak) return 0;

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
        /// picks the best lag weighted toward 120 BPM (as V1), and refines it to
        /// a fractional lag so the tempo isn't limited to whole frame lags.
        /// </summary>
        private static int EstimateTempo(float[] smoothedOdf)
        {
            const int MinBpm = 60;
            const int MaxBpm = 200;

            float frameRate = (float)SampleRate / HopSize;
            int maxLag = (int)(frameRate * 60 / MinBpm);
            int minLag = (int)(frameRate * 60 / MaxBpm);

            if (maxLag >= smoothedOdf.Length) maxLag = smoothedOdf.Length - 1;

            int bestLag = 0;
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
                    bestLag = lag;
                }
            }

            // No onsets at all scores every lag 0; don't report the first lag tried (215 BPM)
            if (bestLag == 0 || maxCorrelation <= 0) return 0;

            return (int)Math.Round(60.0 * frameRate / RefineBeatPeriod(smoothedOdf, bestLag));
        }

        /// <summary>
        /// Refines a whole-frame beat period to a fractional one. The period is
        /// first interpolated at its own peak, then again at the peaks near 2, 4
        /// and 8 periods, where a fraction of a frame is a 2, 4 and 8 times smaller
        /// share of the period. Stops before a multiple overlaps less than half of
        /// the onset function.
        /// </summary>
        private static double RefineBeatPeriod(float[] smoothedOdf, int lag)
        {
            double period = PeakLag(smoothedOdf, lag);

            foreach (int multiple in new[] { 2, 4, 8 })
            {
                int centre = (int)Math.Round(period * multiple);
                if (centre + 2 > smoothedOdf.Length / 2) break;

                // The estimate is within a frame of the peak at this multiple
                int peak = centre;
                double best = MeanProduct(smoothedOdf, centre);
                foreach (int candidate in new[] { centre - 1, centre + 1 })
                {
                    double value = MeanProduct(smoothedOdf, candidate);
                    if (value > best)
                    {
                        best = value;
                        peak = candidate;
                    }
                }

                period = PeakLag(smoothedOdf, peak) / multiple;
            }

            return period;
        }

        /// <summary>
        /// Fits a parabola through the correlation at <paramref name="lag"/> and its
        /// two neighbours and returns the lag at its peak, within half a frame of
        /// <paramref name="lag"/>, or <paramref name="lag"/> if the three points
        /// don't form a peak.
        /// </summary>
        private static double PeakLag(float[] smoothedOdf, int lag)
        {
            if (lag < 2 || lag + 1 >= smoothedOdf.Length) return lag;

            double before = MeanProduct(smoothedOdf, lag - 1);
            double at = MeanProduct(smoothedOdf, lag);
            double after = MeanProduct(smoothedOdf, lag + 1);

            double curvature = before - 2 * at + after;
            if (curvature >= 0) return lag;

            double offset = 0.5 * (before - after) / curvature;
            return lag + Math.Clamp(offset, -0.5, 0.5);
        }

        /// <summary>
        /// Autocorrelation at <paramref name="lag"/> divided by the number of
        /// overlapping frames, so longer lags aren't penalised for overlapping less.
        /// </summary>
        private static double MeanProduct(float[] smoothedOdf, int lag)
        {
            int count = smoothedOdf.Length - lag;
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += smoothedOdf[i] * smoothedOdf[i + lag];
            }
            return sum / count;
        }
    }
}
