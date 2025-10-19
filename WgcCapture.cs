using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11; // IDirect3DDevice
using WinRT.Interop; 
using System.Windows.Interop;

// === ВИПРАВЛЕННЯ: Додаємо всі потрібні using ===
using Vortice.Direct3D;          // Для DriverType, FeatureLevel
using Vortice.Direct3D11;      // Для ID3D11Device, Texture2DDescription, CpuAccessFlags etc.
using Vortice.DXGI;          // Для IDXGIDevice, IDXGISurface, SurfaceDescription, MapFlags
// ==========================================

// COM-інтерфейс для отримання SurfaceDescription (як і раніше)
[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDirect3DDXGISurface
{
    void GetDesc(out SurfaceDescription pDesc);
}

namespace MomentSnap
{
    public class WgcCapture : ICaptureService
    {
        private GraphicsCaptureItem? _captureItem;
        
        private IDirect3DDevice _winrtDevice; // WinRT пристрій (для FramePool)
        private Vortice.Direct3D11.ID3D11Device _vorticeDevice; // Vortice пристрій (для копіювання)
        private Vortice.Direct3D11.ID3D11DeviceContext _vorticeContext; // Контекст Vortice

        // === ВИПРАВЛЕННЯ: Додаємо P/Invoke для CreateDirect3D11DeviceFromDXGIDevice ===
        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern HResult CreateDirect3D11DeviceFromDXGIDevice(IDXGIDevice dxgiDevice, out IntPtr graphicsDevice);
        // =======================================================================

        public WgcCapture()
        {
            // === ВИПРАВЛЕННЯ: Правильне створення пристрою Vortice ===
            var creationFlags = DeviceCreationFlags.BgraSupport;
            // DriverType тепер з Vortice.Direct3D
            Vortice.Direct3D.DriverType driverType = Vortice.Direct3D.DriverType.Hardware; 
            FeatureLevel[] featureLevels = { FeatureLevel.Level_11_0 }; // Достатньо одного рівня

            var result = D3D11.D3D11CreateDevice(
                null, driverType, creationFlags, featureLevels, 
                out _vorticeDevice, out _vorticeContext);
            result.CheckError(); // Перевірка на помилку
            // ==================================================
            
            _winrtDevice = CreateDirect3DDeviceFromVorticeDevice(_vorticeDevice);
        }

        // === ВИПРАВЛЕННЯ: Правильний helper для конвертації ===
        // Використовує P/Invoke та WinRT.MarshalInterface
        private static IDirect3DDevice CreateDirect3DDeviceFromVorticeDevice(Vortice.Direct3D11.ID3D11Device device)
        {
            using (var dxgiDevice = device.QueryInterface<IDXGIDevice>())
            {
                var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out IntPtr graphicsDevicePtr);
                hr.CheckError(); // Перевірка на помилку

                if (graphicsDevicePtr == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to get graphics device pointer.");
                }

                var winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevicePtr);
                Marshal.Release(graphicsDevicePtr); // Зменшуємо лічильник посилань COM
                return winrtDevice;
            }
        }
        // ========================================================

        public async Task PickCaptureTargetAsync()
        {
            var picker = new GraphicsCapturePicker();
            IntPtr hwnd = new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
            InitializeWithWindow.Initialize(picker, hwnd);
            
            // === ВИПРАВЛЕННЯ: Використовуємо PickSingleItemAsync ===
            _captureItem = await picker.PickSingleItemAsync(); 
            // ===============================================
        }

        public async Task<BitmapSource?> TakeSnapshotAsync()
        {
            if (_captureItem == null)
            {
                await PickCaptureTargetAsync();
                if (_captureItem == null) return null;
            }

            Direct3D11CaptureFrame? frame = null;
            BitmapSource? wpfBitmap = null;

            // Використовуємо _winrtDevice (IDirect3DDevice), це правильно
            using (var framePool = Direct3D11CaptureFramePool.Create(_winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _captureItem!.Size))
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
                using (var sourceTexture = GetVorticeTextureFromFrame(frame.Surface))
                {
                    // === ВИПРАВЛЕННЯ: Явні типи Vortice для Texture2DDescription ===
                    var stagingDesc = new Vortice.Direct3D11.Texture2DDescription
                    {
                        Width = sourceTexture.Description.Width,
                        Height = sourceTexture.Description.Height,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = sourceTexture.Description.Format, // Використовуємо той самий формат
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Staging, // Правильно
                        BindFlags = BindFlags.None,    // Правильно
                        // CpuAccessFlags тепер з Vortice.Direct3D11
                        CpuAccessFlags = Vortice.Direct3D11.CpuAccessFlags.Read, 
                        // MiscFlags тепер ResourceOptionFlags з Vortice.Direct3D11
                        MiscFlags = ResourceOptionFlags.None 
                    };
                    // ===========================================================

                    using (var stagingTexture = _vorticeDevice.CreateTexture2D(stagingDesc))
                    {
                        // Використовуємо контекст, який ми створили
                        _vorticeContext.CopyResource(stagingTexture, sourceTexture);
                        
                        // === ВИПРАВЛЕННЯ: Явні MapFlags з Vortice.Direct3D11 ===
                        var mappedSubresource = _vorticeContext.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None); 
                        // ==================================================

                        wpfBitmap = BitmapSource.Create(
                            stagingDesc.Width,
                            stagingDesc.Height,
                            96, 96,
                            System.Windows.Media.PixelFormats.Bgra32,
                            null,
                            mappedSubresource.DataPointer,
                            mappedSubresource.RowPitch * stagingDesc.Height, 
                            mappedSubresource.RowPitch
                        );

                        wpfBitmap.Freeze(); 
                        _vorticeContext.Unmap(stagingTexture, 0);
                    }
                }
            }
            finally
            {
                frame?.Dispose();
            }

            return wpfBitmap;
        }

        // === ВИПРАВЛЕННЯ: Правильний QueryInterface через WinRT Interop ===
        private ID3D11Texture2D GetVorticeTextureFromFrame(IDirect3DSurface surface)
        {
            // Отримуємо COM-інтерфейс IDXGISurface з IDirect3DSurface
            using (var dxgiSurface = surface.GetDXGISurface()) 
            {
                // Отримуємо Vortice ID3D11Texture2D з IDXGISurface
                return dxgiSurface.QueryInterface<ID3D11Texture2D>();
            }
        }

        // Допоміжний метод розширення для легкого отримання IDXGISurface
        static class Direct3DSurfaceExtensions
        {
            public static IDXGISurface GetDXGISurface(this IDirect3DSurface surface)
            {
                var access = (IDirect3DDXGISurface)surface; // Використовуємо наш COMImport
                return MarshalInterface<IDXGISurface>.FromAbi(Marshal.GetIUnknownForObject(access));
            }
        }
        // =============================================================
    }
}
