using System;
using System.Windows;
using System.Windows.Input; // KeyEventArgs, MouseEventArgs, etc.
using System.Windows.Media.Imaging;

namespace MomentSnap
{
    public partial class CaptureOverlayWindow : Window
    {
        // Використовуємо явний тип System.Windows.Rect
        public System.Windows.Rect Selection { get; private set; } = System.Windows.Rect.Empty;

        // Використовуємо явний тип System.Windows.Point
        private System.Windows.Point _startPoint;
        private bool _isSelecting = false;

        public CaptureOverlayWindow(BitmapSource screenshot)
        {
            InitializeComponent();
            ScreenshotImage.Source = screenshot;
            FullScreenGeometry.Rect = new System.Windows.Rect(0, 0, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        }

        // Явно вказуємо тип System.Windows.Input.KeyEventArgs
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Selection = System.Windows.Rect.Empty; // Явно вказуємо System.Windows.Rect
                this.Close();
            }
        }

        // Явно вказуємо тип System.Windows.Input.MouseButtonEventArgs
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isSelecting = true;
            _startPoint = e.GetPosition(this); // e.GetPosition() повертає System.Windows.Point
            this.CaptureMouse();
        }

        // Явно вказуємо тип System.Windows.Input.MouseEventArgs
        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isSelecting) return;
            // currentPoint буде типу System.Windows.Point
            System.Windows.Point currentPoint = e.GetPosition(this);
            var rect = new System.Windows.Rect(_startPoint, currentPoint); // Явно вказуємо System.Windows.Rect
            SelectionGeometry.Rect = rect;
        }

        // Явно вказуємо тип System.Windows.Input.MouseButtonEventArgs
        private void Window_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;
            this.ReleaseMouseCapture();
            Selection = SelectionGeometry.Rect;
            this.Close();
        }
    }
}
