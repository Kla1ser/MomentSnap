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
    public class ScreenCapture
    {
        public Task<BitmapSource> TakeSnapshotAsync()
        {
            // === ВИПРАВЛЕННЯ CS8602 ===
            // Додаємо null-guard, як ви й запропонували.
            var screen = WinForms.Screen.PrimaryScreen;
            if (screen == null)
            {
                // Failsafe для середовищ без PrimaryScreen (наприклад, CI runner)
                screen = WinForms.Screen.AllScreens[0]; 
            }
            var bounds = screen.Bounds;
            // ==========================
            
            var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            using (var memory = new MemoryStream())
            {
                bmp.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); 
                
                bmp.Dispose(); 
                
                return Task.FromResult<BitmapSource>(bitmapImage);
            }
        }
        
        public Task PickCaptureTargetAsync()
        {
            return Task.CompletedTask;
        }
    }
}
