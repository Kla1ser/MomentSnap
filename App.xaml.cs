using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MomentSnap
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _hiddenWindow;
        private ScreenCapture? _capture;
        private bool _isBusy = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _hiddenWindow = new MainWindow();
            _capture = new ScreenCapture();

            _notifyIcon = new NotifyIcon();

            // === ВИПРАВЛЕННЯ (Try...Catch) ===
            // Ми "ловимо" помилку, якщо icon.ico пошкоджений.
            // Це дозволить програмі продовжити працювати, навіть якщо іконки немає.
            try
            {
                _notifyIcon.Icon = new System.Drawing.Icon("icon.ico");
                _notifyIcon.Text = "Moment Snap";
                _notifyIcon.Visible = true;
            }
            catch (Exception ex)
            {
                // Якщо іконка зламана, просто покажемо помилку, але НЕ будемо "падати".
                System.Windows.MessageBox.Show(
                    "Не вдалося завантажити icon.ico. " +
                    "Програма працюватиме без іконки в треї. " +
                    "Помилка: " + ex.Message, 
                    "Помилка іконки");
                
                // Ми все одно робимо іконку "видимою", 
                // щоб можна було додати до неї меню "Вихід".
                _notifyIcon.Visible = true; 
            }
            // ==================================


            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Вихід", null, OnExitClicked);

            GlobalHotKeyManager.RegisterHotKey(_hiddenWindow);
            GlobalHotKeyManager.OnHotKeyPressed += OnHotKeyHandler;
        }

        // === ВИПРАВЛЕННЯ CS8622 ===
        // Додаємо '?' до 'object', щоб відповідати делегату EventHandler
        private void OnExitClicked(object? sender, EventArgs e)
        {
            GlobalHotKeyManager.UnregisterHotKey();
            _notifyIcon?.Dispose(); // Додаємо '?' про всяк випадок
            Shutdown();
        }

        // === ВИПРАВЛЕННЯ CS8622 ===
        // Додаємо '?' до 'object', щоб відповідати делегату EventHandler
        private async void OnHotKeyHandler(object? sender, EventArgs e)
        {
            if (_isBusy || _capture == null) return; // Додаємо перевірку на null
            _isBusy = true;

            try
            {
                BitmapSource fullScreenshot = await _capture.TakeSnapshotAsync();
                if (fullScreenshot == null) return; 

                var overlayWindow = new CaptureOverlayWindow(fullScreenshot);
                overlayWindow.ShowDialog(); 

                var selectionRect = overlayWindow.Selection;

                if (selectionRect.IsEmpty || selectionRect.Width <= 1 || selectionRect.Height <= 1)
                    return;

                var dpi = VisualTreeHelper.GetDpi(overlayWindow);
                var physicalRect = new Int32Rect(
                    (int)(selectionRect.X * dpi.DpiScaleX),
                    (int)(selectionRect.Y * dpi.DpiScaleY),
                    (int)(selectionRect.Width * dpi.DpiScaleX),
                    (int)(selectionRect.Height * dpi.DpiScaleY)
                );

                var croppedBitmap = new CroppedBitmap(fullScreenshot, physicalRect);
                croppedBitmap.Freeze();

                System.Windows.Clipboard.SetImage(croppedBitmap);

                _notifyIcon?.ShowBalloonTip(1000, "Moment Snap", "Знімок скопійовано!", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _notifyIcon?.ShowBalloonTip(2000, "Moment Snap - Помилка", ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                _isBusy = false;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            GlobalHotKeyManager.UnregisterHotKey();
            base.OnExit(e);
        }
    }
}
