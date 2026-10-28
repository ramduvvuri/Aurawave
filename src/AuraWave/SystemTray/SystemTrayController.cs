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

        public event EventHandler? ToggleNowPlayingRequested;
        public event EventHandler? ResetPositionRequested;
        public event EventHandler? TogglePinRequested;
        public event EventHandler? ToggleWaveformRequested;
        public event EventHandler? ToggleClickThroughRequested;
        public event EventHandler? ToggleEditModeRequested;
        public event EventHandler? OpenSettingsRequested;
        public event EventHandler? ApplyChangesRequested;
        public event EventHandler? QuitRequested;

        public SystemTrayController(ConfigurationService configService)
        {
            _configService = configService;
            _trayIcon = CreateDynamicEmeraldIcon();

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = _trayIcon,
                Text = "AuraWave — Emerald Floating Music Bar",
                Visible = true,
                ContextMenuStrip = BuildContextMenu()
            };

            _notifyIcon.DoubleClick += (_, _) => ToggleNowPlayingRequested?.Invoke(this, EventArgs.Empty);
        }

        private System.Windows.Forms.ContextMenuStrip BuildContextMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();

            menu.Items.Add("Show / Hide Emerald Music Bar (Alt+P)", null, (_, _) => ToggleNowPlayingRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add("Pin / Unpin Music Bar", null, (_, _) => TogglePinRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add("Reset Position to Bottom Center", null, (_, _) => ResetPositionRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            menu.Items.Add("Unlock / Toggle Click-Through (Alt+C)", null, (_, _) => ToggleClickThroughRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add("Enter / Exit Resize Mode (Ctrl+Alt+E)", null, (_, _) => ToggleEditModeRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add("Toggle Optional Standalone Waveform (Alt+W)", null, (_, _) => ToggleWaveformRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

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

        private static Icon CreateDynamicEmeraldIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(255, 7, 18, 15));
                using var penEmerald = new Pen(Color.FromArgb(255, 53, 214, 160), 3f);
                using var penBright = new Pen(Color.FromArgb(255, 66, 229, 176), 3f);
                g.DrawLine(penEmerald, 6, 22, 6, 12);
                g.DrawLine(penBright, 12, 26, 12, 6);
                g.DrawLine(penEmerald, 19, 24, 19, 8);
                g.DrawLine(penBright, 25, 20, 25, 13);
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