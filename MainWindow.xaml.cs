using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace GamerMX.Tool;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x474D;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VkG = 0x47;
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExLayered = 0x00080000L;

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background);
    private readonly string _dataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GamerMX Tool", "data.json");
    private readonly Random _random = new();
    private readonly List<string> _tips =
    [
        "💡  Правило 20–20–20: каждые 20 минут смотри 20 секунд на объект в 6 метрах.",
        "🫗  Поставь воду рядом: несколько глотков каждый час помогают сохранять концентрацию.",
        "🧘  Опусти плечи, выпрями спину и сделай пять медленных глубоких вдохов.",
        "🖐  Разожми кисти: 10 кругов запястьями в каждую сторону снижают напряжение.",
        "🚶  Две минуты ходьбы улучшают кровообращение после долгого сидения.",
        "👀  Быстро поморгай 15 раз, затем закрой глаза на 20 секунд.",
        "🦵  Встань и сделай 10 спокойных приседаний без рывков."
    ];
    private readonly List<string> _exercises =
    [
        "Разминка всего тела",
        "Фокус для глаз",
        "Плечи и шея",
        "Вода и дыхание",
        "Кисти и предплечья",
        "Короткая прогулка"
    ];

    private AppData _data = new();
    private TimeSpan _remaining = TimeSpan.FromMinutes(25);
    private TimeSpan _total = TimeSpan.FromMinutes(25);
    private DateTime _endAtUtc;
    private long _lastShownSecond = -1;
    private DateTime _sessionStarted = DateTime.Now;
    private bool _isRunning;
    private bool _isOverlay;
    private bool _isLoaded;
    private bool _allowClose;
    private IntPtr _hwnd;
    private Rect _normalBounds;

    public MainWindow()
    {
        InitializeComponent();
        // Ten lightweight vector updates per second look continuous for a
        // countdown arc while keeping layered-window redraws inexpensive.
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += Timer_Tick;
        Loaded += MainWindow_Loaded;
        NotesList.ItemsSource = _data.Notes;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadData();
        ApplyDataToUi();
        _isLoaded = true;
        UpdateTimerDisplay();
    }

    private void ChromeContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
            return;

        var clip = new RectangleGeometry(
            new Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
            25,
            25);
        clip.Freeze();
        ChromeContent.Clip = clip;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        if (HwndSource.FromHwnd(_hwnd) is { } source)
            source.AddHook(WndProc);

        RegisterHotKey(_hwnd, HotkeyId, ModControl | ModShift, VkG);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmHotkey = 0x0312;
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            if (ClickThroughCheck.IsChecked == true)
            {
                ClickThroughCheck.IsChecked = false;
                SetClickThrough(false);
            }
            else
            {
                ToggleOverlay();
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _remaining = _endAtUtc - DateTime.UtcNow;
        if (_remaining <= TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
            UpdateRingProgress();
            UpdateTimerDisplay();
            CompleteTimer();
            return;
        }

        UpdateRingProgress();
        var shownSecond = (long)Math.Ceiling(_remaining.TotalSeconds);
        if (shownSecond != _lastShownSecond)
        {
            _lastShownSecond = shownSecond;
            UpdateTimerDisplay();
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _remaining = TimeSpan.FromTicks(Math.Max(0, (_endAtUtc - DateTime.UtcNow).Ticks));
            _timer.Stop();
            _isRunning = false;
            StartButton.Content = "Продолжить";
            StatusBadge.Text = "●  ПАУЗА";
            StatusBadge.Foreground = System.Windows.Media.Brushes.Gold;
            return;
        }

        if (_remaining <= TimeSpan.Zero || _remaining == _total)
        {
            var interval = ReadInterval();
            if (interval <= TimeSpan.Zero)
            {
                MinutesBox.Text = "25";
                interval = TimeSpan.FromMinutes(25);
            }
            _remaining = _total = interval;
        }

        _endAtUtc = DateTime.UtcNow + _remaining;
        _lastShownSecond = -1;
        _timer.Start();
        _isRunning = true;
        StartButton.Content = "Пауза";
        StatusBadge.Text = "●  В ФОКУСЕ";
        StatusBadge.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#5EEAD4")!;
        UpdateTimerDisplay();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _isRunning = false;
        _remaining = _total = ReadInterval();
        if (_remaining <= TimeSpan.Zero)
            _remaining = _total = TimeSpan.FromMinutes(25);
        StartButton.Content = "Старт";
        TimerLabel.Text = "СЛЕДУЮЩАЯ ПАУЗА";
        StatusBadge.Text = "●  ГОТОВ";
        StatusBadge.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#5EEAD4")!;
        _lastShownSecond = -1;
        UpdateTimerDisplay();
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !int.TryParse(value, out var seconds))
            return;

        var interval = TimeSpan.FromSeconds(seconds);
        HoursBox.Text = ((int)interval.TotalHours).ToString();
        MinutesBox.Text = interval.Minutes.ToString("00");
        SecondsBox.Text = interval.Seconds.ToString("00");
        _timer.Stop();
        _isRunning = false;
        _remaining = _total = interval;
        StartButton.Content = "Старт";
        _lastShownSecond = -1;
        UpdateTimerDisplay();
    }

    private void CompleteTimer()
    {
        _timer.Stop();
        _isRunning = false;
        _remaining = TimeSpan.Zero;
        _data.TodayBreaks++;
        _data.TotalBreaks++;
        _data.LastUsedDate = DateTime.Today;
        var exercise = _exercises[_random.Next(_exercises.Count)];
        ExerciseText.Text = exercise;
        OverlayExerciseText.Text = exercise;
        TimerLabel.Text = "ВРЕМЯ РАЗМЯТЬСЯ";
        StatusBadge.Text = "●  ПЕРЕРЫВ";
        StatusBadge.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F9A8D4")!;
        StartButton.Content = "Ещё интервал";
        _data.History.Insert(0, $"{DateTime.Now:HH:mm}  •  Перерыв завершён  •  {exercise}");
        if (_data.History.Count > 12)
            _data.History.RemoveAt(_data.History.Count - 1);
        RefreshStats();
        SaveData();

        if (SoundCheck.IsChecked == true)
            _ = Task.Run(PlayGentleChime);
        if (BreakOverlayCheck.IsChecked == true && !_isOverlay)
            ToggleOverlay();

        FlashOverlay();
        UpdateTimerDisplay();
    }

    private TimeSpan ReadInterval()
    {
        _ = int.TryParse(HoursBox.Text, out var hours);
        _ = int.TryParse(MinutesBox.Text, out var minutes);
        _ = int.TryParse(SecondsBox.Text, out var seconds);
        hours = Math.Clamp(hours, 0, 23);
        minutes = Math.Clamp(minutes, 0, 59);
        seconds = Math.Clamp(seconds, 0, 59);
        return new TimeSpan(hours, minutes, seconds);
    }

    private void UpdateTimerDisplay()
    {
        var visibleTime = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(_remaining.TotalSeconds)));
        var totalHours = (int)visibleTime.TotalHours;
        var display = totalHours > 0
            ? $"{totalHours:00}:{visibleTime.Minutes:00}:{visibleTime.Seconds:00}"
            : $"{visibleTime.Minutes:00}:{visibleTime.Seconds:00}";
        TimerText.Text = display;
        OverlayTimerText.Text = display;
        TimerProgress.Value = _total.TotalSeconds <= 0
            ? 0
            : Math.Clamp((1 - _remaining.TotalSeconds / _total.TotalSeconds) * 100, 0, 100);
        UpdateRingProgress();
        FocusTimeText.Text = $"{Math.Max(0, (int)(DateTime.Now - _sessionStarted).TotalMinutes)} мин";
    }

    private void UpdateRingProgress()
    {
        CountdownRing.Progress = _total.TotalSeconds <= 0
            ? 0
            : Math.Clamp(_remaining.TotalSeconds / _total.TotalSeconds, 0, 1);
    }

    private void FlashOverlay()
    {
        var animation = new DoubleAnimation(.45, 1, TimeSpan.FromMilliseconds(260))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3)
        };
        CompactOverlay.BeginAnimation(OpacityProperty, animation);
    }

    private void OverlayButton_Click(object sender, RoutedEventArgs e) => ToggleOverlay();

    private void ToggleOverlay()
    {
        if (!_isOverlay)
        {
            _normalBounds = new Rect(Left, Top, Width, Height);
            _isOverlay = true;
            WindowChrome.Visibility = Visibility.Collapsed;
            CompactOverlay.Visibility = Visibility.Visible;
            Width = 420;
            Height = 170;
            MinWidth = 0;
            MinHeight = 0;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            Left = SystemParameters.WorkArea.Right - Width - 28;
            Top = SystemParameters.WorkArea.Top + 28;
            OverlayButton.Content = "Развернуть";
        }
        else
        {
            _isOverlay = false;
            CompactOverlay.Visibility = Visibility.Collapsed;
            WindowChrome.Visibility = Visibility.Visible;
            MinWidth = 1040;
            MinHeight = 680;
            Width = Math.Max(1040, _normalBounds.Width);
            Height = Math.Max(680, _normalBounds.Height);
            Left = _normalBounds.Left;
            Top = _normalBounds.Top;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Topmost = TopmostCheck.IsChecked == true;
            OverlayButton.Content = "Включить";
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RootGrid is null)
            return;
        RootGrid.Opacity = Math.Clamp(e.NewValue / 100d, .25, 1);
        if (OpacityValueText is not null)
            OpacityValueText.Text = $"{e.NewValue:0}%";
        if (_isLoaded)
            SaveData();
    }

    private void TopmostCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoaded && !_isOverlay)
        {
            Topmost = TopmostCheck.IsChecked == true;
            SaveData();
        }
    }

    private void ClickThroughCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            SetClickThrough(ClickThroughCheck.IsChecked == true);
            SaveData();
        }
    }

    private void SetClickThrough(bool enabled)
    {
        if (_hwnd == IntPtr.Zero)
            return;
        var style = GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
        style = enabled
            ? style | WsExTransparent | WsExLayered
            : style & ~WsExTransparent;
        SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr(style));
    }

    private void AutoStartCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
            return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (AutoStartCheck.IsChecked == true)
                key?.SetValue("GamerMX Tool", $"\"{Environment.ProcessPath}\"");
            else
                key?.DeleteValue("GamerMX Tool", false);
        }
        catch
        {
            AutoStartCheck.IsChecked = false;
        }
        SaveData();
    }

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        var title = NoteTitleBox.Text.Trim();
        var body = NoteBodyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title) || title == "Заголовок")
            title = "Без заголовка";
        if (string.IsNullOrWhiteSpace(body) || body == "Что важно не забыть?")
            return;

        _data.Notes.Insert(0, new NoteItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Body = body,
            Created = DateTime.Now.ToString("dd.MM.yyyy • HH:mm")
        });
        NotesList.Items.Refresh();
        NoteTitleBox.Text = string.Empty;
        NoteBodyBox.Text = string.Empty;
        RefreshNotesCount();
        SaveData();
    }

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid id })
            return;
        var note = _data.Notes.FirstOrDefault(x => x.Id == id);
        if (note is not null)
            _data.Notes.Remove(note);
        NotesList.Items.Refresh();
        RefreshNotesCount();
        SaveData();
    }

    private void RefreshNotesCount() =>
        NotesCountText.Text = $"{_data.Notes.Count} {Pluralize(_data.Notes.Count, "заметка", "заметки", "заметок")}";

    private void RefreshStats()
    {
        TodayBreaksText.Text = _data.TodayBreaks.ToString();
        StreakText.Text = $"{Math.Max(1, _data.Streak)} {Pluralize(Math.Max(1, _data.Streak), "день", "дня", "дней")}";
        FocusTimeText.Text = $"{Math.Max(0, (int)(DateTime.Now - _sessionStarted).TotalMinutes)} мин";
        SessionHistoryList.ItemsSource = null;
        SessionHistoryList.ItemsSource = _data.History;
    }

    private static string Pluralize(int value, string one, string few, string many)
    {
        var n = Math.Abs(value) % 100;
        var n1 = n % 10;
        if (n is > 10 and < 20) return many;
        if (n1 == 1) return one;
        return n1 is >= 2 and <= 4 ? few : many;
    }

    private void NewTip_Click(object sender, RoutedEventArgs e) =>
        TipText.Text = _tips[_random.Next(_tips.Count)];

    private void TimerNav_Click(object sender, RoutedEventArgs e) => ShowPage(TimerPage, TimerNav);
    private void NotesNav_Click(object sender, RoutedEventArgs e) => ShowPage(NotesPage, NotesNav);
    private void StatsNav_Click(object sender, RoutedEventArgs e)
    {
        RefreshStats();
        ShowPage(StatsPage, StatsNav);
    }
    private void SettingsNav_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, SettingsNav);
    private void AboutNav_Click(object sender, RoutedEventArgs e) => ShowPage(AboutPage, AboutNav);

    private void ShowPage(UIElement page, Button nav)
    {
        TimerPage.Visibility = Visibility.Collapsed;
        NotesPage.Visibility = Visibility.Collapsed;
        StatsPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        AboutPage.Visibility = Visibility.Collapsed;
        TimerNav.Tag = NotesNav.Tag = StatsNav.Tag = SettingsNav.Tag = AboutNav.Tag = null;
        page.Visibility = Visibility.Visible;
        nav.Tag = "Active";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && !_isOverlay)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        Close();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void Telegram_Click(object sender, RoutedEventArgs e) => OpenUrl("https://t.me/dmitrymx");
    private void Website_Click(object sender, RoutedEventArgs e) => OpenUrl("https://mxmvdev.ru");

    private void LoadData()
    {
        try
        {
            if (File.Exists(_dataPath))
                _data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(_dataPath)) ?? new AppData();
        }
        catch
        {
            _data = new AppData();
        }

        if (_data.LastUsedDate.Date != DateTime.Today)
        {
            var gap = (DateTime.Today - _data.LastUsedDate.Date).Days;
            _data.Streak = gap == 1 ? Math.Max(1, _data.Streak + 1) : 1;
            _data.TodayBreaks = 0;
            _data.LastUsedDate = DateTime.Today;
        }
        NotesList.ItemsSource = _data.Notes;
    }

    private void ApplyDataToUi()
    {
        OpacitySlider.Value = Math.Clamp(_data.OpacityPercent, 25, 100);
        TopmostCheck.IsChecked = _data.AlwaysOnTop;
        SoundCheck.IsChecked = _data.SoundEnabled;
        BreakOverlayCheck.IsChecked = _data.BreakOverlay;
        AutoStartCheck.IsChecked = _data.AutoStart;
        Topmost = _data.AlwaysOnTop;
        RefreshNotesCount();
        RefreshStats();
    }

    private void SaveData()
    {
        if (!_isLoaded)
            return;
        try
        {
            _data.OpacityPercent = OpacitySlider.Value;
            _data.AlwaysOnTop = TopmostCheck.IsChecked == true;
            _data.SoundEnabled = SoundCheck.IsChecked == true;
            _data.BreakOverlay = BreakOverlayCheck.IsChecked == true;
            _data.AutoStart = AutoStartCheck.IsChecked == true;
            Directory.CreateDirectory(Path.GetDirectoryName(_dataPath)!);
            File.WriteAllText(_dataPath, JsonSerializer.Serialize(_data,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static void PlayGentleChime()
    {
        try
        {
            using var stream = CreateChimeWav();
            using var player = new SoundPlayer(stream);
            player.PlaySync();
        }
        catch
        {
            SystemSounds.Asterisk.Play();
        }
    }

    private static MemoryStream CreateChimeWav()
    {
        const int sampleRate = 44100;
        const short channels = 1;
        const short bits = 16;
        var notes = new[] { (523.25, .22), (659.25, .22), (783.99, .42) };
        var samples = new List<short>();

        foreach (var (frequency, duration) in notes)
        {
            var count = (int)(sampleRate * duration);
            for (var i = 0; i < count; i++)
            {
                var t = (double)i / sampleRate;
                var attack = Math.Min(1, i / (sampleRate * .018));
                var release = Math.Min(1, (count - i) / (sampleRate * .16));
                var envelope = Math.Min(attack, release);
                var wave = Math.Sin(2 * Math.PI * frequency * t) * .55
                         + Math.Sin(2 * Math.PI * frequency * 2 * t) * .12;
                samples.Add((short)(wave * envelope * short.MaxValue * .34));
            }
            samples.AddRange(Enumerable.Repeat((short)0, (int)(sampleRate * .055)));
        }

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            var dataSize = samples.Count * sizeof(short);
            writer.Write("RIFF"u8);
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bits / 8);
            writer.Write((short)(channels * bits / 8));
            writer.Write(bits);
            writer.Write("data"u8);
            writer.Write(dataSize);
            foreach (var sample in samples)
                writer.Write(sample);
        }
        stream.Position = 0;
        return stream;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
            _allowClose = true;
        SaveData();
        if (_hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, HotkeyId);
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, value) : SetWindowLongPtr32(hWnd, nIndex, value);
}

public sealed class AppData
{
    public List<NoteItem> Notes { get; set; } = [];
    public List<string> History { get; set; } = [];
    public int TodayBreaks { get; set; }
    public int TotalBreaks { get; set; }
    public int Streak { get; set; } = 1;
    public DateTime LastUsedDate { get; set; } = DateTime.Today;
    public double OpacityPercent { get; set; } = 96;
    public bool AlwaysOnTop { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool BreakOverlay { get; set; } = true;
    public bool AutoStart { get; set; }
}

public sealed class NoteItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
}