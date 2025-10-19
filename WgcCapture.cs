using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT.Interop; // "Клей" вбудований в SDK!
using System.Windows.Interop;

// Використовуємо Vortice замість SharpDX
using Vortice.Direct3D11;
using Vortice.DXGI;

// Оголошення COM-інтерфейсу для "склеювання"
[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDirect3DDXGISurface
{
    void GetDesc(out SurfaceDescription pDesc);
}

namespace MomentSnap
{
    /// <summary>
    /// Сучасна реалізація захоплення через Windows Graphics Capture (WGC)
    /// з використанням Vortice. Працює з іграми.
    /// </summary>
    public class WgcCapture : ICaptureService
    {
        private GraphicsCaptureItem? _captureItem;
        
        private ID3D11Device _d3dDevice; // WinRT пристрій (для FramePool)
        private Vortice.Direct3D11.ID3D11Device _vorticeDevice; // Vortice пристрій (для копіювання)

        public WgcCapture()
        {
            // Створюємо пристрій Vortice
            D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                null, 
                out _vorticeDevice).CheckError();
            
            // Створюємо пристрій WinRT з пристрою Vortice
            _d3dDevice = CreateDirect3D11DeviceFromVorticeDevice(_vorticeDevice);
        }

        // Допоміжна функція "склеювання"
        private ID3D11Device CreateDirect3D11DeviceFromVorticeDevice(Vortice.Direct3D11.ID3D11Device device)
        {
            using (var dxgiDevice = device.QueryInterface<IDXGIDevice>())
            {
                // Це "клей" з WinRT.Interop
                return (ID3D11Device)GraphicsCaptureAccess.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer);
            }
        }

        // Реалізація з контракту
        public async Task PickCaptureTargetAsync()
        {
            var picker = new GraphicsCapturePicker();
            // Нам потрібен Hwnd нашого невидимого вікна
            IntPtr hwnd = new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
            InitializeWithWindow.Initialize(picker, hwnd);
            
            _captureItem = await picker.PickTargetAsync();
        }

        // Реалізація з контракту
        public async Task<BitmapSource?> TakeSnapshotAsync()
        {
            // Якщо ми ніколи не питали дозвіл (перший запуск)
            if (_captureItem == null)
            {
                await PickCaptureTargetAsync();
                // Якщо користувач скасував вибір
                if (_captureItem == null) return null;
            }

            Direct3D11CaptureFrame? frame = null;
            BitmapSource? wpfBitmap = null;

            // Створюємо пул і сесію
            using (var framePool = Direct3D11CaptureFramePool.Create(_d3dDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _captureItem.Size))
            using (var session = framePool.CreateCaptureSession(_captureItem))
            {
                var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>();

                // Підписуємося на ОДИН кадр
                framePool.FrameArrived += (s, a) =>
                {
                    tcs.SetResult(s.TryGetNextFrame());
                };

                session.StartCapture();
                frame = await tcs.Task; // Чекаємо на кадр
            } // Сесія і пул тут автоматично зупиняться (Dispose)

            if (frame == null) return null;

            try
            {
                // Отримуємо текстуру (на GPU) з кадру
                using (var sourceTexture = GetVorticeTextureFromFrame(frame.Surface))
                {
                    // Створюємо "проміжну" текстуру в пам'яті CPU
                    var stagingDesc = sourceTexture.Description;
                    stagingDesc.CpuAccessFlags = CpuAccessFlags.Read;
                    stagingDesc.Usage = ResourceUsage.Staging;
                    stagingDesc.BindFlags = BindFlags.None;
                    stagingDesc.MiscFlags = ResourceMiscFlags.None;

                    using (var stagingTexture = _vorticeDevice.CreateTexture2D(stagingDesc))
                    {
                        // Копіюємо з GPU -> CPU
                        _vorticeDevice.ImmediateContext.CopyResource(stagingTexture, sourceTexture);
                        
                        // "Мапимо" текстуру в пам'яті, щоб отримати до неї доступ
                        var mappedSubresource = _vorticeDevice.ImmediateContext.Map(stagingTexture, 0, MapMode.Read, MapFlags.None);

                        // Створюємо WPF BitmapSource
                        wpfBitmap = BitmapSource.Create(
                            sourceTexture.Description.Width,
                            sourceTexture.Description.Height,
                            96, 96,
                            System.Windows.Media.PixelFormats.Bgra32,
                            null,
                            mappedSubresource.DataPointer,
                            mappedSubresource.RowPitch * sourceTexture.Description.Height, 
                            mappedSubresource.RowPitch
                        );

                        wpfBitmap.Freeze(); // "Заморожуємо"
                        _vorticeDevice.ImmediateContext.Unmap(stagingTexture, 0);
                    }
                }
            }
            finally
            {
                frame?.Dispose();
            }

            return wpfBitmap;
        }

        // Допоміжна функція для отримання Vortice-текстури з WinRT-поверхні
        private ID3D11Texture2D GetVorticeTextureFromFrame(IDirect3DSurface surface)
        {
            var access = (IDirect3DDXGISurface)surface;
            access.GetDesc(out var desc);
            
            IntPtr texturePtr;
            using (var dxgiSurface = surface.QueryInterface<Vortice.DXGI.IDXGISurface>())
            {
                texturePtr = dxgiSurface.NativePointer;
            }

            return new ID3D11Texture2D(texturePtr);
        }
    }
}
