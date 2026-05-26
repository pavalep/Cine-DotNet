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
using System.Linq;
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
    private MenuStrip _menuStrip = null!;
    private ToolStripMenuItem _menuFile = null!;
    private ToolStripMenuItem _menuPlayback = null!;
    private ToolStripMenuItem _menuView = null!;
    private ToolStripMenuItem _menuHelp = null!;

    private Button btnOpen = null!;
    private Button btnPlayPause = null!;
    private Button btnStop = null!;
    private Button btnPrev = null!;
    private Button btnNext = null!;
    private Button btnMute = null!;
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
    private ToolStripStatusLabel statusTimeLabel = null!;

    // Fullscreen state
    private bool _isFullscreen;
    private FormBorderStyle _normalBorderStyle;
    private Size _normalSize;
    private Point _normalLocation;
    private bool _wasMaximized;

    // Audio tracks from TrackListChanged event
    private List<SubtitleSource>? _audioTracks;

    private bool _uiVisible = true;
    private int _lastMouseActivityTick;

    private readonly System.Windows.Forms.Timer _updateTimer;

    // === Layout constants ===
    private const int WINDOW_W = 1088;
    private const int WINDOW_H = 612;
    private const int MARGIN = 10;
    private const int PANEL_RIGHT_W = 230;   // playlist sidebar width
    private const int VIDEO_PANEL_H = 480;
    private const int AUTO_HIDE_TIMEOUT_MS = 3000; // 3 seconds of inactivity

    public MainForm()
    {
        _player = new MediaFoundationPlayer();

        this.Text = "Cine";
        this.ClientSize = new Size(WINDOW_W, WINDOW_H);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(800, 500);
        this.KeyPreview = true;
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.Icon = CreateAppIcon();

        _normalBorderStyle = this.FormBorderStyle;
        _normalSize = this.ClientSize;
        _normalLocation = this.Location;

        // UI update timer (100ms tick for position sync + auto-hide in fullscreen)
        _updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _updateTimer.Tick += OnUpdateTimerTick;
        _updateTimer.Start();

        _player.UseNativeRendering = true;
        this.HandleCreated += OnHandleCreated;

        BuildMenuStrip();
        InitializeUI();
        playerPanel.Resize += OnPlayerPanelResize;
        WirePlayerEvents();
        _player.Opened += OnPlayerOpened;
        _player.Closed += OnPlayerClosed;

        // Enable drag and drop
        this.AllowDrop = true;
        this.DragEnter += OnDragEnter;
        this.DragDrop += OnDragDrop;
        playerPanel.AllowDrop = true;
        playerPanel.DragEnter += OnDragEnter;
        playerPanel.DragDrop += OnDragDrop;

        // Track mouse activity for auto-hide
        this.MouseMove += OnMouseActivity;
        this.MouseClick += OnMouseActivityClick;
        playerPanel.MouseMove += OnMouseActivity;
        playerPanel.MouseClick += OnMouseActivityClick;
    }

    // ======================
    //  MENU STRIP
    // ======================

    private void BuildMenuStrip()
    {
        _menuStrip = new MenuStrip();

        // --- File Menu ---
        _menuFile = new ToolStripMenuItem("&File");
        _menuFile.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&Open...", null, (s, e) => OpenVideo(), "Ctrl+O"),
            new ToolStripMenuItem("Open &URL...", null, (s, e) => OpenUrl(), "Ctrl+U"),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Playlist", null, (s, e) => ShowPlaylistPanel()),
            new ToolStripMenuItem("&Fullscreen", null, (s, e) => _player.ToggleFullscreen(), Keys.F),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Save Screenshot...", null, (s, e) => Screenshot(), Keys.S),
            new ToolStripSeparator(),
            new ToolStripMenuItem("E&xit", null, (s, e) => Close(), "Alt+F4")
        });

        // --- Playback Menu ---
        _menuPlayback = new ToolStripMenuItem("&Playback");
        _menuPlayback.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&Play/Pause", null, (s, e) => TogglePlayPause(), Keys.Space),
            new ToolStripMenuItem("&Stop", null, (s, e) => _player.Stop(), Keys.Escape),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Previous", null, (s, e) => _player.PreviousPlaylistItem(), Keys.PageUp),
            new ToolStripMenuItem("&Next", null, (s, e) => _player.NextPlaylistItem(), Keys.PageDown),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Seek &Forward", null, (s, e) => _player.SeekForward(5.0), Keys.Right),
            new ToolStripMenuItem("Seek &Backward", null, (s, e) => _player.SeekBackward(5.0), Keys.Left),
            new ToolStripMenuItem("Seek &Large Forward", null, (s, e) => _player.SeekForward(60.0), Keys.Shift | Keys.Right),
            new ToolStripMenuItem("Seek Large &Backward", null, (s, e) => _player.SeekBackward(60.0), Keys.Shift | Keys.Left),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Chapter Next", null, (s, e) => _player.NextChapter(), Keys.P),
            new ToolStripMenuItem("Chapter &Previous", null, (s, e) => _player.PreviousChapter(), Keys.Shift | Keys.P),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Loop File", null, (s, e) => _player.ToggleLoopFile(), Keys.L),
            new ToolStripMenuItem("Loop &Playlist", null, (s, e) => _player.ToggleLoopPlaylist(), Keys.Control | Keys.L),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Reset Speed", null, (s, e) => _player.ResetSpeed(), Keys.Back)
        });

        // --- Audio Menu ---
        var menuAudio = new ToolStripMenuItem("&Audio");
        menuAudio.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("Volume &Up", null, (s, e) => _player.IncreaseVolume(), Keys.Up),
            new ToolStripMenuItem("Volume &Down", null, (s, e) => _player.DecreaseVolume(), Keys.Down),
            new ToolStripMenuItem("&Mute", null, (s, e) => _player.Mute(!_player.IsMuted), Keys.M),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Speed +", null, (s, e) => _player.IncreaseSpeed(), Keys.OemPeriod),
            new ToolStripMenuItem("&Speed -", null, (s, e) => _player.DecreaseSpeed(), Keys.Oemcomma),
        });

        // --- Subtitle Menu ---
        var menuSubtitle = new ToolStripMenuItem("&Subtitle");
        menuSubtitle.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&Delay +", null, (s, e) => _player.IncreaseSubtitleDelay(), Keys.OemPeriod),
            new ToolStripMenuItem("&Delay -", null, (s, e) => _player.DecreaseSubtitleDelay(), Keys.Oemcomma),
            new ToolStripMenuItem("&Cycle Track", null, (s, e) => _player.CycleSubtitleTrack(), Keys.D3 | Keys.Shift),
        });

        // --- View Menu ---
        _menuView = new ToolStripMenuItem("&View");
        _menuView.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&Fullscreen", null, (s, e) => _player.ToggleFullscreen(), Keys.F),
            new ToolStripMenuItem("Toggle &Menu Bar", null, (s, e) => ToggleMenuStrip(), Keys.F10),
            new ToolStripMenuItem("Toggle &Status Bar", null, (s, e) => ToggleStatusBar(), Keys.F11),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Reset Layout", null, (s, e) => ResetLayout(), "F5")
        });

        // --- Help Menu ---
        _menuHelp = new ToolStripMenuItem("&Help");
        var aboutItem = new ToolStripMenuItem("&About", null, (s, e) => ShowAboutDialog());
        aboutItem.ShortcutKeys = Keys.F1;
        _menuHelp.DropDownItems.AddRange(new ToolStripItem[]
        {
            aboutItem
        });

        _menuStrip.Items.AddRange(new ToolStripItem[]
        {
            _menuFile, _menuPlayback, menuAudio, menuSubtitle, _menuView, _menuHelp
        });

        this.MainMenuStrip = _menuStrip;
        this.Controls.Add(_menuStrip);
    }

    // ======================
    //  UI INITIALIZATION
    // ======================

    private void InitializeUI()
    {
        int x = MARGIN;
        int y = MARGIN + _menuStrip.Height;  // Account for menu strip
        int videoW = WINDOW_W - MARGIN * 2 - PANEL_RIGHT_W - 8;  // gap between video + playlist

        // --- VIDEO PANEL (native D3D11 rendering surface) ---
        playerPanel = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(videoW, VIDEO_PANEL_H),
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        // Double-buffer to reduce flicker
        typeof(Panel).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, playerPanel, new object[] { true });
        this.Controls.Add(playerPanel);

        // --- PLAYLIST PANEL (right sidebar) ---
        int playlistX = x + videoW + 8;

        var lblPlaylist = new Label
        {
            Text = "📋 Playlist",
            Location = new Point(playlistX, y),
            Size = new Size(PANEL_RIGHT_W, 22),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        this.Controls.Add(lblPlaylist);

        lstPlaylist = new ListBox
        {
            Location = new Point(playlistX, y + 24),
            Size = new Size(PANEL_RIGHT_W, VIDEO_PANEL_H - 24 + 2),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        lstPlaylist.SelectedIndexChanged += OnPlaylistItemSelected;
        lstPlaylist.KeyDown += OnPlaylistKeyDown;
        lstPlaylist.DoubleClick += OnPlaylistDoubleClick;
        this.Controls.Add(lstPlaylist);

        y += VIDEO_PANEL_H + 10;

        // --- PATH BAR: Open button + file path + Screenshot ---
        btnOpen = new Button
        {
            Text = "📂 Open",
            Location = new Point(x, y),
            Size = new Size(80, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnOpen.Click += (s, e) => OpenVideo();
        this.Controls.Add(btnOpen);

        txtPath = new TextBox
        {
            Location = new Point(x + 86, y),
            Size = new Size(videoW - 170, 30),
            ReadOnly = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = SystemColors.Window
        };
        this.Controls.Add(txtPath);

        btnScreenshot = new Button
        {
            Text = "📷 Screenshot",
            Location = new Point(x + videoW - 78, y),
            Size = new Size(90, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        btnScreenshot.Click += (s, e) => Screenshot();
        this.Controls.Add(btnScreenshot);

        y += 34;

        // --- SEEK BAR + TIME LABELS ---
        int seekBarW = WINDOW_W - MARGIN * 2 - PANEL_RIGHT_W - 8;

        // Time labels above seek bar
        lblPosition = new Label
        {
            Text = "0:00:00",
            Location = new Point(x, y),
            Size = new Size(60, 16),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 8.5f),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(lblPosition);

        lblDuration = new Label
        {
            Text = "0:00:00",
            Location = new Point(x + seekBarW - 60, y),
            Size = new Size(60, 16),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Consolas", 8.5f),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        this.Controls.Add(lblDuration);

        y += 18;

        seekBar = new TrackBar
        {
            Location = new Point(x, y),
            Size = new Size(seekBarW, 28),
            Minimum = 0,
            Maximum = 1000,    // higher resolution than default 100
            TickStyle = (TickStyle)0,  // NoTicks
            Enabled = false,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        seekBar.Scroll += OnSeekBarScroll;
        this.Controls.Add(seekBar);

        y += 30;

        // --- TRANSPORT CONTROLS ---
        int transportY = y;

        btnPlayPause = new Button
        {
            Text = "▶ Play",
            Location = new Point(x, transportY),
            Size = new Size(80, 35),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnPlayPause.Click += (s, e) => TogglePlayPause();
        this.Controls.Add(btnPlayPause);

        btnStop = new Button
        {
            Text = "■ Stop",
            Location = new Point(x + 84, transportY),
            Size = new Size(70, 35),
            Enabled = false,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnStop.Click += (s, e) => _player.Stop();
        this.Controls.Add(btnStop);

        btnPrev = new Button
        {
            Text = "⏮ Prev",
            Location = new Point(x + 158, transportY),
            Size = new Size(75, 35),
            Enabled = false,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnPrev.Click += (s, e) => _player.PreviousPlaylistItem();
        this.Controls.Add(btnPrev);

        btnNext = new Button
        {
            Text = "Next ⏭",
            Location = new Point(x + 237, transportY),
            Size = new Size(75, 35),
            Enabled = false,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnNext.Click += (s, e) => _player.NextPlaylistItem();
        this.Controls.Add(btnNext);

        // Right side controls
        int rightX = x + videoW - 420;

        // Volume group
        var lblVolIcon = new Label
        {
            Text = "🔊",
            Location = new Point(rightX, transportY + 8),
            Size = new Size(24, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(lblVolIcon);

        volumeBar = new TrackBar
        {
            Location = new Point(rightX + 24, transportY + 4),
            Size = new Size(100, 35),
            Minimum = 0,
            Maximum = 150,
            Value = 50,
            TickStyle = (TickStyle)0,
            SmallChange = 5,
            LargeChange = 10,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        volumeBar.Scroll += OnVolumeBarScroll;
        this.Controls.Add(volumeBar);

        btnMute = new Button
        {
            Text = "🔇",
            Location = new Point(rightX + 128, transportY),
            Size = new Size(36, 35),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnMute.Click += (s, e) => _player.Mute(!_player.IsMuted);
        this.Controls.Add(btnMute);

        lblVolume = new Label
        {
            Text = "50",
            Location = new Point(rightX + 168, transportY + 8),
            Size = new Size(35, 20),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(lblVolume);

        // Speed group
        int speedX = rightX + 210;

        lblSpeed = new Label
        {
            Text = "Speed:",
            Location = new Point(speedX, transportY + 8),
            Size = new Size(50, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(lblSpeed);

        speedBar = new TrackBar
        {
            Location = new Point(speedX + 54, transportY + 4),
            Size = new Size(80, 35),
            Minimum = 25,
            Maximum = 400,
            Value = 100,
            TickStyle = (TickStyle)0,
            SmallChange = 10,
            LargeChange = 25,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        speedBar.Scroll += OnSpeedBarScroll;
        this.Controls.Add(speedBar);

        lblSpeedValue = new Label
        {
            Text = "1.00x",
            Location = new Point(speedX + 138, transportY + 8),
            Size = new Size(50, 20),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Consolas", 8.5f),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(lblSpeedValue);

        btnResetSpeed = new Button
        {
            Text = "↺",
            Location = new Point(speedX + 190, transportY),
            Size = new Size(32, 35),
            Font = new Font("Segoe UI", 12f),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnResetSpeed.Click += (s, e) => _player.ResetSpeed();
        this.Controls.Add(btnResetSpeed);

        y += 38;

        // --- SUBTITLE + AUDIO ROW ---
        var lblSubIcon = new Label
        {
            Text = "Sub:",
            Location = new Point(x, y + 6),
            Size = new Size(34, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(lblSubIcon);

        cmbSubtitle = new ComboBox
        {
            Location = new Point(x + 36, y + 3),
            Size = new Size(140, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        cmbSubtitle.SelectedIndexChanged += OnSubtitleChanged;
        this.Controls.Add(cmbSubtitle);

        var btnSubDelayUp = new Button
        {
            Text = "+",
            Location = new Point(x + 180, y + 2),
            Size = new Size(28, 24),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnSubDelayUp.Click += (s, e) => _player.IncreaseSubtitleDelay();
        this.Controls.Add(btnSubDelayUp);

        var btnSubDelayDown = new Button
        {
            Text = "-",
            Location = new Point(x + 210, y + 2),
            Size = new Size(28, 24),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnSubDelayDown.Click += (s, e) => _player.DecreaseSubtitleDelay();
        this.Controls.Add(btnSubDelayDown);

        var lblAudioIcon = new Label
        {
            Text = "Audio:",
            Location = new Point(x + 250, y + 6),
            Size = new Size(46, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(lblAudioIcon);

        cmbAudio = new ComboBox
        {
            Location = new Point(x + 298, y + 3),
            Size = new Size(100, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        cmbAudio.SelectedIndexChanged += OnAudioChanged;
        this.Controls.Add(cmbAudio);

        // Loop buttons
        var lblLoop = new Label
        {
            Text = "Loop:",
            Location = new Point(x + 410, y + 6),
            Size = new Size(40, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        this.Controls.Add(lblLoop);

        var btnLoopFile = new Button
        {
            Text = "File",
            Location = new Point(x + 454, y + 2),
            Size = new Size(50, 24),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnLoopFile.Click += (s, e) => _player.ToggleLoopFile();
        this.Controls.Add(btnLoopFile);

        var btnLoopPlaylist = new Button
        {
            Text = "List",
            Location = new Point(x + 507, y + 2),
            Size = new Size(50, 24),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        btnLoopPlaylist.Click += (s, e) => _player.ToggleLoopPlaylist();
        this.Controls.Add(btnLoopPlaylist);

        // --- STATUS STRIP ---
        statusStrip = new StatusStrip
        {
            Dock = DockStyle.Bottom,
            SizingGrip = true,
            RenderMode = ToolStripRenderMode.ManagerRenderMode
        };

        statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        statusStrip.Items.Add(statusLabel);

        statusTimeLabel = new ToolStripStatusLabel
        {
            Text = "0:00 / 0:00",
            TextAlign = ContentAlignment.MiddleRight
        };
        statusStrip.Items.Add(statusTimeLabel);

        this.Controls.Add(statusStrip);

        // Bring menu to front
        _menuStrip.BringToFront();
    }

    private Icon CreateAppIcon()
    {
        // Simple colored square as placeholder icon
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(30, 80, 180));
        using var font = new Font("Segoe UI", 16, FontStyle.Bold);
        g.DrawString("C", font, Brushes.White, 4, 2);
        return Icon.FromHandle(bmp.GetHicon());
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
            _audioTracks = e.AudioTracks?.ToList();
            if (InvokeRequired) { Invoke(() => UpdateTrackLists()); return; }
            UpdateTrackLists();
        };

        _player.PlaylistChanged += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => UpdatePlaylistUI()); return; }
            UpdatePlaylistUI();
        };

        _player.FullscreenChangedEvent += (s, e) =>
        {
            if (InvokeRequired) { Invoke(() => OnFullscreenChanged(e)); return; }
            OnFullscreenChanged(e);
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
        UpdateStatus($"Now playing: {Path.GetFileName(e.FilePath)}");
        _updateTimer.Start();
    }

    private void OnFileEnded()
    {
        _updateTimer.Stop();
        btnPlayPause.Text = "▶ Play";
        btnPlayPause.Enabled = false;
        btnStop.Enabled = false;
        btnPrev.Enabled = false;
        btnNext.Enabled = false;
        seekBar.Enabled = false;
        seekBar.Value = 0;
        lblPosition.Text = "0:00:00";
        lblDuration.Text = "0:00:00";
        statusTimeLabel.Text = "0:00 / 0:00";
        UpdateStatus("Stopped");
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
            statusTimeLabel.Text = $"{FormatTime(e.Position)} / {FormatTime(_player.Duration)}";
        }
    }

    private void OnDurationChanged(DurationChangedEventArgs e)
    {
        lblDuration.Text = FormatTime(e.Duration);
        statusTimeLabel.Text = $"{FormatTime(_player.Position)} / {FormatTime(e.Duration)}";
    }

    private void OnVolumeChanged(VolumeChangedEventArgs e)
    {
        volumeBar.Value = Math.Min(volumeBar.Maximum, (int)e.Volume);
        lblVolume.Text = $"{(int)e.Volume}";
    }

    private void OnFullscreenChanged(FullscreenChangedEventArgs e)
    {
        _isFullscreen = e.IsFullscreen;
        UpdateFullscreenUI();
    }

    private void UpdatePlayPauseButton(bool isPlaying)
    {
        btnPlayPause.Text = isPlaying ? "⏸ Pause" : "▶ Play";
        _menuPlayback.Text = isPlaying ? "⏸ Pause" : "▶ Play";
    }

    // ======================
    //  TIMER
    // ======================

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        // Timer keeps UI responsive and drives auto-hide check
        if (_isFullscreen)
        {
            int idleMs = Environment.TickCount - _lastMouseActivityTick;
            bool shouldHide = idleMs > AUTO_HIDE_TIMEOUT_MS;
            if (shouldHide && _uiVisible)
            {
                SetUIVisibility(false);
            }
            else if (!shouldHide && !_uiVisible)
            {
                SetUIVisibility(true);
            }
        }
    }

    // ======================
    //  AUTO-HIDE UI
    // ======================

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        if (!_isFullscreen) return;
        int idleMs = Environment.TickCount - _lastMouseActivityTick;
        bool shouldHide = idleMs > AUTO_HIDE_TIMEOUT_MS;
        if (shouldHide && _uiVisible)
        {
            SetUIVisibility(false);
            _updateTimer.Stop();
        }
    }

    private void SetUIVisibility(bool visible)
    {
        _uiVisible = visible;
        _menuStrip.Visible = visible;
        statusStrip.Visible = visible;
        foreach (Control c in this.Controls)
        {
            if (c != playerPanel) c.Visible = visible;
        }
        if (visible) BringMenuToFront();
    }

    private void BringMenuToFront()
    {
        _menuStrip.BringToFront();
        statusStrip.BringToFront();
    }

    // ======================
    //  FULLSCREEN
    // ======================

    private void UpdateFullscreenUI()
    {
        if (_isFullscreen)
        {
            _normalBorderStyle = this.FormBorderStyle;
            _normalSize = this.Size;
            _normalLocation = this.Location;
            _wasMaximized = this.WindowState == FormWindowState.Maximized;

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.Bounds = Screen.FromControl(this).Bounds;
            _updateTimer.Start();
        }
        else
        {
            _updateTimer.Stop();
            this.FormBorderStyle = _normalBorderStyle;
            if (_wasMaximized)
                this.WindowState = FormWindowState.Maximized;
            else
            {
                this.Size = _normalSize;
                this.Location = _normalLocation;
            }
            SetUIVisibility(true);
        }
        BringMenuToFront();
    }

    private void ToggleMenuStrip()
    {
        _menuStrip.Visible = !_menuStrip.Visible;
    }

    private void ToggleStatusBar()
    {
        statusStrip.Visible = !statusStrip.Visible;
    }

    private void ResetLayout()
    {
        if (_isFullscreen) _player.ToggleFullscreen();
        this.Size = new Size(WINDOW_W, WINDOW_H);
        this.StartPosition = FormStartPosition.CenterScreen;
        SetUIVisibility(true);
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

    private void OpenUrl()
    {
        using var dialog = new Form
        {
            Text = "Open URL",
            Size = new Size(400, 120),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent
        };
        var txtUrl = new TextBox { Location = new Point(10, 10), Size = new Size(360, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        var btnOk = new Button { Text = "OK", Location = new Point(210, 40), Size = new Size(80, 28), DialogResult = DialogResult.OK, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(300, 40), Size = new Size(80, 28), DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        dialog.Controls.AddRange(new Control[] { txtUrl, btnOk, btnCancel });
        dialog.AcceptButton = btnOk;
        dialog.CancelButton = btnCancel;
        if (dialog.ShowDialog(this) == DialogResult.OK && Uri.TryCreate(txtUrl.Text, UriKind.Absolute, out _))
        {
            _player.AddToPlaylist(txtUrl.Text);
        }
    }

    private void Screenshot()
    {
        try
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Save Screenshot",
                FileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };
            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                _player.ScreenshotWithSubtitles();
                MessageBox.Show("Screenshot saved!", "Cine", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Screenshot failed: {ex.Message}", "Cine", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowPlaylistPanel()
    {
        lstPlaylist.Visible = !lstPlaylist.Visible;
    }

    private void ShowAboutDialog()
    {
        MessageBox.Show(
            "Cine Media Player\n\n" +
            "A native Windows media player built with Direct3D 11 and Media Foundation.\n" +
            "Features GPU-accelerated video rendering with NV12→BGRA shader pipeline.\n\n" +
            "Developed for Cine project.",
            "About Cine",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    // ======================
    //  DRAG & DROP
    // ======================

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data is not null && e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data is not null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files is not null)
            {
                foreach (var file in files)
                {
                    if (File.Exists(file))
                    {
                        _player.AddToPlaylist(file);
                    }
                }
                txtPath.Text = files[0];
                _player.SetPlaylistPosition(0);
                UpdatePlaylistUI();
            }
        }
    }

    // ======================
    //  MOUSE ACTIVITY
    // ======================

    private void OnMouseActivity(object? sender, MouseEventArgs e)
    {
        _lastMouseActivityTick = Environment.TickCount;
        if (_isFullscreen && !_uiVisible)
        {
            SetUIVisibility(true);
            _updateTimer.Stop();
            _updateTimer.Start();
        }
    }

    private void OnMouseActivityClick(object? sender, MouseEventArgs e)
    {
        _lastMouseActivityTick = Environment.TickCount;
    }

    // ======================
    //  KEYBOARD SHORTCUTS (matching Python INTERNAL_BINDINGS)
    // ======================

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _lastMouseActivityTick = Environment.TickCount;

        if (e.KeyCode == Keys.Escape)
        {
            if (_isFullscreen)
                _player.ToggleFullscreen();
            else
                _player.Stop();
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

        // --- Fullscreen toggle with F ---
        else if (e.KeyCode == Keys.F && !e.Alt && !e.Control)
        {
            _player.ToggleFullscreen();
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
        if (_audioTracks != null)
        {
            foreach (var track in _audioTracks)
            {
                cmbAudio.Items.Add(track);
            }
        }
        cmbAudio.SelectedIndex = Math.Max(0, _player.AudioTrack);
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

    private void UpdateStatus(string text)
    {
        statusLabel.Text = text;
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

    // ======================
    //  RESIZE HANDLING
    // ======================

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (!_isFullscreen && !_normalSize.IsEmpty)
        {
            _normalSize = this.Size;
        }
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        if (!_isFullscreen)
        {
            _normalSize = this.Size;
            _normalLocation = this.Location;
        }
    }
}