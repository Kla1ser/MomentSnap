using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MomentSnap
{
    public partial class CaptureOverlayWindow : Window
    {
        public Rect Selection { get; private set; } = Rect.Empty;
        private Point _startPoint;
        private bool _isSelecting = false;

        public CaptureOverlayWindow(BitmapSource screenshot)
        {
            InitializeComponent();
            ScreenshotImage.Source = screenshot;
            FullScreenGeometry.Rect = new Rect(0, 0, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Selection = Rect.Empty;
                this.Close();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isSelecting = true;
            _startPoint = e.GetPosition(this);
            this.CaptureMouse();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;
            Point currentPoint = e.GetPosition(this);
            var rect = new Rect(_startPoint, currentPoint);
            SelectionGeometry.Rect = rect;
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;
            this.ReleaseMouseCapture();
            Selection = SelectionGeometry.Rect;
            this.Close();
        }
    }
}
