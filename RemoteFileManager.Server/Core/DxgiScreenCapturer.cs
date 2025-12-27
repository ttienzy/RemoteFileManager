using System;
using System.Runtime.InteropServices;
using System.Threading;
using K4os.Compression.LZ4;
using Vortice;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace RemoteFileManager.Server.Core
{
    public class DxgiScreenCapturer : IDisposable
    {
        private ID3D11Device _device;
        private ID3D11DeviceContext _context;
        private IDXGIOutputDuplication _duplication;
        private ID3D11Texture2D _stagingTexture;

        // --- CẤU HÌNH RESIZE ---
        // 0.6 nghĩa là thu nhỏ còn 60% (VD: 1920x1080 -> 1152x648)
        // Giúp tăng tốc độ FPS lên đáng kể
        private readonly double _scale = 0.6;

        // Kích thước gốc (Real)
        private int _realWidth;
        private int _realHeight;

        // Kích thước gửi đi (Sent) - Client sẽ nhận kích thước này
        public int SendWidth => (int)(_realWidth * _scale);
        public int SendHeight => (int)(_realHeight * _scale);

        public event Action<int, int, int, int, byte[]> OnScreenUpdate;
        public event Action<int, int, bool> OnCursorUpdate;

        public DxgiScreenCapturer()
        {
            Initialize();
        }

        private void Initialize()
        {
            Console.WriteLine($"[DXGI] Initializing with Scale {_scale * 100}%...");

            IDXGIFactory1 factory = null;
            IDXGIAdapter1 selectedAdapter = null;
            IDXGIOutput selectedOutput = null;

            try
            {
                if (CreateDXGIFactory1(out factory).Failure) throw new Exception("Cannot create DXGI Factory.");

                int adapterIndex = 0;
                IDXGIAdapter1 tempAdapter;
                while (factory.EnumAdapters1((uint)adapterIndex, out tempAdapter).Success)
                {
                    IDXGIOutput tempOutput;
                    if (tempAdapter.EnumOutputs(0, out tempOutput).Success)
                    {
                        selectedAdapter = tempAdapter;
                        selectedOutput = tempOutput;
                        Console.WriteLine($"[DXGI] Selected Adapter: {tempAdapter.Description1.Description}");
                        break;
                    }
                    tempAdapter.Dispose();
                    adapterIndex++;
                }

                if (selectedAdapter == null) throw new Exception("No Monitor found.");

                if (D3D11CreateDevice(selectedAdapter, Vortice.Direct3D.DriverType.Unknown, DeviceCreationFlags.VideoSupport, null, out _device, out _context).Failure)
                {
                    throw new Exception("D3D11CreateDevice Failed.");
                }

                using (var output1 = selectedOutput.QueryInterface<IDXGIOutput1>())
                {
                    _duplication = output1.DuplicateOutput(_device);
                }

                var bounds = selectedOutput.Description.DesktopCoordinates;
                _realWidth = bounds.Right - bounds.Left;
                _realHeight = bounds.Bottom - bounds.Top;

                Console.WriteLine($"[DXGI] Real Size: {_realWidth}x{_realHeight} -> Send Size: {SendWidth}x{SendHeight}");

                var textureDesc = new Texture2DDescription
                {
                    Width = (uint)_realWidth,
                    Height = (uint)_realHeight,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CPUAccessFlags = CpuAccessFlags.Read,
                    MiscFlags = ResourceOptionFlags.None
                };
                _stagingTexture = _device.CreateTexture2D(textureDesc);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DXGI] Init Error: {ex.Message}");
                throw;
            }
            finally
            {
                selectedOutput?.Dispose();
                selectedAdapter?.Dispose();
                factory?.Dispose();
            }
        }

        public void CaptureLoop(CancellationToken token)
        {
            Console.WriteLine("[DXGI] Capture Loop Started.");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = _duplication.AcquireNextFrame(40, out var frameInfo, out var desktopResource);

                    if (result.Failure)
                    {
                        if (result.Code == Vortice.DXGI.ResultCode.WaitTimeout) continue;
                        if (result.Code == Vortice.DXGI.ResultCode.AccessLost)
                        {
                            DisposeResources();
                            Initialize();
                            continue;
                        }
                        continue;
                    }

                    // Gửi toàn bộ frame (đã resize)
                    using (var frameTexture = desktopResource.QueryInterface<ID3D11Texture2D>())
                    {
                        ProcessFullFrameResized(frameTexture, frameInfo);
                    }

                    _duplication.ReleaseFrame();
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void ProcessFullFrameResized(ID3D11Texture2D frameTexture, OutduplFrameInfo frameInfo)
        {
            // 1. Gửi chuột (Cần scale tọa độ chuột theo tỷ lệ ảnh)
            if (frameInfo.LastMouseUpdateTime > 0)
            {
                int cursorX = (int)(frameInfo.PointerPosition.Position.X * _scale);
                int cursorY = (int)(frameInfo.PointerPosition.Position.Y * _scale);
                OnCursorUpdate?.Invoke(cursorX, cursorY, frameInfo.PointerPosition.Visible);
            }

            // 2. Gửi Hình Ảnh (Nếu có thay đổi)
            if (frameInfo.TotalMetadataBufferSize > 0)
            {
                _context.CopyResource(_stagingTexture, frameTexture);
                var mapSource = _context.Map(_stagingTexture, 0, Vortice.Direct3D11.MapMode.Read, Vortice.Direct3D11.MapFlags.None);

                try
                {
                    int targetW = SendWidth;
                    int targetH = SendHeight;
                    byte[] resizedData = new byte[targetW * targetH * 4];

                    unsafe
                    {
                        byte* sourcePtr = (byte*)mapSource.DataPointer;
                        long sourceRowPitch = mapSource.RowPitch;

                        fixed (byte* destPtr = resizedData)
                        {
                            // Thuật toán Nearest Neighbor Resize (Nhanh nhất)
                            // Duyệt qua từng pixel của ảnh đích (ảnh nhỏ)
                            for (int y = 0; y < targetH; y++)
                            {
                                // Tính dòng tương ứng bên ảnh gốc
                                int sourceY = (int)(y / _scale);
                                byte* sourceRow = sourcePtr + (sourceY * sourceRowPitch);
                                byte* destRow = destPtr + (y * targetW * 4);

                                for (int x = 0; x < targetW; x++)
                                {
                                    // Tính cột tương ứng bên ảnh gốc
                                    int sourceX = (int)(x / _scale);

                                    // Copy 4 bytes (B,G,R,A) từ gốc sang đích
                                    // Dùng con trỏ int* để copy 4 byte 1 lần cho nhanh
                                    int* pixelSrc = (int*)(sourceRow + (sourceX * 4));
                                    int* pixelDest = (int*)(destRow + (x * 4));

                                    *pixelDest = *pixelSrc;
                                }
                            }
                        }
                    }

                    // Nén LZ4 trên dữ liệu đã thu nhỏ (nhanh hơn rất nhiều)
                    byte[] compressed = LZ4Pickler.Pickle(resizedData);

                    OnScreenUpdate?.Invoke(0, 0, targetW, targetH, compressed);
                }
                finally
                {
                    _context.Unmap(_stagingTexture, 0);
                }
            }
        }

        private void DisposeResources()
        {
            _stagingTexture?.Dispose();
            _duplication?.Dispose();
            _context?.Dispose();
            _device?.Dispose();
        }

        public void Dispose()
        {
            DisposeResources();
            GC.SuppressFinalize(this);
        }
    }
}