using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AuraWave.Configuration;
using AuraWave.DSP;
using AuraWave.Models;
using AuraWave.Utilities;

namespace AuraWave.Audio
{
    public sealed class WasapiLoopbackEngine : IDisposable
    {
        private readonly ConfigurationService _configService;
        private readonly DspAnalyzer _dsp;
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private readonly float[] _ringBuffer = new float[2048];
        private int _ringWritePos;

        public string CurrentDeviceName { get; private set; } = "Default Windows Output";
        public string CaptureStatusText { get; private set; } = "Initializing";
        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
        public AudioAnalysisFrame LatestFrame { get; private set; } = new AudioAnalysisFrame();

        public event EventHandler<AudioAnalysisFrame>? FrameAnalyzed;
        public event EventHandler<string>? StatusChanged;

        public WasapiLoopbackEngine(ConfigurationService configService, DspAnalyzer dsp)
        {
            _configService = configService;
            _dsp = dsp;
        }

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _captureTask?.Wait(400);
            }
            catch { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                UpdateStatus("Stopped");
            }
        }

        public float GetMasterVolumeScalar()
        {
            try
            {
                var ep = GetEndpointVolumeInterface();
                if (ep != null)
                {
                    ep.GetMasterVolumeLevelScalar(out float level);
                    Marshal.ReleaseComObject(ep);
                    return Math.Clamp(level, 0f, 1f);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Failed to read master volume scalar: " + ex.Message);
            }
            return 0.75f;
        }

        public bool SetMasterVolumeScalar(float level)
        {
            try
            {
                var ep = GetEndpointVolumeInterface();
                if (ep != null)
                {
                    Guid ctx = Guid.Empty;
                    ep.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref ctx);
                    Marshal.ReleaseComObject(ep);
                    return true;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Failed to set master volume scalar: " + ex.Message);
            }
            return false;
        }

        public bool ToggleMute()
        {
            try
            {
                var ep = GetEndpointVolumeInterface();
                if (ep != null)
                {
                    ep.GetMute(out bool muted);
                    Guid ctx = Guid.Empty;
                    ep.SetMute(!muted, ref ctx);
                    Marshal.ReleaseComObject(ep);
                    return !muted;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Failed to toggle mute: " + ex.Message);
            }
            return false;
        }

        private void CaptureLoop(CancellationToken token)
        {
            const int AUDCLNT_SHAREMODE_SHARED = 0;
            const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
            Guid iidAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
            Guid iidCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

            while (!token.IsCancellationRequested)
            {
                IMMDeviceEnumerator? enumerator = null;
                IMMDevice? device = null;
                IAudioClient? audioClient = null;
                IAudioCaptureClient? captureClient = null;
                IntPtr mixFormatPtr = IntPtr.Zero;

                try
                {
                    Type? enumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                    if (enumType == null)
                    {
                        UpdateStatus("WASAPI unavailable on this OS");
                        Thread.Sleep(2000);
                        continue;
                    }

                    enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumType)!;
                    int hr = enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                    if (hr != 0 || device == null)
                    {
                        UpdateStatus("No active audio output endpoint found");
                        Thread.Sleep(1500);
                        continue;
                    }

                    device.GetId(out string devId);
                    CurrentDeviceName = string.IsNullOrWhiteSpace(devId)
                        ? "Default Audio Output"
                        : $"WASAPI Loopback ({devId.Substring(0, Math.Min(14, devId.Length))}...)";

                    hr = device.Activate(ref iidAudioClient, 0x17, IntPtr.Zero, out object audioClientObj);
                    audioClient = (IAudioClient)audioClientObj;

                    audioClient.GetMixFormat(out mixFormatPtr);
                    int channels = Marshal.ReadInt16(mixFormatPtr, 2);
                    int sampleRate = Marshal.ReadInt32(mixFormatPtr, 4);
                    int bitsPerSample = Marshal.ReadInt16(mixFormatPtr, 14);
                    int blockAlign = Marshal.ReadInt16(mixFormatPtr, 12);

                    long hnsRequestedDuration = 300000;
                    hr = audioClient.Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, hnsRequestedDuration, 0, mixFormatPtr, IntPtr.Zero);
                    if (hr != 0)
                    {
                        UpdateStatus($"WASAPI Initialize failed (0x{hr:X8})");
                        Thread.Sleep(1500);
                        continue;
                    }

                    hr = audioClient.GetService(ref iidCaptureClient, out object captureObj);
                    captureClient = (IAudioCaptureClient)captureObj;

                    audioClient.Start();
                    UpdateStatus("Capturing WASAPI Loopback");

                    int silentIterations = 0;
                    while (!token.IsCancellationRequested)
                    {
                        captureClient.GetNextPacketSize(out uint packetLength);
                        bool readAny = false;

                        while (packetLength > 0 && !token.IsCancellationRequested)
                        {
                            readAny = true;
                            captureClient.GetBuffer(out IntPtr dataPtr, out uint numFrames, out uint flags, out _, out _);
                            bool isSilentFlag = (flags & 0x2) != 0;

                            ExtractMonoSamples(dataPtr, (int)numFrames, channels, bitsPerSample, blockAlign, isSilentFlag);

                            captureClient.ReleaseBuffer(numFrames);
                            captureClient.GetNextPacketSize(out packetLength);
                        }

                        if (!readAny)
                        {
                            silentIterations++;
                            if (silentIterations > 2)
                            {
                                for (int i = 0; i < 256; i++)
                                {
                                    _ringBuffer[_ringWritePos] = 0f;
                                    _ringWritePos = (_ringWritePos + 1) & (_ringBuffer.Length - 1);
                                }
                            }
                        }
                        else
                        {
                            silentIterations = 0;
                        }

                        PublishAnalysisFrame(sampleRate);
                        Thread.Sleep(15);
                    }

                    audioClient.Stop();
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("WASAPI capture loop recovering: " + ex.Message);
                    UpdateStatus("Recovering audio endpoint...");
                    Thread.Sleep(1000);
                }
                finally
                {
                    if (mixFormatPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(mixFormatPtr);
                    if (captureClient != null) Marshal.ReleaseComObject(captureClient);
                    if (audioClient != null) Marshal.ReleaseComObject(audioClient);
                    if (device != null) Marshal.ReleaseComObject(device);
                    if (enumerator != null) Marshal.ReleaseComObject(enumerator);
                }
            }
        }

        private unsafe void ExtractMonoSamples(IntPtr dataPtr, int frames, int channels, int bitsPerSample, int blockAlign, bool silent)
        {
            if (frames <= 0 || dataPtr == IntPtr.Zero) return;
            byte* basePtr = (byte*)dataPtr.ToPointer();

            for (int f = 0; f < frames; f++)
            {
                float mono = 0f;
                if (!silent)
                {
                    byte* framePtr = basePtr + f * blockAlign;
                    if (bitsPerSample == 32)
                    {
                        float* fltPtr = (float*)framePtr;
                        for (int c = 0; c < channels; c++)
                            mono += fltPtr[c];
                        mono /= Math.Max(1, channels);
                    }
                    else if (bitsPerSample == 16)
                    {
                        short* pcmPtr = (short*)framePtr;
                        for (int c = 0; c < channels; c++)
                            mono += pcmPtr[c] / 32768f;
                        mono /= Math.Max(1, channels);
                    }
                }
                _ringBuffer[_ringWritePos] = Math.Clamp(mono, -1f, 1f);
                _ringWritePos = (_ringWritePos + 1) & (_ringBuffer.Length - 1);
            }
        }

        private void PublishAnalysisFrame(int sampleRate)
        {
            Span<float> ordered = stackalloc float[1024];
            int mask = _ringBuffer.Length - 1;
            int start = (_ringWritePos - 1024) & mask;
            for (int i = 0; i < 1024; i++)
            {
                ordered[i] = _ringBuffer[(start + i) & mask];
            }

            var cfg = _configService.Current;
            var frame = _dsp.ProcessSamples(
                ordered,
                sampleRate > 0 ? sampleRate : 48000,
                cfg.FrequencyBandCount,
                cfg.AudioSensitivity,
                cfg.SmoothingFactor,
                cfg.PeakDecayRate,
                cfg.BassResponseBoost,
                cfg.NoiseFloorThreshold,
                cfg.ActivityAttackThreshold,
                cfg.ActivityReleaseThreshold,
                cfg.AttackHoldMilliseconds,
                cfg.AudioStopInactivityDelaySeconds * 1000,
                DateTime.UtcNow);

            LatestFrame = frame;
            FrameAnalyzed?.Invoke(this, frame);
        }

        private IAudioEndpointVolume? GetEndpointVolumeInterface()
        {
            Type? enumType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            if (enumType == null) return null;
            var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumType)!;
            enumerator.GetDefaultAudioEndpoint(0, 0, out IMMDevice device);
            Guid iidEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
            device.Activate(ref iidEndpointVolume, 0x17, IntPtr.Zero, out object epObj);
            Marshal.ReleaseComObject(device);
            Marshal.ReleaseComObject(enumerator);
            return epObj as IAudioEndpointVolume;
        }

        private void UpdateStatus(string status)
        {
            CaptureStatusText = status;
            StatusChanged?.Invoke(this, status);
        }

        public void Dispose() => Stop();

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            [PreserveSig] int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            [PreserveSig] int Initialize(int ShareMode, int StreamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr AudioSessionGuid);
            [PreserveSig] int GetBufferSize(out uint pNumBufferFrames);
            [PreserveSig] int GetStreamLatency(out long phnsLatency);
            [PreserveSig] int GetCurrentPadding(out uint pNumPaddingFrames);
            [PreserveSig] int IsFormatSupported(int ShareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
            [PreserveSig] int GetMixFormat(out IntPtr ppDeviceFormat);
            [PreserveSig] int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr eventHandle);
            [PreserveSig] int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        }

        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
            [PreserveSig] int ReleaseBuffer(uint NumFramesRead);
            [PreserveSig] int GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int GetChannelCount(out uint pnChannelCount);
            [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
            [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }
    }
}