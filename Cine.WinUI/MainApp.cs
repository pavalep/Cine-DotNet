// ============================================================================
// MainApp.cs — WinForms UI for Cine Native Video Player
// Aligned with Python reference (window.py / Adw.ApplicationWindow layout)
//
// Python reference:
// - DEFAULT_WIDTH=1088, DEFAULT_HEIGHT=612
// - Header bar with transport + menu buttons
// - Video area (left) + Playlist sidebar (right)
// - Progress bar + time labels below video
// - Transport controls, volume, speed, subtitle/audio rows
// - Status bar at bottom
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Cine.Media;
using Cine.Media.Events;
using Cine.Media.Implementations;
using Cine.Media.Models;

namespace Cine.WinUI;

public static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

public class MainForm : Form
{
    // === Player ===
    private readonly MediaFoundationPlayer _player;

    // === Controls ===
    private Button btnOpen = null!;
    private Button btnPlayPause = null!;
    private Button btnStop = null!;
    private Button btnPrev = null!;
    private Button btnNext = null!;
    private Button btnMute = null!;
    private Button btnFullscreen = null!;
    private Button btnScreenshot = null!;
    private Button btnResetSpeed = null!;
    private TextBox txtPath = null!;
    private Panel playerPanel = null!;
    private TrackBar seekBar = null!;
    private TrackBar volumeBar = null!;
    private TrackBar speedBar = null!;
    private Label lblPosition = null!;
    private Label lblDuration = null!;
    private Label lblVolume = null!;
    private Label lblSpeed = null!;
    private Label lblSpeedValue = null!;
    private ComboBox cmbSubtitle = null!;
    private ComboBox cmbAudio = null!;
    private ListBox lstPlaylist = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel statusLabel = null!;

    private readonly System.Windows.Forms.Timer _updateTimer;

    // === Layout constants ===
    private const int WINDOW_W = 1088;
    private const int WINDOW_H = 612;
    private const int MARGIN = 10;
    private const int PANEL_RIGHT_W = 230;   // playlist sidebar width
    private const int VIDEO_PANEL_H = 480;

    public MainForm()
    {
        _player = new MediaFoundationPlayer();

        this.Text = "Cine";
        this.ClientSize = new Size(WINDOW_W, WINDOW_H);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(800, 500);
        this.KeyPreview = true;
        this.FormBorderStyle = FormBorderStyle.Sizable;

        _updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _updateTimer.Tick += OnUpdateTimerTick;

        _player.UseNativeRendering = true;
        this.HandleCreated += OnHandleCreated;
        playerPanel.Resize += OnPlayerPanelResize;

        InitializeUI();
        WirePlayerEvents();
        _player.Opened += OnPlayerOpened;
        _player.Closed += OnPlayerClosed;
    }

    // ======================
    //  UI INITIALIZATION
    // ======================

