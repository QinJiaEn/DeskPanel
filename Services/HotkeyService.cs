using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using DeskPanel.Native;

namespace DeskPanel.Services;

public class HotkeyService
{
    private readonly IntPtr _hwnd;
    private HwndSource? _source;
    private bool _registered;

    public event Action? HotkeyPressed;

    public HotkeyService(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public void Register()
    {
        if (_registered) return;

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        // Try Alt+` first
        bool ok = Win32.RegisterHotKey(_hwnd, 1, Win32.MOD_ALT, Win32.VK_OEM_3);
        if (!ok)
        {
            // Fallback: Ctrl+Shift+A
            Win32.RegisterHotKey(_hwnd, 1,
                Win32.MOD_CONTROL | Win32.MOD_SHIFT, Win32.VK_A);
        }
        _registered = true;
    }

    public void Unregister()
    {
        if (!_registered) return;
        Win32.UnregisterHotKey(_hwnd, 1);
        _source?.RemoveHook(WndProc);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == 1)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }
}
