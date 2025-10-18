using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using WinRT.Interop;
using System.Windows.Interop;

[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IDirect3DDXGISurface
{
    void GetDesc(out SurfaceDescription pDesc);
}

namespace MomentSnap
{
    public class ScreenCapture
    {
        private GraphicsCaptureItem _captureItem;
        private ID3D11Device _d3dDevice;
        private SharpDX.Direct3D11.Device _sharpDxDevice;

        public ScreenCapture()
        {
            _sharpDxDevice = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            _d3dDevice = CreateDirect3D11DeviceFromSharpDXDevice(_sharpDxDevice);
        }

        private ID3D11Device CreateDirect3D11DeviceFromSharpDXDevice(SharpDX.Direct3D11.Device device)
        {
            using (var dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device>())
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
                using (var sourceTexture = GetSharpDXTextureFromFrame(frame.Surface))
                {
                    var stagingDesc = sourceTexture.Description;
                    stagingDesc.CpuAccessFlags = CpuAccessFlags.Read;
                    stagingDesc.Usage = ResourceUsage.Staging;
                    stagingDesc.BindFlags = BindFlags.None;
                    stagingDesc.OptionFlags = ResourceOptionFlags.None;

                    using (var stagingTexture = new Texture2D(_sharpDxDevice, stagingDesc))
                    {
                        _sharpDxDevice.ImmediateContext.CopyResource(sourceTexture, stagingTexture);
                        var dataBox = _sharpDxDevice.ImmediateContext.MapSubresource(stagingTexture, 0, MapMode.Read, MapFlags.None);

                        wpfBitmap = BitmapSource.Create(
                            sourceTexture.Description.Width,
                            sourceTexture.Description.Height,
                            96, 96,
                            System.Windows.Media.PixelFormats.Bgra32,
                            null,
                            dataBox.DataPointer,
                            dataBox.RowPitch * sourceTexture.Description.Height,
                            dataBox.RowPitch
                        );

                        wpfBitmap.Freeze();
                        _sharpDxDevice.ImmediateContext.UnmapSubresource(stagingTexture, 0);
                    }
                }
            }
            finally
            {
                frame?.Dispose();
            }

            return wpfBitmap;
        }

        private Texture2D GetSharpDXTextureFromFrame(IDirect3DSurface surface)
        {
            var access = (IDirect3DDXGISurface)surface;
            access.GetDesc(out var desc);
            IntPtr texturePtr;
            using (var dxgiSurface = surface.QueryInterface<SharpDX.DXGI.Surface>())
            {
                texturePtr = dxgiSurface.NativePointer;
            }
            return new Texture2D(texturePtr);
        }
    }
}
