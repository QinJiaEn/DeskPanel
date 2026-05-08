using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    // ── White theme colors ─────────────────────────────
    private static readonly Color C_Bg = Color.FromRgb(0xF5, 0xF5, 0xF5);
    private static readonly Color C_Surface = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color C_SurfaceHover = Color.FromRgb(0xF0, 0xF0, 0xF0);
    private static readonly Color C_Sidebar = Color.FromRgb(0xFA, 0xFA, 0xFA);
    private static readonly Color C_SidebarFooter = Color.FromRgb(0xF0, 0xF0, 0xF0);
    private static readonly Color C_TextPrimary = Color.FromRgb(0x1A, 0x1A, 0x1A);
    private static readonly Color C_TextSecondary = Color.FromRgb(0x88, 0x88, 0x88);
    private static readonly Color C_TextMuted = Color.FromRgb(0xAA, 0xAA, 0xAA);
    private static readonly Color C_Accent = Color.FromRgb(0x00, 0x78, 0xD4);
    private static readonly Color C_Danger = Color.FromRgb(0xE7, 0x48, 0x56);
    private static readonly Color C_Border = Color.FromRgb(0xE0, 0xE0, 0xE0);
    private static readonly Color C_DialogBg = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color C_InputBg = Color.FromRgb(0xF5, 0xF5, 0xF5);

    public MainWindow()
    {
        InitializeComponent();
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
        if (!_hasPosition)
            CenterWindow();
        else
            RestorePosition();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Hide();
    }

    private void Window_Deactivated(object sender, EventArgs e) { }

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
        if (e.ChangedButton == MouseButton.Left)
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

        if (nearLeft || nearRight || nearTop || nearBottom)
        {
            Hide();
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
            Width = 110, Height = 90,
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
            Width = 32, Height = 32,
            CornerRadius = new CornerRadius(6),
            Background = ParseColorBrush(catColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 4)
        };
        var realIcon = GetFileIcon(entry.StoredPath);
        if (realIcon != null)
        {
            var img = new Image
            {
                Source = realIcon,
                Width = 24, Height = 24,
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
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;
        }
        stack.Children.Add(iconBorder);

        var displayName = entry.FileName.Length > 14 ? entry.FileName[..11] + "..." : entry.FileName;
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

        var nameTb = CreateDialogTextBox(entry.FileName);
        nameTb.SelectAll();
        stack.Children.Add(nameTb);

        var btnPanel = DialogButtonPanel(
            ("取消", () => dialog.Close()),
            ("确定", () =>
            {
                var newName = nameTb.Text.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != entry.FileName)
                {
                    if (FileOperationService.RenameFile(entry, newName))
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

    private void BtnCollectDesktop_Click(object sender, RoutedEventArgs e)
    {
        _vm.CollectDesktop();
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
            var flags = Native.Win32.SHGFI_SMALLICON | Native.Win32.SHGFI_ICON;
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
