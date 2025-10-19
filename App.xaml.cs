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
        
        // === ЗМІНА ТУТ ===
        // Використовуємо наш новий "контракт"
        private ICaptureService? _capture; 
        // =================
        
        private bool _isBusy = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _hiddenWindow = new MainWindow();
            
            // === ПЕРЕМИКАЧ ===
            // Ми вмикаємо нашу нову WGC-версію!
            _capture = new WgcCapture(); 
            // (Якщо захочемо повернути GDI, просто напишемо: new GdiCapture();)
            // ==================

            _notifyIcon = new NotifyIcon();

            try
            {
                _notifyIcon.Icon = new System.Drawing.Icon("icon.ico");
                _notifyIcon.Text = "Moment Snap";
                _notifyIcon.Visible = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Не вдалося завантажити icon.ico. " +
                    "Програма працюватиме без іконки в треї. " +
                    "Помилка: " + ex.Message, 
                    "Помилка іконки");
                _notifyIcon.Visible = true; 
            }


            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Вихід", null, OnExitClicked);

            GlobalHotKeyManager.RegisterHotKey(_hiddenWindow);
            GlobalHotKeyManager.OnHotKeyPressed += OnHotKeyHandler;
        }

        private void OnExitClicked(object? sender, EventArgs e)
        {
            GlobalHotKeyManager.UnregisterHotKey();
            _notifyIcon?.Dispose(); 
            Shutdown();
        }

        private async void OnHotKeyHandler(object? sender, EventArgs e)
        {
            if (_isBusy || _capture == null) return; 
            _isBusy = true;

            try
            {
                // Все інше залишається без змін!
                BitmapSource? fullScreenshot = await _capture.TakeSnapshotAsync(); 
                if (fullScreenshot == null) 
                {
                    _isBusy = false;
                    return; 
                }

                var overlayWindow = new CaptureOverlayWindow(fullScreenshot);
                overlayWindow.ShowDialog(); 

                var selectionRect = overlayWindow.Selection;

                if (selectionRect.IsEmpty || selectionRect.Width <= 1 || selectionRect.Height <= 1)
                {
                    _isBusy = false;
                    return;
                }
                    
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