    private void InitializeUI()
    {
        int x = MARGIN;
        int y = MARGIN;
        int videoW = WINDOW_W - MARGIN * 2 - PANEL_RIGHT_W - 8;  // gap between video + playlist

        // --- VIDEO PANEL (native D3D11 rendering surface) ---
        playerPanel = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(videoW, VIDEO_PANEL_H),
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        this.Controls.Add(playerPanel);

        // --- PLAYLIST PANEL (right sidebar) ---
        int playlistX = x + videoW + 8;
        var lblPlaylist = new Label
        {
            Text = "Playlist",
            Location = new Point(playlistX, y),
            Size = new Size(PANEL_RIGHT_W, 22),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        this.Controls.Add(lblPlaylist);

        lstPlaylist = new ListBox
        {
            Location = new Point(playlistX, y + 24),
            Size = new Size(PANEL_RIGHT_W, VIDEO_PANEL_H - 24 + 2),
            BorderStyle = BorderStyle.FixedSingle
        };
        lstPlaylist.SelectedIndexChanged += OnPlaylistItemSelected;
        lstPlaylist.KeyDown += OnPlaylistKeyDown;
        lstPlaylist.DoubleClick += OnPlaylistDoubleClick;
        this.Controls.Add(lstPlaylist);

        y += VIDEO_PANEL_H + 10;

        // --- PATH BAR: Open button + file path + Screenshot ---
        btnOpen = new Button
        {
            Text = "Open",
            Location = new Point(x, y),
            Size = new Size(70, 30)
        };
        btnOpen.Click += (s, e) => OpenVideo();
        this.Controls.Add(btnOpen);

        txtPath = new TextBox
        {
            Location = new Point(x + 76, y),
            Size = new Size(videoW - 160, 30),
            ReadOnly = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        this.Controls.Add(txtPath);

        btnScreenshot = new Button
        {
            Text = "Screenshot",
            Location = new Point(x + videoW - 78, y),
            Size = new Size(78, 30)
        };
        btnScreenshot.Click += (s, e) => Screenshot();
        this.Controls.Add(btnScreenshot);

        y += 34;

        // --- SEEK BAR + TIME LABELS ---
        int seekBarW = WINDOW_W - MARGIN * 2 - PANEL_RIGHT_W - 8;

        seekBar = new TrackBar
        {
            Location = new Point(x, y + 8),
            Size = new Size(seekBarW - 150, 28),
            Minimum = 0,
            Maximum = 1000,    // higher resolution than default 100
            TickStyle = (TickStyle)0,  // NoTicks
            Enabled = false
        };
        seekBar.Scroll += OnSeekBarScroll;
        this.Controls.Add(seekBar);

        lblPosition = new Label
        {
            Text = "0:00:00",
            Location = new Point(x, y + 2),
            Size = new Size(60, 16),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 8.5f)
        };
        this.Controls.Add(lblPosition);

        lblDuration = new Label
        {
            Text = "0:00:00",
            Location = new Point(x + seekBarW - 148, y + 2),
            Size = new Size(60, 16),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Consolas", 8.5f)
        };
        this.Controls.Add(lblDuration);

        y += 38;

        // --- TRANSPORT CONTROLS ---
        btnPlayPause = new Button
        {
            Text = "▶ Play",
            Location = new Point(x, y),
            Size = new Size(70, 30),
            Enabled = false
        };
        btnPlayPause.Click += (s, e) => TogglePlayPause();
        this.Controls.Add(btnPlayPause);

        btnStop = new Button
        {
            Text = "Stop",
            Location = new Point(x + 74, y),
            Size = new Size(60, 30),
            Enabled = false
        };
        btnStop.Click += (s, e) => _player.Stop();
        this.Controls.Add(btnStop);

        btnPrev = new Button
        {
            Text = "◀ Prev",
            Location = new Point(x + 138, y),
            Size = new Size(70, 30),
            Enabled = false
        };
        btnPrev.Click += (s, e) => _player.PreviousPlaylistItem();
        this.Controls.Add(btnPrev);

        btnNext = new Button
        {
            Text = "Next ▶",
            Location = new Point(x + 212, y),
            Size = new Size(70, 30),
            Enabled = false
        };
        btnNext.Click += (s, e) => _player.NextPlaylistItem();
        this.Controls.Add(btnNext);

        // Volume group (right-aligned in transport row)
        int volX = x + videoW - 215;

        var lblVolIcon = new Label
        {
            Text = "🔊",
            Location = new Point(volX, y + 6),
            Size = new Size(22, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(lblVolIcon);

        volumeBar = new TrackBar
        {
            Location = new Point(volX + 22, y + 4),
            Size = new Size(100, 30),
            Minimum = 0,
            Maximum = 150,
            Value = 50,
            TickStyle = (TickStyle)0,  // NoTicks
            SmallChange = 5,
            LargeChange = 10
        };
        volumeBar.Scroll += OnVolumeBarScroll;
        this.Controls.Add(volumeBar);

        lblVolume = new Label
        {
            Text = "50",
            Location = new Point(volX + 126, y + 6),
            Size = new Size(35, 20),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5f)
        };
        this.Controls.Add(lblVolume);

        btnMute = new Button
        {
            Text = "🔇",
            Location = new Point(volX + 164, y + 2),
            Size = new Size(30, 30)
        };
        btnMute.Click += (s, e) => _player.Mute(!_player.IsMuted);
        this.Controls.Add(btnMute);

        btnFullscreen = new Button
        {
            Text = "⛶",
            Location = new Point(volX + 197, y + 2),
            Size = new Size(30, 30)
        };
        btnFullscreen.Click += (s, e) => _player.ToggleFullscreen();
        this.Controls.Add(btnFullscreen);

        y += 36;

        // --- SPEED ROW ---
        lblSpeed = new Label
        {
            Text = "Speed:",
            Location = new Point(x, y + 6),
            Size = new Size(48, 20),
            TextAlign = ContentAlignment.MiddleRight
        };
        this.Controls.Add(lblSpeed);

        speedBar = new TrackBar
        {
            Location = new Point(x + 52, y + 3),
            Size = new Size(140, 30),
            Minimum = 25,
            Maximum = 400,
            Value = 100,
            TickStyle = (TickStyle)0,  // NoTicks
            SmallChange = 10,
            LargeChange = 25
        };
        speedBar.Scroll += OnSpeedBarScroll;
        this.Controls.Add(speedBar);

        lblSpeedValue = new Label
        {
            Text = "1.00x",
            Location = new Point(x + 196, y + 6),
            Size = new Size(50, 20),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 8.5f)
        };
        this.Controls.Add(lblSpeedValue);

        btnResetSpeed = new Button
        {
            Text = "Reset",
            Location = new Point(x + 250, y + 2),
            Size = new Size(58, 26)
        };
        btnResetSpeed.Click += (s, e) => _player.ResetSpeed();
        this.Controls.Add(btnResetSpeed);

        y += 34;

        // --- SUBTITLE + AUDIO ROW ---
        var lblSubIcon = new Label
        {
            Text = "Sub:",
            Location = new Point(x, y + 6),
            Size = new Size(34, 20),
            TextAlign = ContentAlignment.MiddleRight
        };
        this.Controls.Add(lblSubIcon);

        cmbSubtitle = new ComboBox
        {
            Location = new Point(x + 36, y + 3),
            Size = new Size(150, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbSubtitle.SelectedIndexChanged += OnSubtitleChanged;
        this.Controls.Add(cmbSubtitle);

        var lblAudioIcon = new Label
        {
            Text = "Audio:",
            Location = new Point(x + 196, y + 6),
            Size = new Size(46, 20),
            TextAlign = ContentAlignment.MiddleRight
        };
        this.Controls.Add(lblAudioIcon);

        cmbAudio = new ComboBox
        {
            Location = new Point(x + 244, y + 3),
            Size = new Size(110, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbAudio.SelectedIndexChanged += OnAudioChanged;
        this.Controls.Add(cmbAudio);

        y += 32;

        // --- LOOP ROW ---
        var lblLoop = new Label
        {
            Text = "Loop:",
            Location = new Point(x, y + 6),
            Size = new Size(40, 20),
            TextAlign = ContentAlignment.MiddleRight
        };
        this.Controls.Add(lblLoop);

        var btnLoopFile = new Button
        {
            Text = "File",
            Location = new Point(x + 42, y + 2),
            Size = new Size(50, 26)
        };
        btnLoopFile.Click += (s, e) => _player.ToggleLoopFile();
        this.Controls.Add(btnLoopFile);

        var btnLoopPlaylist = new Button
        {
            Text = "List",
            Location = new Point(x + 95, y + 2),
            Size = new Size(50, 26)
        };
        btnLoopPlaylist.Click += (s, e) => _player.ToggleLoopPlaylist();
        this.Controls.Add(btnLoopPlaylist);

        // --- STATUS STRIP ---
        statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = true
        };
        statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready | Space=Play/Pause  M=Mute  F=Fullscreen  ←→=Seek  ↑↓=Volume  [/]=Speed  L=Loop  S=Screenshot"
        };
        statusStrip.Items.Add(statusLabel);
        this.Controls.Add(statusStrip);
    }

    // ======================
    //  PLAYER EVENT WIRING
    // ======================

    private void WirePlayerEvents()
    {
        _player.FileLoaded += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => OnFileLoaded(e)); return; }
            OnFileLoaded(e);
        };

        _player.EndFile += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => OnFileEnded()); return; }
            OnFileEnded();
        };

        _player.PositionChanged += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => OnPositionChanged(e)); return; }
            OnPositionChanged(e);
        };

        _player.DurationChanged += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => OnDurationChanged(e)); return; }
            OnDurationChanged(e);
        };

        _player.VolumeChanged += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => OnVolumeChanged(e)); return; }
            OnVolumeChanged(e);
        };

        _player.PlaybackResumed += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => UpdatePlayPauseButton(true)); return; }
            UpdatePlayPauseButton(true);
        };

        _player.PlaybackPaused += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => UpdatePlayPauseButton(false)); return; }
            UpdatePlayPauseButton(false);
        };

        _player.PlaybackStopped += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => OnFileEnded()); return; }
            OnFileEnded();
        };

        _player.TrackListChanged += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => UpdateTrackLists()); return; }
            UpdateTrackLists();
        };

        _player.PlaylistChanged += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => UpdatePlaylistUI()); return; }
            UpdatePlaylistUI();
        };
    }

    // ======================
    //  NATIVE RENDERER EVENTS
    // ======================

    private void OnHandleCreated(object? sender, EventArgs e)
    {
        _player.InitializeRenderer(playerPanel.Handle);
    }

    private void OnPlayerPanelResize(object? sender, EventArgs e)
    {
        if (playerPanel.Width > 0 && playerPanel.Height > 0)
            _player.NotifyResize(playerPanel.Width, playerPanel.Height);
    }

    // ======================
    //  EVENT HANDLERS
    // ======================

    private void OnFileLoaded(MediaEventArgs e)
    {
        btnPlayPause.Enabled = true;
        btnStop.Enabled = true;
        btnPrev.Enabled = true;
        btnNext.Enabled = true;
        seekBar.Enabled = true;
        UpdatePlaylistUI();
        statusLabel.Text = $"Now playing: {Path.GetFileName(e.FilePath)}";
        _updateTimer.Start();
    }

    private void OnFileEnded()
    {
        _updateTimer.Stop();
        btnPlayPause.Text = "Play";
        btnPlayPause.Enabled = false;
        btnStop.Enabled = false;
        btnPrev.Enabled = false;
        btnNext.Enabled = false;
        seekBar.Enabled = false;
        seekBar.Value = 0;
        lblPosition.Text = "0:00:00";
        lblDuration.Text = "0:00:00";
        statusLabel.Text = "Stopped";
    }

    private void OnPlayerOpened(object? sender, EventArgs e)
    {
        _updateTimer.Start();
    }

    private void OnPlayerClosed(object? sender, EventArgs e)
    {
        _updateTimer.Stop();
    }

    private void OnPositionChanged(PositionChangedEventArgs e)
    {
        var total = e.Position.TotalSeconds;
        var dur = _player.Duration.TotalSeconds;

        lblPosition.Text = FormatTime(e.Position);

        if (dur > 0)
        {
            seekBar.Value = (int)((total / dur) * 1000);
        }
    }

    private void OnDurationChanged(DurationChangedEventArgs e)
    {
        lblDuration.Text = FormatTime(e.Duration);
    }

    private void OnVolumeChanged(VolumeChangedEventArgs e)
    {
        volumeBar.Value = Math.Min(volumeBar.Maximum, (int)e.Volume);
        lblVolume.Text = $"{(int)e.Volume}";
    }

    private void UpdatePlayPauseButton(bool isPlaying)
    {
        btnPlayPause.Text = isPlaying ? "⏸ Pause" : "▶ Play";
    }

    // ======================
    //  TIMER
    // ======================

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        // Timer keeps the UI responsive; position is driven by
        // MediaFoundationPlayer.PositionChanged events.
    }

    // ======================
    //  UI HANDLERS
    // ======================

    private void TogglePlayPause()
    {
        if (_player.State == PlaybackState.Playing)
            _player.Pause();
        else
            _player.Play();
    }

    private void OnSeekBarScroll(object? sender, EventArgs e)
    {
        if (_player.Duration.TotalSeconds <= 0)
            return;

        var fraction = seekBar.Value / 1000.0;
        var target = TimeSpan.FromSeconds(fraction * _player.Duration.TotalSeconds);
        _player.Seek(target);
    }

    private void OnVolumeBarScroll(object? sender, EventArgs e)
    {
        _player.SetVolume(volumeBar.Value);
    }

    private void OnSpeedBarScroll(object? sender, EventArgs e)
    {
        double speed = speedBar.Value / 100.0;
        _player.SetSpeed(speed);
        lblSpeedValue.Text = $"{speed:F2}x";
    }

    private void OnSubtitleChanged(object? sender, EventArgs e)
    {
        if (cmbSubtitle.SelectedIndex >= 0 && cmbSubtitle.SelectedIndex < _player.SubtitleSources.Length)
        {
            _player.CurrentSubtitleTrack = cmbSubtitle.SelectedIndex;
        }
    }

    private void OnAudioChanged(object? sender, EventArgs e)
    {
        if (cmbAudio.SelectedIndex >= 0)
        {
            _player.AudioTrack = cmbAudio.SelectedIndex;
        }
    }

    private void OnPlaylistItemSelected(object? sender, EventArgs e)
    {
        if (lstPlaylist.SelectedIndex >= 0 && lstPlaylist.SelectedIndex != _player.PlaylistPosition)
        {
            _player.SetPlaylistPosition(lstPlaylist.SelectedIndex);
        }
    }

    private void OnPlaylistDoubleClick(object? sender, EventArgs e)
    {
        if (lstPlaylist.SelectedIndex >= 0)
        {
            _player.SetPlaylistPosition(lstPlaylist.SelectedIndex);
        }
    }

    private void OnPlaylistKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && lstPlaylist.SelectedIndex >= 0)
        {
            _player.SetPlaylistPosition(lstPlaylist.SelectedIndex);
            e.Handled = true;
        }
    }

    private void OpenVideo()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All Files|*.*",
            Title = "Select Video File",
            Multiselect = true
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            txtPath.Text = openFileDialog.FileName;

            foreach (var file in openFileDialog.FileNames)
            {
                _player.AddToPlaylist(file);
            }

            _player.SetPlaylistPosition(0);
            UpdatePlaylistUI();
        }
    }

    private void Screenshot()
    {
        try
        {
            _player.ScreenshotWithSubtitles();
            MessageBox.Show("Screenshot saved!", "Cine", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Screenshot failed: {ex.Message}", "Cine", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ======================
    //  KEYBOARD SHORTCUTS (matching Python INTERNAL_BINDINGS)
    // ======================

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
        {
            if (this.FormBorderStyle == FormBorderStyle.None)
            {
                _player.ToggleFullscreen();
            }
            else
            {
                _player.Stop();
            }
            e.Handled = true;
            return;
        }

        // --- Playback Controls ---
        if (e.KeyCode == Keys.Space)
        {
            TogglePlayPause();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F || e.KeyCode == Keys.F11)
        {
            _player.ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.M)
        {
            _player.Mute(!_player.IsMuted);
            e.Handled = true;
        }

        // --- Seeking (matching Python: left/right arrows) ---
        else if (e.KeyCode == Keys.Right)
        {
            _player.SeekForward(GetSeekStep(e));
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.NumPad5 || e.KeyCode == Keys.Left)
        {
            _player.SeekBackward(GetSeekStep(e));
            e.Handled = true;
        }

        // --- Volume (matching Python: up/down arrows) ---
        else if (e.KeyCode == Keys.Up)
        {
            _player.IncreaseVolume();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down)
        {
            _player.DecreaseVolume();
            e.Handled = true;
        }

        // --- Playback Speed — using / and [ for decrease, ] and . for increase ---
        else if (e.KeyCode == Keys.OemPeriod || e.KeyCode == Keys.Oem6)
        {
            _player.IncreaseSpeed();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Oemcomma || e.KeyCode == Keys.Oem4)
        {
            _player.DecreaseSpeed();
            e.Handled = true;
        }

        // --- Chapter Navigation (matching Python: p / P) ---
        else if (e.KeyCode == Keys.P && !e.Control && !e.Alt && !e.Shift)
        {
            _player.NextChapter();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.P && e.Shift)
        {
            _player.PreviousChapter();
            e.Handled = true;
        }

        // --- Playlist Navigation ---
        // PageDown / \ key (next), PageUp / | key (prev)
        else if (e.KeyCode == Keys.PageDown)
        {
            _player.NextPlaylistItem();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.PageUp)
        {
            _player.PreviousPlaylistItem();
            e.Handled = true;
        }

        // --- Subtitle Delay ---
        else if (e.KeyCode == Keys.OemPeriod)
        {
            _player.IncreaseSubtitleDelay();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Oemcomma)
        {
            _player.DecreaseSubtitleDelay();
            e.Handled = true;
        }

        // --- Loop mode (matching Python: L) ---
        else if (e.KeyCode == Keys.L && !e.Control && !e.Alt)
        {
            _player.ToggleLoopFile();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.L && e.Control)
        {
            _player.ToggleLoopPlaylist();
            e.Handled = true;
        }

        // --- Screenshot (matching Python: s / S) ---
        else if (e.KeyCode == Keys.S && !e.Control && !e.Alt)
        {
            Screenshot();
            e.Handled = true;
        }

        // --- Reset Speed (matching Python: BS / Backspace) ---
        else if (e.KeyCode == Keys.Back)
        {
            _player.ResetSpeed();
            e.Handled = true;
        }

        // --- Track switching (matching Python: #) ---
        else if (e.KeyCode == Keys.D3 && e.Shift)
        {
            _player.CycleSubtitleTrack();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Gets seek step based on modifier keys (matching Python's seek logic)
    /// Small seek = 5s (normal), Large seek = 60s (with shift/ctrl)
    /// </summary>
    private static double GetSeekStep(KeyEventArgs e)
    {
        if (e.Shift || e.Control)
            return 60.0;      // Large jump (matching Python's big step)
        return 5.0;           // Small jump (matching Python's small step)
    }

    // ======================
    //  UI UPDATE HELPERS
    // ======================

    private void UpdateTrackLists()
    {
        // Update subtitle combo
        var subs = _player.SubtitleSources;
        if (cmbSubtitle.Tag == null || !ReferenceEquals(cmbSubtitle.Tag, subs))
        {
            cmbSubtitle.Items.Clear();
            foreach (var sub in subs)
            {
                cmbSubtitle.Items.Add(sub);
            }
            cmbSubtitle.Tag = subs;
        }
        cmbSubtitle.SelectedIndex = Math.Max(0, _player.CurrentSubtitleTrack);

        // Update audio combo
        cmbAudio.Items.Clear();
        for (int i = 0; i < _player.AudioTrack; i++)
        {
            cmbAudio.Items.Add($"Track {i + 1}");
        }
    }

    private void UpdatePlaylistUI()
    {
        var playlist = _player.Playlist;
        int selectedPos = _player.PlaylistPosition;

        lstPlaylist.BeginUpdate();
        lstPlaylist.Items.Clear();
        for (int i = 0; i < playlist.Length; i++)
        {
            var item = playlist[i];
            var display = Path.GetFileName(item);
            if (i == selectedPos)
                display = "▶ " + display;
            lstPlaylist.Items.Add(display);
        }
        if (selectedPos >= 0 && selectedPos < lstPlaylist.Items.Count)
            lstPlaylist.SelectedIndex = selectedPos;
        lstPlaylist.EndUpdate();
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.Hours > 0
            ? time.ToString(@"hh\:mm\:ss")
            : time.ToString(@"mm\:ss");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _updateTimer.Stop();
        _player.Dispose();
        base.OnFormClosing(e);
    }
}