using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT.Interop;
using System.Windows.Interop;

// ==========================================================
// ОСЬ ВИПРАВЛЕННЯ (ВАШ ВАРІАНТ А):
// Використовуємо Vortice замість SharpDX
using Vortice.Direct3D11;
using Vortice.DXGI;
// ==========================================================


// Оголошення необхідних COM-інтерфейсів для Interop
[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDirect3DDXGISurface
{
    // Vortice.DXGI.SurfaceDescription
    void GetDesc(out SurfaceDescription pDesc);
}

namespace MomentSnap
{
    public class ScreenCapture
    {
        private GraphicsCaptureItem _captureItem;
        
        // Використовуємо типи Vortice
        private ID3D11Device _d3dDevice; // WinRT
        private Vortice.Direct3D11.ID3D11Device _vorticeDevice; // Vortice

        public ScreenCapture()
        {
            // Створюємо пристрій Vortice
            D3D11.D3D11CreateDevice(
                null, // Адаптер (null = за замовчуванням)
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                null, // Feature levels (null = default)
                out _vorticeDevice).CheckError();
            
            // Створюємо пристрій WinRT (для FramePool) з пристрою Vortice
            _d3dDevice = CreateDirect3D11DeviceFromVorticeDevice(_vorticeDevice);
        }

        // Допоміжна функція для "склеювання" Vortice та WinRT
        private ID3D11Device CreateDirect3D11DeviceFromVorticeDevice(Vortice.Direct3D11.ID3D11Device device)
        {
            using (var dxgiDevice = device.QueryInterface<IDXGIDevice>())
            {
                return (ID3D11Device)GraphicsCaptureAccess.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer);
            }
        }

        public async Task PickCaptureTargetAsync()
        {
            var picker = new GraphicsCapturePicker();
            IntPtr hwnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            InitializeWithWindow.Initialize(picker, hwnd);
            
            _captureItem = await picker.PickTargetAsync();
        }

        public async Task<BitmapSource> TakeSnapshotAsync()
        {
            if (_captureItem == null)
            {
                await PickCaptureTargetAsync();
                if (_captureItem == null) return null;
            }

            Direct3D11CaptureFrame frame = null;
            BitmapSource wpfBitmap = null;

            using (var framePool = Direct3D11CaptureFramePool.Create(_d3dDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _captureItem.Size))
            using (var session = framePool.CreateCaptureSession(_captureItem))
            {
                var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>();

                framePool.FrameArrived += (s, a) =>
                {
                    tcs.SetResult(s.TryGetNextFrame());
                };

                session.StartCapture();
                frame = await tcs.Task;
            }

            if (frame == null) return null;

            try
            {
                // Використовуємо Vortice.Direct3D11.ID3D11Texture2D
                using (var sourceTexture = GetVorticeTextureFromFrame(frame.Surface))
                {
                    var stagingDesc = sourceTexture.Description;
                    stagingDesc.CpuAccessFlags = CpuAccessFlags.Read;
                    stagingDesc.Usage = ResourceUsage.Staging;
                    stagingDesc.BindFlags = BindFlags.None;
                    stagingDesc.MiscFlags = ResourceMiscFlags.None;

                    using (var stagingTexture = _vorticeDevice.CreateTexture2D(stagingDesc))
                    {
                        _vorticeDevice.ImmediateContext.CopyResource(stagingTexture, sourceTexture);
                        
                        // API маппінгу Vortice трохи відрізняється
                        var mappedSubresource = _vorticeDevice.ImmediateContext.Map(stagingTexture, 0, MapMode.Read, MapFlags.None);

                        wpfBitmap = BitmapSource.Create(
                            sourceTexture.Description.Width,
                            sourceTexture.Description.Height,
                            96, 96,
                            System.Windows.Media.PixelFormats.Bgra32,
                            null,
                            mappedSubresource.DataPointer, // Використовуємо mappedSubresource.DataPointer
                            mappedSubresource.RowPitch * sourceTexture.Description.Height, // Використовуємо RowPitch
                            mappedSubresource.RowPitch
                        );

                        wpfBitmap.Freeze();
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

        // Отримуємо Vortice.Direct3D11.ID3D11Texture2D
        private ID3D11Texture2D GetVorticeTextureFromFrame(IDirect3DSurface surface)
        {
            var access = (IDirect3DDXGISurface)surface;
            access.GetDesc(out var desc);
            
            // Використовуємо WinRT Interop для отримання вказівника
            IntPtr texturePtr;
            using (var dxgiSurface = surface.QueryInterface<Vortice.DXGI.IDXGISurface>())
            {
                texturePtr = dxgiSurface.NativePointer;
            }

            // Створюємо об'єкт Vortice з вказівника
            return new ID3D11Texture2D(texturePtr);
        }
    }
}
