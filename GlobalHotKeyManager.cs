using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
// Ми не потребуємо using System.Windows.Forms, 
// оскільки наш App.xaml.cs вже має його для NotifyIcon

public static class GlobalHotKeyManager
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;
    public const int HOTKEY_ID = 9000;
    public const uint MOD_NONE = 0x0000;
    public const uint VK_F12 = 0x7B;

    private static HwndSource? _source; // Робимо nullable (виправляє попередження CS8618)
    private static bool _isRegistered = false;

    public static void RegisterHotKey(Window window)
    {
        if (_isRegistered) return;

        IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle);
        _source!.AddHook(HwndHook); // _source не буде null тут

        if (!RegisterHotKey(handle, HOTKEY_ID, MOD_NONE, VK_F12))
        {
            // === ВИПРАВЛЕННЯ CS0104 ===
            // Явно вказуємо, що це WPF MessageBox
            System.Windows.MessageBox.Show("Не вдалося зареєструвати гарячу клавішу F12.", "Помилка");
        }
        _isRegistered = true;
    }

    public static void UnregisterHotKey()
    {
        if (!_isRegistered || _source == null) return;

        _source.RemoveHook(HwndHook);
        
        // === ВИПРАВЛЕННЯ CS0104 ===
        // Явно вказуємо, що це WPF Application
        if (System.Windows.Application.Current != null && System.Windows.Application.Current.MainWindow != null)
        {
             IntPtr handle = new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
             UnregisterHotKey(handle, HOTKEY_ID);
        }
        _isRegistered = false;
    }

    private static IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            OnHotKeyPressed?.Invoke(null, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public static event EventHandler? OnHotKeyPressed; // Робимо nullable (виправляє попередження CS8618)
}
