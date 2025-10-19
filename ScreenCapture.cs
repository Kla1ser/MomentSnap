using System;
using System.Drawing; // Для GDI+ Bitmap
using System.Drawing.Imaging; // Для GDI+ PixelFormat
using System.IO; // Для MemoryStream
using System.Threading.Tasks; // Для Task
using System.Windows.Media.Imaging; // Для WPF BitmapSource

// Явно використовуємо System.Windows.Forms для доступу до Screen
using WinForms = System.Windows.Forms;

namespace MomentSnap
{
    /// <summary>
    /// Швидка GDI+ реалізація для захоплення екрана (за вашою пропозицією).
    /// Це виправляє всі помилки D3D/Vortice і дозволяє проєкту зібратися.
    /// </summary>
    public class ScreenCapture
    {
        // Робимо поле nullable, щоб виправити попередження CS8618
        private GraphicsCaptureItem? _captureItem; 

        // Ми залишаємо метод async, щоб відповідати App.xaml.cs,
        // але всередині він буде синхронним.
        public Task<BitmapSource> TakeSnapshotAsync()
        {
            // Використовуємо логіку GDI+ CopyFromScreen
            var bounds = WinForms.Screen.PrimaryScreen.Bounds;
            var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            // Конвертуємо GDI+ Bitmap у WPF BitmapSource (який потрібен оверлею)
            using (var memory = new MemoryStream())
            {
                bmp.Save(memory, ImageFormat.Bmp); // Зберігаємо у потік
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory; // Завантажуємо з потоку
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Важливо для передачі між потоками
                
                bmp.Dispose(); // Очищуємо GDI+ об'єкт
                
                // Повертаємо готовий WPF-сумісний знімок
                return Task.FromResult<BitmapSource>(bitmapImage);
            }
        }
    }
}
