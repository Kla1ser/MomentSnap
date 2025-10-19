using System;
using System.Drawing; 
using System.Drawing.Imaging;
using System.IO; 
using System.Threading.Tasks; 
using System.Windows.Media.Imaging; 

using WinForms = System.Windows.Forms;

namespace MomentSnap
{
    /// <summary>
    /// GDI+ реалізація, яка тепер відповідає "контракту" ICaptureService.
    /// </summary>
    public class GdiCapture : ICaptureService // <-- Вказуємо "контракт"
    {
        // Реалізація методу з контракту
        public Task<BitmapSource?> TakeSnapshotAsync()
        {
            var screen = WinForms.Screen.PrimaryScreen;
            if (screen == null)
            {
                screen = WinForms.Screen.AllScreens[0]; 
            }
            var bounds = screen.Bounds;
            
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
                
                // Ми повинні повертати Task<BitmapSource?>, а не Task<BitmapSource>
                return Task.FromResult<BitmapSource?>(bitmapImage);
            }
        }
        
        // Реалізація методу з контракту
        public Task PickCaptureTargetAsync()
        {
            // GDI+ не потребує вибору цілі.
            return Task.CompletedTask;
        }
    }
}
