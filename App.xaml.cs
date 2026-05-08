using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using DeskPanel.Native;
using DeskPanel.Services;
using MessageBox = System.Windows.MessageBox;

namespace DeskPanel;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private HotkeyService? _hotkeyService;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Single instance check
        _mutex = new Mutex(true, "DeskPanel_SingleInstance_8A7F3D2C");
        if (!_mutex.WaitOne(0, false))
        {
            MessageBox.Show("DeskPanel 已经在运行中。\n请按 Alt+` 呼出面板。", "DeskPanel",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Ensure storage directories exist
        Directory.CreateDirectory(@"F:\DeskPanel\files");
        Directory.CreateDirectory(Path.GetDirectoryName(DataService.DataFilePath)!);

        // Create main window (hidden initially)
        _mainWindow = new MainWindow();
        _mainWindow.Show();  // Show briefly to get HWND, then hide
        _mainWindow.Hide();

        // Set up system tray
        CreateNotifyIcon();

        // Register global hotkey after window handle is available
        var helper = new WindowInteropHelper(_mainWindow);
        var hwnd = helper.EnsureHandle();
        _hotkeyService = new HotkeyService(hwnd);
        _hotkeyService.HotkeyPressed += () =>
        {
            Dispatcher.Invoke(() => _mainWindow.ToggleVisibility());
        };
        _hotkeyService.Register();

        // Pass hotkey unregistration to window closing
        _mainWindow.Closed += (_, _) =>
        {
            _hotkeyService.Unregister();
            _notifyIcon?.Dispose();
            _mutex?.ReleaseMutex();
        };
    }

    private void CreateNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "DeskPanel - Alt+` 呼出面板",
            Visible = true
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _mainWindow?.ToggleVisibility();
        };
        _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add("显示面板", null, (_, _) =>
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
        });
        _notifyIcon.ContextMenuStrip.Items.Add("退出", null, (_, _) =>
        {
            _hotkeyService?.Unregister();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _mutex?.ReleaseMutex();
            Shutdown();
        });
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _hotkeyService?.Unregister();
        _notifyIcon?.Visible = false;
        _notifyIcon?.Dispose();
        _mutex?.ReleaseMutex();
    }
}
