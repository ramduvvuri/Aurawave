using System;
using AuraWave.Models;

namespace AuraWave.DSP
{
    public sealed class DspAnalyzer
    {
        private readonly int _fftSize;
        private readonly float[] _hannWindow;
        private readonly float[] _fftReal;
        private readonly float[] _fftImag;
        private readonly float[] _magnitudeDb;
        private float[] _smoothedBands;
        private float[] _peakBands;
        private readonly float[] _waveformDownsampled;

        private bool _isAudioActive;
        private DateTime _aboveAttackSinceUtc = DateTime.MinValue;
        private DateTime _belowReleaseSinceUtc = DateTime.MinValue;
        private float _rollingBassAvg;

        public int FftSize => _fftSize;

        public DspAnalyzer(int fftSize = 1024, int initialBandCount = 48)
        {
            if (fftSize <= 0 || (fftSize & (fftSize - 1)) != 0)
                throw new ArgumentException("FFT size must be a power of two.", nameof(fftSize));

            _fftSize = fftSize;
            _hannWindow = new float[fftSize];
            _fftReal = new float[fftSize];
            _fftImag = new float[fftSize];
            _magnitudeDb = new float[fftSize / 2];
            _smoothedBands = new float[initialBandCount];
            _peakBands = new float[initialBandCount];
            _waveformDownsampled = new float[128];

            for (int i = 0; i < fftSize; i++)
            {
                _hannWindow[i] = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (fftSize - 1)));
            }
        }

        public AudioAnalysisFrame ProcessSamples(
            ReadOnlySpan<float> monoSamples,
            int sampleRate,
            int bandCount,
            double sensitivity,
            double smoothing,
            double peakDecayRate,
            double bassBoost,
            double noiseFloor,
            double attackThreshold,
            double releaseThreshold,
            int attackHoldMs,
            int releaseHoldMs,
            DateTime nowUtc)
        {
            bandCount = Math.Clamp(bandCount, 16, 128);
            if (_smoothedBands.Length != bandCount)
            {
                _smoothedBands = new float[bandCount];
                _peakBands = new float[bandCount];
            }

            int n = Math.Min(monoSamples.Length, _fftSize);
            double sumSquares = 0.0;
            float peak = 0.0f;

            Array.Clear(_fftReal, 0, _fftSize);
            Array.Clear(_fftImag, 0, _fftSize);

            for (int i = 0; i < n; i++)
            {
                float s = monoSamples[i];
                float abs = Math.Abs(s);
                if (abs > peak) peak = abs;
                sumSquares += s * s;
                _fftReal[i] = s * _hannWindow[i];
            }

            float rms = n > 0 ? (float)Math.Sqrt(sumSquares / n) : 0.0f;
            if (rms < (float)noiseFloor)
            {
                rms = 0.0f;
            }

            for (int i = 0; i < _waveformDownsampled.Length; i++)
            {
                int idx = n > 0 ? (i * n) / _waveformDownsampled.Length : 0;
                float raw = n > 0 ? monoSamples[Math.Clamp(idx, 0, n - 1)] : 0f;
                _waveformDownsampled[i] = Math.Clamp(raw * (float)sensitivity, -1.0f, 1.0f);
            }

            ComputeFftInPlace(_fftReal, _fftImag);

            int halfFft = _fftSize / 2;
            float invHalf = 2.0f / _fftSize;
            for (int k = 0; k < halfFft; k++)
            {
                float re = _fftReal[k];
                float im = _fftImag[k];
                float mag = (float)Math.Sqrt(re * re + im * im) * invHalf;
                if (mag < (float)noiseFloor * 0.25f)
                {
                    _magnitudeDb[k] = 0.0f;
                }
                else
                {
                    float db = 20.0f * (float)Math.Log10(mag + 1e-7f);
                    float norm = (db + 65.0f) / 65.0f;
                    _magnitudeDb[k] = Math.Clamp(norm, 0.0f, 1.0f);
                }
            }

            double minFreq = 20.0;
            double maxFreq = Math.Min(18000.0, sampleRate * 0.48);
            double logMin = Math.Log10(minFreq);
            double logMax = Math.Log10(Math.Max(minFreq + 10.0, maxFreq));
            double binHz = (double)sampleRate / _fftSize;

            float smooth = (float)Math.Clamp(smoothing, 0.05, 0.95);
            float sens = (float)Math.Clamp(sensitivity, 0.2, 5.0);
            float decayStep = (float)Math.Clamp(peakDecayRate, 0.02, 1.0) * 0.04f;

            float bassSum = 0f;
            int bassCount = 0;
            float midSum = 0f;
            int midCount = 0;
            float trebleSum = 0f;
            int trebleCount = 0;

            for (int b = 0; b < bandCount; b++)
            {
                double t0 = (double)b / bandCount;
                double t1 = (double)(b + 1) / bandCount;
                double f0 = Math.Pow(10.0, logMin + (logMax - logMin) * t0);
                double f1 = Math.Pow(10.0, logMin + (logMax - logMin) * t1);

                int k0 = Math.Clamp((int)Math.Floor(f0 / binHz), 1, halfFft - 1);
                int k1 = Math.Clamp((int)Math.Ceiling(f1 / binHz), k0, halfFft - 1);

                float bandVal = 0f;
                for (int k = k0; k <= k1; k++)
                {
                    if (_magnitudeDb[k] > bandVal)
                        bandVal = _magnitudeDb[k];
                }

                double centerFreq = 0.5 * (f0 + f1);
                if (centerFreq < 250.0)
                {
                    bandVal *= (float)bassBoost;
                    bassSum += bandVal;
                    bassCount++;
                }
                else if (centerFreq < 4000.0)
                {
                    midSum += bandVal;
                    midCount++;
                }
                else
                {
                    trebleSum += bandVal;
                    trebleCount++;
                }

                float scaled = Math.Clamp(bandVal * sens, 0.0f, 1.0f);
                if (rms == 0.0f) scaled = 0.0f;

                float prev = _smoothedBands[b];
                float alpha = scaled > prev ? smooth * 0.55f : smooth;
                float next = prev * alpha + scaled * (1.0f - alpha);
                if (next < 0.002f) next = 0.0f;
                _smoothedBands[b] = Math.Clamp(next, 0.0f, 1.0f);

                if (_smoothedBands[b] >= _peakBands[b])
                {
                    _peakBands[b] = _smoothedBands[b];
                }
                else
                {
                    _peakBands[b] = Math.Max(0.0f, _peakBands[b] - decayStep);
                }
            }

            float bassEnergy = bassCount > 0 ? Math.Clamp(bassSum / bassCount, 0f, 1f) : 0f;
            float midEnergy = midCount > 0 ? Math.Clamp(midSum / midCount, 0f, 1f) : 0f;
            float trebleEnergy = trebleCount > 0 ? Math.Clamp(trebleSum / trebleCount, 0f, 1f) : 0f;

            bool isBeat = bassEnergy > 0.35f && bassEnergy > (_rollingBassAvg * 1.45f + 0.08f);
            _rollingBassAvg = _rollingBassAvg * 0.92f + bassEnergy * 0.08f;

            UpdateActivityState(rms, attackThreshold, releaseThreshold, attackHoldMs, releaseHoldMs, nowUtc);

            var outBands = new float[bandCount];
            var outPeaks = new float[bandCount];
            var outWave = new float[_waveformDownsampled.Length];
            Array.Copy(_smoothedBands, outBands, bandCount);
            Array.Copy(_peakBands, outPeaks, bandCount);
            Array.Copy(_waveformDownsampled, outWave, _waveformDownsampled.Length);

            return new AudioAnalysisFrame
            {
                Rms = Math.Clamp(rms * sens, 0f, 1f),
                Peak = Math.Clamp(peak * sens, 0f, 1f),
                BassEnergy = bassEnergy,
                MidrangeEnergy = midEnergy,
                TrebleEnergy = trebleEnergy,
                FrequencyBands = outBands,
                PeakHoldBands = outPeaks,
                WaveformSamples = outWave,
                IsAudioActive = _isAudioActive,
                IsBeatDetected = isBeat,
                TimestampUtc = nowUtc
            };
        }

        private void UpdateActivityState(
            float rawRms,
            double attackThreshold,
            double releaseThreshold,
            int attackHoldMs,
            int releaseHoldMs,
            DateTime nowUtc)
        {
            if (!_isAudioActive)
            {
                if (rawRms >= attackThreshold)
                {
                    if (_aboveAttackSinceUtc == DateTime.MinValue)
                        _aboveAttackSinceUtc = nowUtc;

                    if ((nowUtc - _aboveAttackSinceUtc).TotalMilliseconds >= attackHoldMs)
                    {
                        _isAudioActive = true;
                        _belowReleaseSinceUtc = DateTime.MinValue;
                    }
                }
                else
                {
                    _aboveAttackSinceUtc = DateTime.MinValue;
                }
            }
            else
            {
                if (rawRms <= releaseThreshold)
                {
                    if (_belowReleaseSinceUtc == DateTime.MinValue)
                        _belowReleaseSinceUtc = nowUtc;

                    if ((nowUtc - _belowReleaseSinceUtc).TotalMilliseconds >= releaseHoldMs)
                    {
                        _isAudioActive = false;
                        _aboveAttackSinceUtc = DateTime.MinValue;
                    }
                }
                else
                {
                    _belowReleaseSinceUtc = DateTime.MinValue;
                }
            }
        }

        public static void ComputeFftInPlace(float[] real, float[] imag)
        {
            int n = real.Length;
            int j = 0;
            for (int i = 0; i < n; i++)
            {
                if (i < j)
                {
                    (real[i], real[j]) = (real[j], real[i]);
                    (imag[i], imag[j]) = (imag[j], imag[i]);
                }
                int m = n >> 1;
                while (m >= 1 && j >= m)
                {
                    j -= m;
                    m >>= 1;
                }
                j += m;
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                int halfLen = len >> 1;
                double angle = -2.0 * Math.PI / len;
                float wLenRe = (float)Math.Cos(angle);
                float wLenIm = (float)Math.Sin(angle);

                for (int i = 0; i < n; i += len)
                {
                    float wRe = 1.0f;
                    float wIm = 0.0f;
                    for (int k = 0; k < halfLen; k++)
                    {
                        int evenIdx = i + k;
                        int oddIdx = i + k + halfLen;

                        float uRe = real[evenIdx];
                        float uIm = imag[evenIdx];
                        float vRe = real[oddIdx] * wRe - imag[oddIdx] * wIm;
                        float vIm = real[oddIdx] * wIm + imag[oddIdx] * wRe;

                        real[evenIdx] = uRe + vRe;
                        imag[evenIdx] = uIm + vIm;
                        real[oddIdx] = uRe - vRe;
                        imag[oddIdx] = uIm - vIm;

                        float nextWRe = wRe * wLenRe - wIm * wLenIm;
                        wIm = wRe * wLenIm + wIm * wLenRe;
                        wRe = nextWRe;
                    }
                }
            }
        }
    }
}