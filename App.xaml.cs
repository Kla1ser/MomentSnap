using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MomentSnap
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon _notifyIcon;
        private MainWindow _hiddenWindow;
        private ScreenCapture _capture;
        private bool _isBusy = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _hiddenWindow = new MainWindow();
            _capture = new ScreenCapture();

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = new System.Drawing.Icon("icon.ico");
            _notifyIcon.Text = "Moment Snap";
            _notifyIcon.Visible = true;

            _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            _notifyIcon.ContextMenuStrip.Items.Add("Вихід", null, OnExitClicked);

            GlobalHotKeyManager.RegisterHotKey(_hiddenWindow);
            GlobalHotKeyManager.OnHotKeyPressed += OnHotKeyHandler;
        }

        private async void OnHotKeyHandler(object sender, EventArgs e)
        {
            if (_isBusy) return;
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

                _notifyIcon.ShowBalloonTip(1000, "Moment Snap", "Знімок скопійовано!", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _notifyIcon.ShowBalloonTip(2000, "Moment Snap - Помилка", ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            GlobalHotKeyManager.UnregisterHotKey();
            _notifyIcon.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _notifyIcon?.Dispose();
            GlobalHotKeyManager.UnregisterHotKey();
            base.OnExit(e);
        }
    }
}
