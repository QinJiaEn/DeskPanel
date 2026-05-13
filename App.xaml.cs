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
        // Global exception handler to prevent silent crashes (WinExe Release mode)
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"发生未处理的错误:\n\n{ex?.Message}\n\n{ex?.StackTrace}",
                "DeskPanel 错误", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"发生未处理的错误:\n\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "DeskPanel 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
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

            // Ensure storage directories exist (wrap in try-catch for invalid paths on other machines)
            try { Directory.CreateDirectory(settings.StoragePath); }
            catch (Exception ex) { MessageBox.Show($"存储路径无效，请在设置中修改:\n{settings.StoragePath}\n\n{ex.Message}", "DeskPanel 警告", MessageBoxButton.OK, MessageBoxImage.Warning); }
            try { Directory.CreateDirectory(Path.GetDirectoryName(DataService.DataFilePath)!); }
            catch { }

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
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show($"DeskPanel 启动失败:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "DeskPanel 启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void CreateNotifyIcon()
    {
        // Load icon from embedded resource
        Icon appIcon;
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("DeskPanel.Resources.icon.ico"))
        {
            if (stream != null)
                appIcon = new Icon(stream);
            else
                appIcon = SystemIcons.Application;
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = appIcon,
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
        // Mutex is auto-released when process exits; manual ReleaseMutex can crash
        // if called from wrong thread or after Shutdown has already torn down state.
    }
}
