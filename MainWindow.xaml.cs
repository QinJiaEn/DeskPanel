using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskPanel.Models;
using DeskPanel.Services;
using DeskPanel.ViewModels;

namespace DeskPanel;

public partial class MainWindow : Window
{
    private MainViewModel _vm = null!;
    private List<string> _pendingDropFiles = new();
    private bool _isShown = false;
    private double _savedLeft, _savedTop;
    private bool _hasPosition = false;
    private AppSettings _settings = null!;
    private HotkeyService? _hotkeyService;

    // 5-click easter egg trigger
    private int _titleClickCount;
    private System.Windows.Threading.DispatcherTimer? _titleClickTimer;
    private bool _unlockDialogProcessing;

    // Edge snap: track which edge and auto-show on hover
    private enum SnapEdge { None, Left, Right, Top, Bottom }
    private SnapEdge _snapEdge = SnapEdge.None;
    private System.Windows.Threading.DispatcherTimer? _edgeHoverTimer;
    private bool _edgeMode;
    private SnapEdge _edgeModeSide = SnapEdge.None;

    // ── White theme colors ─────────────────────────────
    private static Color C_Bg = Color.FromRgb(0xF5, 0xF5, 0xF5);
    private static Color C_Surface = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static Color C_SurfaceHover = Color.FromRgb(0xF0, 0xF0, 0xF0);
    private static Color C_Sidebar = Color.FromRgb(0xFA, 0xFA, 0xFA);
    private static Color C_SidebarFooter = Color.FromRgb(0xF0, 0xF0, 0xF0);
    private static Color C_TextPrimary = Color.FromRgb(0x1A, 0x1A, 0x1A);
    private static Color C_TextSecondary = Color.FromRgb(0x88, 0x88, 0x88);
    private static Color C_TextMuted = Color.FromRgb(0xAA, 0xAA, 0xAA);
    private static Color C_Accent = Color.FromRgb(0x00, 0x78, 0xD4);
    private static Color C_Danger = Color.FromRgb(0xE7, 0x48, 0x56);
    private static Color C_Border = Color.FromRgb(0xE0, 0xE0, 0xE0);
    private static Color C_DialogBg = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static Color C_InputBg = Color.FromRgb(0xF5, 0xF5, 0xF5);

    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _vm = new MainViewModel();
        DataContext = _vm;
        ApplyAcrylicBackdrop();
    }

    // ── Window lifecycle ──────────────────────────────

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.LoadData();
        RenderCategories();
        RenderFiles();
        ApplyTheme(_settings);
        if (!_hasPosition)
            BottomRightWindow();
        else
            RestorePosition();
        MouseLeave += Window_MouseLeave;
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                Width = _settings.PanelWidth;
                Height = _settings.PanelHeight;
                BottomRightWindow();
            }
        };
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_edgeMode || !_isShown) return;
        var mousePos = System.Windows.Forms.Cursor.Position;
        var rect = new System.Drawing.Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
        if (!rect.Contains(mousePos))
        {
            Hide();
            StartEdgeHoverTimer();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Hide();
    }

    private void Window_Deactivated(object sender, EventArgs e) { }

    public void ApplyTheme(AppSettings s)
    {
        _settings = s;
        bool dark = s.EffectiveThemeMode == 1;

        if (dark)
        {
            C_Bg = Color.FromRgb(0x1E, 0x1E, 0x1E);
            C_Surface = Color.FromRgb(0x2D, 0x2D, 0x2D);
            C_SurfaceHover = Color.FromRgb(0x3A, 0x3A, 0x3A);
            C_Sidebar = Color.FromRgb(0x25, 0x25, 0x25);
            C_SidebarFooter = Color.FromRgb(0x33, 0x33, 0x33);
            C_TextPrimary = Color.FromRgb(0xE0, 0xE0, 0xE0);
            C_TextSecondary = Color.FromRgb(0x99, 0x99, 0x99);
            C_TextMuted = Color.FromRgb(0x77, 0x77, 0x77);
            C_Accent = Color.FromRgb(0x60, 0xCD, 0xFF);
            C_Danger = Color.FromRgb(0xFF, 0x6B, 0x6B);
            C_Border = Color.FromRgb(0x40, 0x40, 0x40);
            C_DialogBg = Color.FromRgb(0x2D, 0x2D, 0x2D);
            C_InputBg = Color.FromRgb(0x3A, 0x3A, 0x3A);
        }
        else
        {
            C_Bg = Color.FromRgb(0xF5, 0xF5, 0xF5);
            C_Surface = Color.FromRgb(0xFF, 0xFF, 0xFF);
            C_SurfaceHover = Color.FromRgb(0xF0, 0xF0, 0xF0);
            C_Sidebar = Color.FromRgb(0xFA, 0xFA, 0xFA);
            C_SidebarFooter = Color.FromRgb(0xF0, 0xF0, 0xF0);
            C_TextPrimary = Color.FromRgb(0x1A, 0x1A, 0x1A);
            C_TextSecondary = Color.FromRgb(0x88, 0x88, 0x88);
            C_TextMuted = Color.FromRgb(0xAA, 0xAA, 0xAA);
            C_Accent = Color.FromRgb(0x00, 0x78, 0xD4);
            C_Danger = Color.FromRgb(0xE7, 0x48, 0x56);
            C_Border = Color.FromRgb(0xE0, 0xE0, 0xE0);
            C_DialogBg = Color.FromRgb(0xFF, 0xFF, 0xFF);
            C_InputBg = Color.FromRgb(0xF5, 0xF5, 0xF5);
        }

        this.Width = s.PanelWidth;
        this.Height = s.PanelHeight;
        this.Opacity = s.Opacity;

        if (!string.IsNullOrEmpty(s.BackgroundImagePath) && File.Exists(s.BackgroundImagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(s.BackgroundImagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                BgImageBorder.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                // Make MainBorder and child backgrounds semi-transparent to show the image
                MainBorder.Background = new SolidColorBrush(Color.FromArgb(40, C_Bg.R, C_Bg.G, C_Bg.B));
                TitleBarBg.Background = new SolidColorBrush(Color.FromArgb(200, C_Sidebar.R, C_Sidebar.G, C_Sidebar.B));
                SearchBarBg.Background = new SolidColorBrush(Color.FromArgb(200, C_Bg.R, C_Bg.G, C_Bg.B));
                SidebarBg.Background = new SolidColorBrush(Color.FromArgb(200, C_Sidebar.R, C_Sidebar.G, C_Sidebar.B));
                SidebarFooterBg.Background = new SolidColorBrush(Color.FromArgb(200, C_SidebarFooter.R, C_SidebarFooter.G, C_SidebarFooter.B));
                FileGridBg.Background = new SolidColorBrush(Color.FromArgb(200, C_Bg.R, C_Bg.G, C_Bg.B));
                StatusBarBg.Background = new SolidColorBrush(Color.FromArgb(200, C_SidebarFooter.R, C_SidebarFooter.G, C_SidebarFooter.B));
            }
            catch
            {
                BgImageBorder.Background = null;
                MainBorder.Background = new SolidColorBrush(
                    Color.FromArgb((byte)(s.Opacity * 255), C_Bg.R, C_Bg.G, C_Bg.B));
            }
        }
        else
        {
            BgImageBorder.Background = null;
            MainBorder.Background = new SolidColorBrush(
                Color.FromArgb((byte)(s.Opacity * 255), C_Bg.R, C_Bg.G, C_Bg.B));
            // Restore opaque backgrounds
            TitleBarBg.Background = new SolidColorBrush(C_Sidebar);
            SearchBarBg.Background = new SolidColorBrush(C_Bg);
            SidebarBg.Background = new SolidColorBrush(C_Sidebar);
            SidebarFooterBg.Background = new SolidColorBrush(C_SidebarFooter);
            FileGridBg.Background = new SolidColorBrush(C_Bg);
            StatusBarBg.Background = new SolidColorBrush(C_SidebarFooter);
        }

        // Apply dark mode to title bar via DWM
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var hwnd = helper.EnsureHandle();
            int darkMode = dark ? 1 : 0;
            Native.Win32.DwmSetWindowAttribute(hwnd,
                Native.Win32.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref darkMode, sizeof(int));
        }
        catch { }

        if (_isShown)
        {
            RenderCategories();
            RenderFiles();
        }
    }

    public void SetHotkeyService(HotkeyService service)
    {
        _hotkeyService = service;
    }

    public void ToggleVisibility()
    {
        if (_isShown)
            Hide();
        else
            ShowWindow();
    }

    public new void Show()
    {
        base.Show();
        _isShown = true;
    }

    public new void Hide()
    {
        // Save position before hiding
        if (_isShown)
        {
            _savedLeft = Left;
            _savedTop = Top;
            _hasPosition = true;
        }
        base.Hide();
        _isShown = false;
        DropOverlay.Visibility = Visibility.Collapsed;
        CategoryPickerOverlay.Visibility = Visibility.Collapsed;
    }

    private void ShowWindow()
    {
        _vm.LoadData();
        RenderCategories();
        RenderFiles();
        if (_hasPosition)
            RestorePosition();
        else
            CenterWindow();
        Show();
        Activate();
        TxtSearch.Focus();
        TxtSearch.SelectAll();
    }

    private void CenterWindow()
    {
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;
        Left = (sw - Width) / 2;
        Top = (sh - Height) / 3;
    }

    private void BottomRightWindow()
    {
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;
        Left = sw - Width - 20;
        Top = sh - Height - 60;
    }

    private void RestorePosition()
    {
        // Clamp to screen bounds
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;
        Left = Math.Max(0, Math.Min(_savedLeft, sw - Width));
        Top = Math.Max(0, Math.Min(_savedTop, sh - Height));
    }

    // ── Edge snap auto-hide ────────────────────────────

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            CheckEdgeSnap();
        }
    }

    private void CheckEdgeSnap()
    {
        const double snapThreshold = 30;
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;

        bool nearLeft = Left <= snapThreshold;
        bool nearRight = Left + Width >= sw - snapThreshold;
        bool nearTop = Top <= snapThreshold;
        bool nearBottom = Top + Height >= sh - snapThreshold;

        if (nearLeft) _snapEdge = SnapEdge.Left;
        else if (nearRight) _snapEdge = SnapEdge.Right;
        else if (nearTop) _snapEdge = SnapEdge.Top;
        else if (nearBottom) _snapEdge = SnapEdge.Bottom;
        else _snapEdge = SnapEdge.None;

        if (_snapEdge != SnapEdge.None)
        {
            _edgeMode = true;
            _edgeModeSide = _snapEdge;
            Hide();
            StartEdgeHoverTimer();
        }
        else
        {
            _edgeMode = false;
            _edgeModeSide = SnapEdge.None;
        }
    }

    private void StartEdgeHoverTimer()
    {
        _edgeHoverTimer?.Stop();
        _edgeHoverTimer = new System.Windows.Threading.DispatcherTimer
        { Interval = TimeSpan.FromMilliseconds(200) };
        _edgeHoverTimer.Tick += (_, _) => CheckEdgeHover();
        _edgeHoverTimer.Start();
    }

    private void CheckEdgeHover()
    {
        if (_isShown || _edgeModeSide == SnapEdge.None)
        {
            _edgeHoverTimer?.Stop();
            return;
        }

        var mousePos = System.Windows.Forms.Cursor.Position;
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;
        const int edgeZone = 5; // pixels from edge to trigger

        bool atEdge = _edgeModeSide switch
        {
            SnapEdge.Left => mousePos.X <= edgeZone,
            SnapEdge.Right => mousePos.X >= (int)sw - edgeZone,
            SnapEdge.Top => mousePos.Y <= edgeZone,
            SnapEdge.Bottom => mousePos.Y >= (int)sh - edgeZone,
            _ => false
        };

        if (atEdge)
        {
            _edgeHoverTimer?.Stop();
            ShowWindow();
        }
    }

    // ── Acrylic backdrop ──────────────────────────────

    private void ApplyAcrylicBackdrop()
    {
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var hwnd = helper.EnsureHandle();

            int backdropType = 2; // Mica
            Native.Win32.DwmSetWindowAttribute(hwnd,
                Native.Win32.DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdropType, sizeof(int));

            int cornerPref = Native.Win32.DWMWCP_ROUND;
            Native.Win32.DwmSetWindowAttribute(hwnd,
                Native.Win32.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref cornerPref, sizeof(int));

            // Light mode (0 = light, 1 = dark)
            int darkMode = 0;
            Native.Win32.DwmSetWindowAttribute(hwnd,
                Native.Win32.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref darkMode, sizeof(int));
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsDialog();
    }

    private void BtnRestoreSize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Normal;
        Width = _settings.PanelWidth;
        Height = _settings.PanelHeight;
        BottomRightWindow();
        _edgeMode = false;
        _edgeModeSide = SnapEdge.None;
        _edgeHoverTimer?.Stop();
    }

    // ── 5-click easter egg ──────────────────────────────

    private void TitleMrQ_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        _titleClickCount++;
        if (_titleClickCount == 1)
        {
            _titleClickTimer?.Stop();
            _titleClickTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(2) };
            _titleClickTimer.Tick += (_, _) => { _titleClickCount = 0; _titleClickTimer.Stop(); };
            _titleClickTimer.Start();
        }
        if (_titleClickCount >= 5)
        {
            _titleClickCount = 0;
            _titleClickTimer?.Stop();
            ShowAiUnlockDialog();
        }
    }

    private void ShowAiUnlockDialog()
    {
        var dialog = new Window
        {
            Title = "访问验证", Width = 360, Height = 220,
            WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(C_DialogBg),
            BorderBrush = new SolidColorBrush(C_Border),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 20, ShadowDepth = 2, Opacity = 0.12, Color = Colors.Black }
        };
        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock
        {
            Text = "🔑 输入访问码", Foreground = new SolidColorBrush(C_TextPrimary),
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
            FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12)
        });
        var codeTb = new TextBox
        {
            Background = new SolidColorBrush(C_InputBg),
            Foreground = new SolidColorBrush(C_TextPrimary),
            CaretBrush = new SolidColorBrush(C_Accent),
            BorderThickness = new Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
            FontSize = 14, Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 16)
        };
        stack.Children.Add(codeTb);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var cancelBtn = new Button
        {
            Content = "取消",
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
            FontSize = 12, Padding = new Thickness(14, 6, 14, 6),
            Foreground = new SolidColorBrush(C_TextSecondary),
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancelBtn.Click += (_, _) => dialog.Close();
        btnPanel.Children.Add(cancelBtn);

        var confirmBtn = new Button
        {
            Content = "确认",
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
            FontSize = 12, Padding = new Thickness(14, 6, 14, 6),
            Background = new SolidColorBrush(C_Accent),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        confirmBtn.Click += (_, _) =>
        {
            if (_unlockDialogProcessing) return;
            _unlockDialogProcessing = true;
            try
            {
                if (codeTb.Text == "Mr.Q")
                {
                    var s = SettingsService.Current;
                    s.AiMode = true;
                    SettingsService.Save(s);
                    _settings = s;
                    System.Windows.MessageBox.Show("AI 智能分类已开启！\n下次收纳桌面时将使用 AI 分析文件名并自动分类。",
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    dialog.Close();
                }
                else
                {
                    System.Windows.MessageBox.Show("访问码错误", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                _unlockDialogProcessing = false;
            }
        };
        btnPanel.Children.Add(confirmBtn);
        stack.Children.Add(btnPanel);
        border.Child = stack;
        dialog.Content = border;
        dialog.ShowDialog();
    }

    // ── Search ────────────────────────────────────────

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _vm.SearchText = TxtSearch.Text;
        RenderFiles();
    }

    // ── Category rendering ────────────────────────────

    private void RenderCategories()
    {
        CategoryPanel.Children.Clear();

        var allBtn = CreateCategoryButton(new Category { Name = "全部", Color = "#0078D4", Id = "" },
            _vm.SelectedCategory == null);
        CategoryPanel.Children.Add(allBtn);

        foreach (var cat in _vm.Categories)
        {
            var btn = CreateCategoryButton(cat, _vm.SelectedCategory?.Id == cat.Id);
            CategoryPanel.Children.Add(btn);
        }
    }

    private Border CreateCategoryButton(Category cat, bool isSelected)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = isSelected ? new SolidColorBrush(C_SurfaceHover) : Brushes.Transparent,
            Margin = new Thickness(0, 1, 0, 1),
            Cursor = Cursors.Hand,
            Tag = cat
        };

        var grid = new Grid { Margin = new Thickness(10, 8, 10, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Border
        {
            Width = 10, Height = 10,
            CornerRadius = new CornerRadius(5),
            Background = ParseColorBrush(cat.Color),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(dot, 0);
        grid.Children.Add(dot);

        var nameLabel = new TextBlock
        {
            Text = cat.Name,
            Foreground = isSelected
                ? new SolidColorBrush(C_TextPrimary)
                : new SolidColorBrush(C_TextSecondary),
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameLabel, 2);
        grid.Children.Add(nameLabel);

        var count = _vm.Files.Count(f => cat.Id == "" || f.CategoryId == cat.Id);
        var countLabel = new TextBlock
        {
            Text = cat.Id == "" ? count.ToString() : cat.FileCount.ToString(),
            Foreground = new SolidColorBrush(C_TextMuted),
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        Grid.SetColumn(countLabel, 3);
        grid.Children.Add(countLabel);

        border.Child = grid;

        border.MouseLeftButtonDown += (s, e) =>
        {
            _vm.SelectedCategory = cat.Id == "" ? null : cat;
            RenderCategories();
            RenderFiles();
        };

        if (cat.Id != "")
        {
            border.MouseRightButtonDown += (s, e) => ShowCategoryContextMenu(cat, border);

            border.AllowDrop = true;
            border.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.Copy;
                    border.Background = new SolidColorBrush(C_Surface);
                }
            };
            border.DragLeave += (s, e) =>
            {
                border.Background = isSelected
                    ? new SolidColorBrush(C_SurfaceHover) : Brushes.Transparent;
            };
            border.Drop += (s, e) =>
            {
                border.Background = isSelected
                    ? new SolidColorBrush(C_SurfaceHover) : Brushes.Transparent;
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    foreach (var file in files)
                        _vm.AddFileFromDrop(file, cat);
                    RenderCategories();
                    RenderFiles();
                }
            };
        }

        return border;
    }

    private void ShowCategoryContextMenu(Category cat, Border target)
    {
        var menu = new ContextMenu();
        menu.Background = new SolidColorBrush(C_DialogBg);
        menu.Foreground = new SolidColorBrush(C_TextPrimary);
        menu.BorderBrush = new SolidColorBrush(C_Border);

        var editItem = new MenuItem { Header = "编辑分类" };
        editItem.Click += (s, e) => EditCategory(cat);
        menu.Items.Add(editItem);

        var deleteItem = new MenuItem { Header = "删除分类", Foreground = new SolidColorBrush(C_Danger) };
        deleteItem.Click += (s, e) => _vm.DeleteCategoryCommand.Execute(cat);
        menu.Items.Add(deleteItem);

        menu.IsOpen = true;
    }

    private void EditCategory(Category cat)
    {
        var dialog = new Window
        {
            Title = "编辑分类",
            Width = 340, Height = 240,
            WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, Topmost = true
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(C_DialogBg),
            BorderBrush = new SolidColorBrush(C_Border),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 20, ShadowDepth = 2, Opacity = 0.12, Color = Colors.Black }
        };

        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock
        {
            Text = "编辑分类", Foreground = new SolidColorBrush(C_TextPrimary),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 14, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var nameTb = CreateDialogTextBox(cat.Name);
        stack.Children.Add(nameTb);

        var colors = new[] { "#0078D4", "#16A34A", "#CA8A04", "#E74856", "#7C3AED", "#0891B2", "#DB2777", "#4F46E5" };
        var colorPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        string selectedColor = cat.Color;
        foreach (var c in colors)
        {
            var dot = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
                Background = ParseColorBrush(c),
                Margin = new Thickness(2), Cursor = Cursors.Hand, Tag = c,
                BorderBrush = c == selectedColor
                    ? new SolidColorBrush(C_TextPrimary) : new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(2)
            };
            dot.MouseLeftButtonDown += (s, e) =>
            {
                selectedColor = c;
                foreach (Border child in colorPanel.Children)
                    child.BorderBrush = child.Tag.ToString() == c
                        ? new SolidColorBrush(C_TextPrimary) : new SolidColorBrush(Colors.Transparent);
            };
            colorPanel.Children.Add(dot);
        }
        stack.Children.Add(colorPanel);

        var btnPanel = DialogButtonPanel(
            ("取消", () => dialog.Close()),
            ("保存", () =>
            {
                cat.Name = string.IsNullOrWhiteSpace(nameTb.Text) ? cat.Name : nameTb.Text;
                cat.Color = selectedColor;
                _vm.EditCategoryCommand.Execute(cat);
                RenderCategories();
                dialog.Close();
            }));
        stack.Children.Add(btnPanel);

        border.Child = stack;
        dialog.Content = border;
        dialog.ShowDialog();
    }

    // ── File rendering ────────────────────────────────

    private void RenderFiles()
    {
        FileGridPanel.Children.Clear();
        _vm.ApplyFilter();
        foreach (var entry in _vm.FilteredFiles)
            FileGridPanel.Children.Add(CreateFileCard(entry));
        UpdateStatus();
    }

    private Border CreateFileCard(FileEntry entry)
    {
        var card = new Border
        {
            Width = 140, Height = 110,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(C_Surface),
            Margin = new Thickness(4),
            Cursor = Cursors.Hand,
            Tag = entry,
            ToolTip = entry.Tooltip
        };

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var catColor = _vm.Categories.FirstOrDefault(c => c.Id == entry.CategoryId)?.Color ?? "#0078D4";
        var iconBorder = new Border
        {
            Width = 56, Height = 56,
            CornerRadius = new CornerRadius(8),
            Background = ParseColorBrush(catColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 4)
        };

        if (entry.IsDirectory)
        {
            // Folder icon
            var iconText = new TextBlock
            {
                Text = "", // Segoe MDL2 folder icon
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;
        }
        else
        {
            var realIcon = GetFileIcon(entry.StoredPath);
            if (realIcon != null)
            {
                var img = new Image
                {
                    Source = realIcon,
                    Width = 42, Height = 42,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                iconBorder.Background = Brushes.Transparent;
                iconBorder.Child = img;
            }
            else
            {
                var iconText = new TextBlock
                {
                    Text = GetFileTypeIcon(entry.FileName),
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconBorder.Child = iconText;
            }
        }
        stack.Children.Add(iconBorder);

        // Display name: for folders show full name, for files strip extension
        string displayName;
        if (entry.IsDirectory)
        {
            displayName = entry.FileName.Length > 18 ? entry.FileName[..15] + "..." : entry.FileName;
        }
        else
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(entry.FileName);
            displayName = nameWithoutExt.Length > 18 ? nameWithoutExt[..15] + "..." : nameWithoutExt;
        }
        var nameLabel = new TextBlock
        {
            Text = displayName,
            Foreground = entry.IsMissing
                ? new SolidColorBrush(C_Danger)
                : new SolidColorBrush(C_TextPrimary),
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0)
        };
        stack.Children.Add(nameLabel);

        card.Child = stack;

        card.MouseEnter += (s, e) =>
            card.Background = new SolidColorBrush(C_SurfaceHover);
        card.MouseLeave += (s, e) =>
            card.Background = new SolidColorBrush(C_Surface);
        card.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2) _vm.OpenFileCommand.Execute(entry);
        };
        card.MouseRightButtonDown += (s, e) => ShowFileContextMenu(entry, card);

        // Drag out support
        card.MouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && !entry.IsMissing)
            {
                // Hide window to avoid blocking system dialogs
                var wasShown = _isShown;
                Hide();

                var data = new DataObject(DataFormats.FileDrop, new[] { entry.StoredPath });
                var result = DragDrop.DoDragDrop(card, data, DragDropEffects.Copy | DragDropEffects.Move);

                // If file was moved/copied successfully, remove from list
                if (result == DragDropEffects.Move || result == DragDropEffects.Copy)
                {
                    _vm.DeleteFile(entry);
                    RenderFiles();
                    RenderCategories();
                }

                // Restore window if it was shown before
                if (wasShown)
                    ShowWindow();
            }
        };

        return card;
    }

    private void ShowFileContextMenu(FileEntry entry, Border target)
    {
        var menu = new ContextMenu();
        menu.Background = new SolidColorBrush(C_DialogBg);
        menu.Foreground = new SolidColorBrush(C_TextPrimary);
        menu.BorderBrush = new SolidColorBrush(C_Border);

        var openItem = new MenuItem { Header = "打开" };
        openItem.Click += (s, e) => _vm.OpenFileCommand.Execute(entry);
        menu.Items.Add(openItem);

        var copyPathItem = new MenuItem { Header = "复制路径" };
        copyPathItem.Click += (s, e) => _vm.CopyPathCommand.Execute(entry);
        menu.Items.Add(copyPathItem);

        menu.Items.Add(new Separator());

        var moveItem = new MenuItem { Header = "移动到" };
        foreach (var cat in _vm.Categories.Where(c => c.Id != entry.CategoryId))
        {
            var catItem = new MenuItem { Header = cat.Name };
            catItem.Click += (s, e) =>
            {
                try
                {
                    FileOperationService.MoveFileToCategory(entry, cat);
                    DataService.UpdateFile(entry);
                    _vm.LoadData();
                    RenderCategories();
                    RenderFiles();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"移动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            moveItem.Items.Add(catItem);
        }
        menu.Items.Add(moveItem);

        menu.Items.Add(new Separator());

        var renameItem = new MenuItem { Header = "重命名" };
        renameItem.Click += (s, e) => RenameFile(entry);
        menu.Items.Add(renameItem);

        var deleteItem = new MenuItem { Header = "删除", Foreground = new SolidColorBrush(C_Danger) };
        deleteItem.Click += (s, e) =>
        {
            var result = MessageBox.Show($"确定删除 \"{entry.FileName}\" 吗？\n此操作不可恢复。",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _vm.DeleteFile(entry);
                RenderFiles();
                RenderCategories();
            }
        };
        menu.Items.Add(deleteItem);

        menu.IsOpen = true;
    }

    private void RenameFile(FileEntry entry)
    {
        var dialog = new Window
        {
            Title = "重命名", Width = 340, Height = 180,
            WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, Topmost = true
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(C_DialogBg),
            BorderBrush = new SolidColorBrush(C_Border),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 20, ShadowDepth = 2, Opacity = 0.12, Color = Colors.Black }
        };

        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock
        {
            Text = "重命名文件", Foreground = new SolidColorBrush(C_TextPrimary),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 14, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var ext = Path.GetExtension(entry.FileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(entry.FileName);
        var nameTb = CreateDialogTextBox(nameWithoutExt);
        nameTb.SelectAll();
        stack.Children.Add(nameTb);

        var btnPanel = DialogButtonPanel(
            ("取消", () => dialog.Close()),
            ("确定", () =>
            {
                var newName = nameTb.Text.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != nameWithoutExt)
                {
                    // Append original extension
                    var fullName = newName + ext;
                    if (FileOperationService.RenameFile(entry, fullName))
                    {
                        DataService.UpdateFile(entry);
                        _vm.LoadData();
                        RenderFiles();
                    }
                }
                dialog.Close();
            }));
        stack.Children.Add(btnPanel);

        border.Child = stack;
        dialog.Content = border;
        dialog.Loaded += (s, e) => { nameTb.Focus(); nameTb.SelectAll(); };
        dialog.ShowDialog();
    }

    // ── Drag & Drop ────────────────────────────────────

    private void FileGrid_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Visibility = Visibility.Visible;
        }
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void FileGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FileGrid_DragLeave(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private void FileGrid_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length == 0) return;

        _pendingDropFiles = files.ToList();
        var targetCat = _vm.SelectedCategory ?? _vm.Categories.FirstOrDefault();
        if (targetCat != null && _vm.Categories.Count > 0)
            AddFilesToCategory(targetCat);
        else
            MessageBox.Show("请先创建分类。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        e.Handled = true;
    }

    private void AddFilesToCategory(Category cat)
    {
        foreach (var file in _pendingDropFiles)
            _vm.AddFileFromDrop(file, cat);
        _pendingDropFiles.Clear();
        RenderCategories();
        RenderFiles();
    }

    private void BtnCancelCategoryPick_Click(object sender, RoutedEventArgs e)
    {
        CategoryPickerOverlay.Visibility = Visibility.Collapsed;
        _pendingDropFiles.Clear();
    }

    // ── Collect Desktop ────────────────────────────────

    private async void BtnCollectDesktop_Click(object sender, RoutedEventArgs e)
    {
        await _vm.CollectDesktopAsync();
        RenderCategories();
        RenderFiles();
    }

    private void BtnRestoreDesktop_Click(object sender, RoutedEventArgs e)
    {
        _vm.RestoreToDesktop();
        RenderCategories();
        RenderFiles();
    }

    // ── Add Category ───────────────────────────────────

    private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "添加分类", Width = 340, Height = 240,
            WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, Topmost = true
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(C_DialogBg),
            BorderBrush = new SolidColorBrush(C_Border),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 20, ShadowDepth = 2, Opacity = 0.12, Color = Colors.Black }
        };

        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock
        {
            Text = "添加分类", Foreground = new SolidColorBrush(C_TextPrimary),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 14, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var nameTb = CreateDialogTextBox("新分类");
        stack.Children.Add(nameTb);

        var colors = new[] { "#0078D4", "#16A34A", "#CA8A04", "#E74856", "#7C3AED", "#0891B2", "#DB2777", "#4F46E5" };
        var colorPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        string selectedColor = "#0078D4";
        foreach (var c in colors)
        {
            var dot = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
                Background = ParseColorBrush(c),
                Margin = new Thickness(2), Cursor = Cursors.Hand, Tag = c,
                BorderBrush = c == selectedColor
                    ? new SolidColorBrush(C_TextPrimary) : new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(2)
            };
            dot.MouseLeftButtonDown += (s, e) =>
            {
                selectedColor = c;
                foreach (Border child in colorPanel.Children)
                    child.BorderBrush = child.Tag.ToString() == c
                        ? new SolidColorBrush(C_TextPrimary) : new SolidColorBrush(Colors.Transparent);
            };
            colorPanel.Children.Add(dot);
        }
        stack.Children.Add(colorPanel);

        var btnPanel = DialogButtonPanel(
            ("取消", () => dialog.Close()),
            ("创建", () =>
            {
                var name = nameTb.Text.Trim();
                if (string.IsNullOrEmpty(name)) name = "新分类";
                _vm.AddCategory(name, selectedColor);
                RenderCategories();
                dialog.Close();
            }));
        stack.Children.Add(btnPanel);

        border.Child = stack;
        dialog.Content = border;
        dialog.Loaded += (s, e) => { nameTb.Focus(); nameTb.SelectAll(); };
        dialog.ShowDialog();
    }

    // ── Dialog Helpers ─────────────────────────────────

    private TextBox CreateDialogTextBox(string text)
    {
        var tb = new TextBox
        {
            Text = text,
            Background = new SolidColorBrush(C_InputBg),
            Foreground = new SolidColorBrush(C_TextPrimary),
            CaretBrush = new SolidColorBrush(C_Accent),
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 13,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 12)
        };
        tb.Resources.Add(typeof(Border), new Style(typeof(Border))
        {
            Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(6)) }
        });
        return tb;
    }

    private StackPanel DialogButtonPanel(params (string text, Action action)[] buttons)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            var (text, action) = buttons[i];
            Button btn;
            if (i == buttons.Length - 1) // Last button = primary/accent
            {
                btn = new Button
                {
                    Content = text, Style = (Style)FindResource("AccentButton"),
                    FontSize = 12, Padding = new Thickness(14, 6, 14, 6)
                };
            }
            else
            {
                btn = new Button
                {
                    Content = text, Style = (Style)FindResource("IconButton"),
                    FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
                    Foreground = new SolidColorBrush(C_TextSecondary),
                    Margin = new Thickness(0, 0, 8, 0)
                };
            }
            btn.Click += (s, e) => action();
            panel.Children.Add(btn);
        }
        return panel;
    }

    // ── Settings ────────────────────────────────────────

    private int _pendingHotkeyMods;
    private uint _pendingHotkeyKey;
    private string _pendingStoragePath = "";

    private void ShowSettingsDialog()
    {
        var s = SettingsService.Current;
        _pendingHotkeyMods = s.HotkeyModifiers;
        _pendingHotkeyKey = s.HotkeyKey;
        _pendingStoragePath = s.StoragePath;

        var dialog = new Window
        {
            Title = "设置", Width = 440, Height = 580,
            WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this, Topmost = true
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(C_DialogBg),
            BorderBrush = new SolidColorBrush(C_Border),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 20, ShadowDepth = 2, Opacity = 0.12, Color = Colors.Black }
        };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Margin = new Thickness(20) };

        // Title
        stack.Children.Add(new TextBlock
        {
            Text = "⚙ 设置 · Mr.Q", Foreground = new SolidColorBrush(C_TextPrimary),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 16,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16)
        });

        // ── Appearance Section ──
        stack.Children.Add(SectionHeader("外观"));

        // Theme mode
        stack.Children.Add(SettingLabel("主题模式"));
        int selectedTheme = s.ThemeMode;
        var themePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var themes = new[] { "浅色", "深色", "跟随系统" };
        for (int i = 0; i < themes.Length; i++)
        {
            var idx = i;
            var rb = new RadioButton
            {
                Content = themes[i], IsChecked = selectedTheme == i, GroupName = "ThemeMode",
                Foreground = new SolidColorBrush(C_TextPrimary),
                FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
                Margin = new Thickness(0, 0, 12, 0),
                Tag = idx
            };
            rb.Checked += (_, _) => selectedTheme = idx;
            themePanel.Children.Add(rb);
        }
        stack.Children.Add(themePanel);

        // Accent color
        stack.Children.Add(SettingLabel("主色调"));
        string selectedAccent = s.AccentColor;
        var accentPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        var accentColors = new[] { "#0078D4", "#16A34A", "#CA8A04", "#E74856", "#7C3AED", "#0891B2", "#DB2777", "#4F46E5" };
        foreach (var c in accentColors)
        {
            var dot = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(14),
                Background = ParseColorBrush(c),
                Margin = new Thickness(2), Cursor = Cursors.Hand, Tag = c,
                BorderBrush = c == selectedAccent
                    ? new SolidColorBrush(C_TextPrimary) : new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(2)
            };
            dot.MouseLeftButtonDown += (_, _) =>
            {
                selectedAccent = c;
                foreach (Border child in accentPanel.Children)
                    child.BorderBrush = child.Tag.ToString() == c
                        ? new SolidColorBrush(C_TextPrimary) : new SolidColorBrush(Colors.Transparent);
            };
            accentPanel.Children.Add(dot);
        }
        stack.Children.Add(accentPanel);

        // Opacity
        stack.Children.Add(SettingLabel("面板透明度"));
        var opacityPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var opacitySlider = new Slider
        {
            Minimum = 0.5, Maximum = 1.0, Value = s.Opacity,
            TickFrequency = 0.05, IsSnapToTickEnabled = true,
            Width = 240, VerticalAlignment = VerticalAlignment.Center
        };
        var opacityLabel = new TextBlock
        {
            Text = s.Opacity.ToString("F2"), Width = 40, TextAlignment = TextAlignment.Right,
            Foreground = new SolidColorBrush(C_TextSecondary),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        opacitySlider.ValueChanged += (_, _) => opacityLabel.Text = opacitySlider.Value.ToString("F2");
        opacityPanel.Children.Add(opacitySlider);
        DockPanel.SetDock(opacityLabel, Dock.Right);
        opacityPanel.Children.Add(opacityLabel);
        stack.Children.Add(opacityPanel);

        // Background image
        stack.Children.Add(SettingLabel("背景图片"));
        string pendingBgImagePath = s.BackgroundImagePath;
        var bgPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var bgPathTb = new TextBox
        {
            Text = string.IsNullOrEmpty(s.BackgroundImagePath) ? "（默认毛玻璃）" : Path.GetFileName(s.BackgroundImagePath),
            Background = new SolidColorBrush(C_InputBg),
            Foreground = new SolidColorBrush(C_TextSecondary),
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
            Padding = new Thickness(8, 6, 8, 6),
            IsReadOnly = true
        };
        var bgBrowseBtn = new Button
        {
            Content = "选择图片",
            Style = (Style)FindResource("IconButton"),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 11,
            Foreground = new SolidColorBrush(C_Accent),
            Padding = new Thickness(8, 6, 8, 6)
        };
        bgBrowseBtn.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp",
                Title = "选择背景图片"
            };
            if (dlg.ShowDialog() == true)
            {
                pendingBgImagePath = dlg.FileName;
                bgPathTb.Text = Path.GetFileName(dlg.FileName);
            }
        };
        var bgClearBtn = new Button
        {
            Content = "清除",
            Style = (Style)FindResource("IconButton"),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 11,
            Foreground = new SolidColorBrush(C_TextSecondary),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(4, 0, 0, 0)
        };
        bgClearBtn.Click += (_, _) =>
        {
            pendingBgImagePath = "";
            bgPathTb.Text = "（默认毛玻璃）";
        };
        bgPanel.Children.Add(bgClearBtn);
        DockPanel.SetDock(bgClearBtn, Dock.Right);
        bgPanel.Children.Add(bgBrowseBtn);
        DockPanel.SetDock(bgBrowseBtn, Dock.Right);
        bgPanel.Children.Add(bgPathTb);
        stack.Children.Add(bgPanel);

        // ── Behavior Section ──
        stack.Children.Add(SectionHeader("行为"));

        // Hotkey
        stack.Children.Add(SettingLabel("快捷键"));
        var hotkeyPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var hotkeyBtn = new Button
        {
            Content = HotkeyToString(s.HotkeyModifiers, s.HotkeyKey),
            Style = (Style)FindResource("IconButton"),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
            Foreground = new SolidColorBrush(C_Accent),
            Padding = new Thickness(12, 6, 12, 6)
        };
        hotkeyBtn.Click += (_, _) =>
        {
            hotkeyBtn.Content = "按下组合键...";
            hotkeyBtn.Focus();
            KeyEventHandler handler = null!;
            handler = (ss, args) =>
            {
                var mods = args.KeyboardDevice.Modifiers;
                var key = args.Key == Key.System ? args.SystemKey : args.Key;
                if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                    or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
                if (mods == ModifierKeys.None) return;
                _pendingHotkeyMods = ModifierKeysToWin32(mods);
                _pendingHotkeyKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                hotkeyBtn.Content = HotkeyToString(_pendingHotkeyMods, _pendingHotkeyKey);
                dialog.PreviewKeyDown -= handler;
                args.Handled = true;
            };
            dialog.PreviewKeyDown += handler;
        };
        hotkeyPanel.Children.Add(hotkeyBtn);
        stack.Children.Add(hotkeyPanel);

        // Auto-start
        var autoStartCb = new CheckBox
        {
            Content = "开机自动启动", IsChecked = s.AutoStart,
            Foreground = new SolidColorBrush(C_TextPrimary),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(autoStartCb);

        // Storage path
        stack.Children.Add(SettingLabel("文件存储路径"));
        var pathPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var pathTb = new TextBox
        {
            Text = s.StoragePath,
            Background = new SolidColorBrush(C_InputBg),
            Foreground = new SolidColorBrush(C_TextPrimary),
            CaretBrush = new SolidColorBrush(C_Accent),
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
            Padding = new Thickness(8, 6, 8, 6)
        };
        var browseBtn = new Button
        {
            Content = "浏览",
            Style = (Style)FindResource("IconButton"),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 11,
            Foreground = new SolidColorBrush(C_Accent),
            Padding = new Thickness(8, 6, 8, 6)
        };
        browseBtn.Click += (_, _) =>
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = pathTb.Text,
                ShowNewFolderButton = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                pathTb.Text = dlg.SelectedPath;
        };
        pathPanel.Children.Add(browseBtn);
        DockPanel.SetDock(browseBtn, Dock.Right);
        pathPanel.Children.Add(pathTb);
        stack.Children.Add(pathPanel);

        // ── Panel Section ──
        stack.Children.Add(SectionHeader("面板"));

        // Panel width
        stack.Children.Add(SettingLabel("面板宽度"));
        var widthPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var widthSlider = new Slider
        {
            Minimum = 600, Maximum = 1400, Value = s.PanelWidth,
            TickFrequency = 20, IsSnapToTickEnabled = true,
            Width = 240, VerticalAlignment = VerticalAlignment.Center
        };
        var widthLabel = new TextBlock
        {
            Text = ((int)s.PanelWidth).ToString() + "px", Width = 50, TextAlignment = TextAlignment.Right,
            Foreground = new SolidColorBrush(C_TextSecondary),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        widthSlider.ValueChanged += (_, _) => widthLabel.Text = ((int)widthSlider.Value).ToString() + "px";
        widthPanel.Children.Add(widthSlider);
        DockPanel.SetDock(widthLabel, Dock.Right);
        widthPanel.Children.Add(widthLabel);
        stack.Children.Add(widthPanel);

        // Panel height
        stack.Children.Add(SettingLabel("面板高度"));
        var heightPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var heightSlider = new Slider
        {
            Minimum = 400, Maximum = 1000, Value = s.PanelHeight,
            TickFrequency = 20, IsSnapToTickEnabled = true,
            Width = 240, VerticalAlignment = VerticalAlignment.Center
        };
        var heightLabel = new TextBlock
        {
            Text = ((int)s.PanelHeight).ToString() + "px", Width = 50, TextAlignment = TextAlignment.Right,
            Foreground = new SolidColorBrush(C_TextSecondary),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        heightSlider.ValueChanged += (_, _) => heightLabel.Text = ((int)heightSlider.Value).ToString() + "px";
        heightPanel.Children.Add(heightSlider);
        DockPanel.SetDock(heightLabel, Dock.Right);
        heightPanel.Children.Add(heightLabel);
        stack.Children.Add(heightPanel);

        // ── AI Section (only visible after Mr.Q unlock) ──
        // Declare pending variables outside the if block so save/restore buttons can access them
        string pendingOpenAiKey = s.OpenAiKey;
        string pendingAiBaseUrl = s.AiBaseUrl;
        string pendingAiModel = s.AiModel;
        bool pendingAiMode = s.AiMode;
        TextBox? aiKeyTb = null;
        TextBox? aiBaseUrlTb = null;
        TextBox? aiModelTb = null;
        CheckBox? aiModeCb = null;

        if (_settings.AiMode)
        {
            stack.Children.Add(SectionHeader("高级"));

            stack.Children.Add(SettingLabel("AI API Key"));
            aiKeyTb = new TextBox
            {
                Text = s.OpenAiKey,
                Background = new SolidColorBrush(C_InputBg),
                Foreground = new SolidColorBrush(C_TextSecondary),
                CaretBrush = new SolidColorBrush(C_Accent),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 8)
            };
            aiKeyTb.TextChanged += (_, _) => pendingOpenAiKey = aiKeyTb.Text;
            stack.Children.Add(aiKeyTb);

            stack.Children.Add(SettingLabel("AI Base URL"));
            aiBaseUrlTb = new TextBox
            {
                Text = s.AiBaseUrl,
                Background = new SolidColorBrush(C_InputBg),
                Foreground = new SolidColorBrush(C_TextSecondary),
                CaretBrush = new SolidColorBrush(C_Accent),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 8)
            };
            aiBaseUrlTb.TextChanged += (_, _) => pendingAiBaseUrl = aiBaseUrlTb.Text;
            stack.Children.Add(aiBaseUrlTb);

            stack.Children.Add(SettingLabel("AI Model"));
            aiModelTb = new TextBox
            {
                Text = s.AiModel,
                Background = new SolidColorBrush(C_InputBg),
                Foreground = new SolidColorBrush(C_TextSecondary),
                CaretBrush = new SolidColorBrush(C_Accent),
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 8)
            };
            aiModelTb.TextChanged += (_, _) => pendingAiModel = aiModelTb.Text;
            stack.Children.Add(aiModelTb);

            aiModeCb = new CheckBox
            {
                Content = "AI 智能分类",
                IsChecked = s.AiMode,
                Foreground = new SolidColorBrush(C_TextPrimary),
                FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
                Margin = new Thickness(0, 0, 0, 12)
            };
            aiModeCb.Checked += (_, _) => pendingAiMode = true;
            aiModeCb.Unchecked += (_, _) => pendingAiMode = false;
            stack.Children.Add(aiModeCb);
        }

        // ── Buttons ──
        var btnPanel = DialogButtonPanel(
            ("恢复默认", () =>
            {
                var def = AppSettings.Default;
                opacitySlider.Value = def.Opacity;
                selectedTheme = def.ThemeMode;
                selectedAccent = def.AccentColor;
                _pendingHotkeyMods = def.HotkeyModifiers;
                _pendingHotkeyKey = def.HotkeyKey;
                hotkeyBtn.Content = HotkeyToString(def.HotkeyModifiers, def.HotkeyKey);
                autoStartCb.IsChecked = def.AutoStart;
                pathTb.Text = def.StoragePath;
                widthSlider.Value = def.PanelWidth;
                heightSlider.Value = def.PanelHeight;
                foreach (var child in themePanel.Children)
                    if (child is RadioButton rb) rb.IsChecked = (int)rb.Tag == def.ThemeMode;
                foreach (Border child in accentPanel.Children)
                    child.BorderBrush = child.Tag.ToString() == def.AccentColor
                        ? new SolidColorBrush(C_TextPrimary) : new SolidColorBrush(Colors.Transparent);
                selectedAccent = def.AccentColor;
                if (aiKeyTb != null) aiKeyTb.Text = "";
                if (aiBaseUrlTb != null) aiBaseUrlTb.Text = "";
                if (aiModelTb != null) aiModelTb.Text = "";
                if (aiModeCb != null) aiModeCb.IsChecked = false;
                pendingBgImagePath = "";
                bgPathTb.Text = "（默认毛玻璃）";
            }),
            ("取消", () => dialog.Close()),
            ("保存", () =>
            {
                var newSettings = new AppSettings
                {
                    ThemeMode = selectedTheme,
                    AccentColor = selectedAccent,
                    Opacity = Math.Round(opacitySlider.Value, 2),
                    HotkeyModifiers = _pendingHotkeyMods,
                    HotkeyKey = _pendingHotkeyKey,
                    AutoStart = autoStartCb.IsChecked ?? false,
                    StoragePath = pathTb.Text.Trim(),
                    PanelWidth = Math.Round(widthSlider.Value),
                    PanelHeight = Math.Round(heightSlider.Value),
                    OpenAiKey = pendingOpenAiKey,
                    AiBaseUrl = pendingAiBaseUrl,
                    AiModel = pendingAiModel,
                    AiMode = pendingAiMode,
                    BackgroundImagePath = pendingBgImagePath
                };

                // Migrate files if storage path changed
                string oldPath = s.StoragePath;
                string newPath = newSettings.StoragePath;
                if (!string.IsNullOrEmpty(newPath) && oldPath != newPath)
                {
                    Directory.CreateDirectory(newPath);
                    if (Directory.Exists(oldPath) && Directory.GetFiles(oldPath).Length > 0)
                    {
                        var result = MessageBox.Show(
                            $"是否将已有文件从旧路径迁移到新路径？\n\n旧路径: {oldPath}\n新路径: {newPath}",
                            "迁移文件", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            foreach (var file in Directory.GetFiles(oldPath))
                            {
                                var dest = Path.Combine(newPath, Path.GetFileName(file));
                                File.Move(file, dest, overwrite: true);
                            }
                            foreach (var dir in Directory.GetDirectories(oldPath))
                            {
                                var dest = Path.Combine(newPath, Path.GetFileName(dir));
                                if (!Directory.Exists(dest))
                                    Directory.Move(dir, dest);
                            }
                        }
                    }
                }

                SettingsService.Save(newSettings);
                _settings = newSettings;
                ApplyTheme(newSettings);
                _hotkeyService?.ReRegister(newSettings.HotkeyModifiers, newSettings.HotkeyKey);

                // Auto-start registry
                ApplyAutoStart(newSettings.AutoStart);

                // Update data file paths if storage changed
                if (oldPath != newPath)
                {
                    var allFiles = DataService.GetFiles();
                    foreach (var f in allFiles)
                    {
                        if (f.StoredPath.StartsWith(oldPath, StringComparison.OrdinalIgnoreCase))
                            f.StoredPath = newPath + f.StoredPath.Substring(oldPath.Length);
                    }
                }

                dialog.Close();
            }));
        stack.Children.Add(btnPanel);

        scroll.Content = stack;
        border.Child = scroll;
        dialog.Content = border;
        dialog.ShowDialog();
    }

    private static TextBlock SectionHeader(string text)
    {
        return new TextBlock
        {
            Text = text, Foreground = new SolidColorBrush(C_Accent),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 8)
        };
    }

    private static TextBlock SettingLabel(string text)
    {
        return new TextBlock
        {
            Text = text, Foreground = new SolidColorBrush(C_TextPrimary),
            FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4)
        };
    }

    private static int ModifierKeysToWin32(ModifierKeys mods)
    {
        int result = 0;
        if (mods.HasFlag(ModifierKeys.Alt)) result |= 1;   // MOD_ALT
        if (mods.HasFlag(ModifierKeys.Control)) result |= 2; // MOD_CONTROL
        if (mods.HasFlag(ModifierKeys.Shift)) result |= 4;   // MOD_SHIFT
        if (mods.HasFlag(ModifierKeys.Windows)) result |= 8;  // MOD_WIN
        return result;
    }

    private static string HotkeyToString(int mods, uint key)
    {
        var parts = new List<string>();
        if ((mods & 1) != 0) parts.Add("Alt");
        if ((mods & 2) != 0) parts.Add("Ctrl");
        if ((mods & 4) != 0) parts.Add("Shift");
        if ((mods & 8) != 0) parts.Add("Win");
        parts.Add(((Key)KeyInterop.KeyFromVirtualKey((int)key)).ToString());
        return string.Join(" + ", parts);
    }

    private static void ApplyAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (enable)
                key?.SetValue("DeskPanel", Environment.ProcessPath);
            else
                key?.DeleteValue("DeskPanel", throwOnMissingValue: false);
        }
        catch { }
    }

    // ── Helpers ────────────────────────────────────────

    private void UpdateStatus()
    {
        TbStatus.Text = $"共 {_vm.Files.Count} 个文件，显示 {_vm.FilteredFiles.Count} 个";
    }

    private static SolidColorBrush ParseColorBrush(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)); }
    }

    private static string GetFileTypeIcon(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLower();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" => "",
            ".doc" or ".docx" => "",
            ".xls" or ".xlsx" => "",
            ".ppt" or ".pptx" => "",
            ".pdf" => "",
            ".mp3" or ".wav" or ".flac" or ".aac" => "",
            ".mp4" or ".mkv" or ".avi" or ".mov" => "",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "",
            ".txt" or ".md" or ".log" => "",
            ".exe" or ".msi" or ".bat" or ".cmd" => "",
            ".cs" or ".py" or ".js" or ".ts" or ".java" or ".json" or ".xml" => "",
            _ => "",
        };
    }

    private static BitmapSource? GetFileIcon(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            var flags = Native.Win32.SHGFI_LARGEICON | Native.Win32.SHGFI_ICON;
            var shfi = new Native.Win32.SHFILEINFO();
            var cbFileInfo = (uint)System.Runtime.InteropServices.Marshal.SizeOf(shfi);

            var result = Native.Win32.SHGetFileInfo(
                path, 0, ref shfi, cbFileInfo, flags);

            if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
                return null;

            using var icon = System.Drawing.Icon.FromHandle(shfi.hIcon);
            var bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

            Native.Win32.DestroyIcon(shfi.hIcon);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
