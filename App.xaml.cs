using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using DeskPanel.Models;
using DeskPanel.Native;
using DeskPanel.Services;
using Microsoft.Win32;
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

        // Load settings
        var settings = SettingsService.Current;

        // Apply auto-start
        ApplyAutoStart(settings.AutoStart);

        // Ensure storage directories exist
        Directory.CreateDirectory(settings.StoragePath);
        Directory.CreateDirectory(Path.GetDirectoryName(DataService.DataFilePath)!);

        // Create main window with settings
        _mainWindow = new MainWindow(settings);
        _mainWindow.Show();  // Show briefly to get HWND, then hide
        _mainWindow.Hide();

        // Set up system tray
        CreateNotifyIcon();

        // Register global hotkey after window handle is available
        var helper = new WindowInteropHelper(_mainWindow);
        var hwnd = helper.EnsureHandle();
        _hotkeyService = new HotkeyService(hwnd, settings.HotkeyModifiers, settings.HotkeyKey);
        _mainWindow.SetHotkeyService(_hotkeyService);
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

    private void ApplyAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (enable)
                key?.SetValue("DeskPanel", Environment.ProcessPath!);
            else
                key?.DeleteValue("DeskPanel", throwOnMissingValue: false);
        }
        catch { }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _hotkeyService?.Unregister();
        _notifyIcon?.Visible = false;
        _notifyIcon?.Dispose();
        _mutex?.ReleaseMutex();
    }
}
