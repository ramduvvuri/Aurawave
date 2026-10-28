using System;
using System.Drawing;
using AuraWave.Configuration;
using AuraWave.Models;

namespace AuraWave.SystemTray
{
    public sealed class SystemTrayController : IDisposable
    {
        private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
        private readonly ConfigurationService _configService;
        private readonly Icon _trayIcon;

        public event EventHandler? ToggleWaveformRequested;
        public event EventHandler? ToggleNowPlayingRequested;
        public event EventHandler? ToggleClickThroughRequested;
        public event EventHandler? ToggleEditModeRequested;
        public event EventHandler? OpenSettingsRequested;
        public event EventHandler? ApplyChangesRequested;
        public event EventHandler? QuitRequested;

        public SystemTrayController(ConfigurationService configService)
        {
            _configService = configService;
            _trayIcon = CreateDynamicAuraWaveIcon();

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = _trayIcon,
                Text = "AuraWave â€” Desktop Audio Companion",
                Visible = true,
                ContextMenuStrip = BuildContextMenu()
            };

            _notifyIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private System.Windows.Forms.ContextMenuStrip BuildContextMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();

            menu.Items.Add("Show / Hide Waveform (Alt+W)", null, (_, _) => ToggleWaveformRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add("Show / Hide Now Playing (Alt+P)", null, (_, _) => ToggleNowPlayingRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            menu.Items.Add("Pin / Unpin Waveform", null, (_, _) =>
            {
                _configService.Current.WaveformPinned = !_configService.Current.WaveformPinned;
                _configService.Save();
                ApplyChangesRequested?.Invoke(this, EventArgs.Empty);
            });

            menu.Items.Add("Pin / Unpin Now Playing", null, (_, _) =>
            {
                _configService.Current.NowPlayingPinned = !_configService.Current.NowPlayingPinned;
                _configService.Save();
                ApplyChangesRequested?.Invoke(this, EventArgs.Empty);
            });

            menu.Items.Add("Toggle Click-Through (Alt+C)", null, (_, _) => ToggleClickThroughRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add("Enter / Exit Edit Mode (Ctrl+Alt+E)", null, (_, _) => ToggleEditModeRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var visMenu = new System.Windows.Forms.ToolStripMenuItem("Select Visualizer");
            foreach (VisualizerType vt in Enum.GetValues(typeof(VisualizerType)))
            {
                VisualizerType captured = vt;
                visMenu.DropDownItems.Add(vt.ToString(), null, (_, _) =>
                {
                    _configService.Current.SelectedVisualizer = captured;
                    _configService.Save();
                    ApplyChangesRequested?.Invoke(this, EventArgs.Empty);
                });
            }
            menu.Items.Add(visMenu);

            var themeMenu = new System.Windows.Forms.ToolStripMenuItem("Select Color Theme");
            foreach (ThemePreset tp in Enum.GetValues(typeof(ThemePreset)))
            {
                ThemePreset captured = tp;
                themeMenu.DropDownItems.Add(tp.ToString(), null, (_, _) =>
                {
                    _configService.Current.SelectedTheme = captured;
                    _configService.Save();
                    ApplyChangesRequested?.Invoke(this, EventArgs.Empty);
                });
            }
            menu.Items.Add(themeMenu);

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Open Settings Studio (Alt+S)", null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add("Quit AuraWave (Alt+Q)", null, (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty));

            return menu;
        }

        private static Icon CreateDynamicAuraWaveIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(255, 11, 17, 32));
                using var penCyan = new Pen(Color.FromArgb(255, 0, 242, 254), 3f);
                using var penViolet = new Pen(Color.FromArgb(255, 155, 81, 224), 3f);
                g.DrawLine(penCyan, 6, 22, 6, 12);
                g.DrawLine(penCyan, 12, 26, 12, 6);
                g.DrawLine(penViolet, 19, 24, 19, 8);
                g.DrawLine(penViolet, 25, 20, 25, 13);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIcon.Dispose();
        }
    }
}