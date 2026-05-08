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
    private int _modifiers;
    private uint _key;

    public event Action? HotkeyPressed;

    public HotkeyService(IntPtr hwnd, int modifiers, uint key)
    {
        _hwnd = hwnd;
        _modifiers = modifiers;
        _key = key;
    }

    public void Register()
    {
        if (_registered) return;

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        bool ok = Win32.RegisterHotKey(_hwnd, 1, (uint)_modifiers, _key);
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

    public void ReRegister(int modifiers, uint key)
    {
        _modifiers = modifiers;
        _key = key;
        if (_registered)
        {
            Unregister();
            Register();
        }
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
