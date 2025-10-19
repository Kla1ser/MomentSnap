using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MomentSnap
{
    /// <summary>
    /// "Контракт", який описує будь-який сервіс захоплення.
    /// </summary>
    public interface ICaptureService
    {
        /// <summary>
        /// Асинхронно робить знімок екрана.
        /// </summary>
        /// <returns>BitmapSource, сумісний з WPF.</returns>
        Task<BitmapSource?> TakeSnapshotAsync();

        /// <summary>
        /// Дозволяє користувачеві вибрати ціль для захоплення (якщо потрібно).
        /// </summary>
        Task PickCaptureTargetAsync();
    }
}
