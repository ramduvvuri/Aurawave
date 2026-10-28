# Changelog

## [1.0.0] - 2026-10-28
### Added
- Native per-pixel transparent WPF & Win32 layered Waveform overlay window (`WS_EX_LAYERED`, `WS_EX_TRANSPARENT`, `WS_EX_TOPMOST`).
- Independent floating Now Playing companion widget with rotating vinyl disc animation, track metadata, progress timeline, transport buttons, and real Windows WASAPI endpoint master volume slider.
- Zero-driver WASAPI Loopback real-time audio capture engine (`IMMDeviceEnumerator`, `IAudioClient`, `IAudioCaptureClient`) with automatic endpoint hot-swap recovery.
- Real-time Radix-2 Cooley-Tukey FFT DSP pipeline with Hann windowing, logarithmic frequency band mapping, RMS/Peak/Bass/Mid/Treble energy calculation, beat detection, and attack/release hysteresis state machine.
- 9 distinct audio visualizers: Liquid Spectrum, Aurora Wave, Neon Particles, Liquid Lava, Minimal Pulse, Retro Equalizer, Ghost / Monochrome, Radial Spectrum, and Synthwave.
- 8 curated color themes: Arctic Pulse, Violet Ether, Aurora, Afterglow, Ultraviolet, Midnight, Monochrome, and Custom.
- Global Windows hotkeys (`Alt+C`, `Alt+A`, `Alt+S`, `Alt+M`, `Alt+W`, `Alt+P`, `Ctrl+Alt+E`, `Ctrl+Alt+Plus`, `Ctrl+Alt+Minus`, `Alt+Q`).
- Full 8-section maximizable Settings window with live preview and atomic JSON persistence in `%LocalAppData%\AuraWave\config.json`.