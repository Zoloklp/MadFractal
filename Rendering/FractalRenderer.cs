using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using System.IO;
using Windows.Graphics.Printing3D;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Windows.Security.Cryptography.Core;
using System.Runtime.CompilerServices;
using System.Numerics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;


namespace MadFractal.Rendering
{
    public class FractalRenderer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FractalParameters
        {
            public Vector2 JuliaConstant;
            public float EscapeRadius;
            public uint Power;

            public uint MaxIterations;
            public uint Function;
            public uint ColoringMethod;
            public uint UseSmooth;
            public uint OrbitTrapType;

            public Vector3 PaletteBase;
            public float PalettePadding1;

            public Vector3 PaletteAmplitude;
            public float PalettePadding2;

            public Vector3 PaletteFrequency;
            public float PalettePadding3;

            public Vector3 PalettePhase;
            public float PalettePadding4;

            public Vector2 ViewCenter;
            public float ViewScale;
            public float ViewPadding;

            // D3D11 constant-buffer size must be a multiple of 16 bytes.
            public float BufferPadding1;
            public float BufferPadding2;
            public float BufferPadding3;
        }
        

        private static readonly Guid SwapChainPanelNativeIid = new("63AAD0B8-7C24-40FF-85A8-640D944CC325");

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetSwapChainDelegate(IntPtr thisPtr, IntPtr swapChain);

        private void AttachSwapChain(SwapChainPanel swapChainPanel)
        {
            SetSwapChain(swapChainPanel, _swapChain.NativePointer);
        }


        private static void SetSwapChain(SwapChainPanel swapChainPanel, IntPtr swapChainPointer)
        {
            var unknown = Marshal.GetIUnknownForObject(swapChainPanel);
            IntPtr native = IntPtr.Zero;

            try
            {
                var iid = SwapChainPanelNativeIid;
                Marshal.ThrowExceptionForHR(
                    Marshal.QueryInterface(unknown, ref iid, out native));

                var vtable = Marshal.ReadIntPtr(native);
                var setSwapChainPointer = Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size);
                var setSwapChain =
                    Marshal.GetDelegateForFunctionPointer<SetSwapChainDelegate>(setSwapChainPointer);

                Marshal.ThrowExceptionForHR(
                    setSwapChain(native, swapChainPointer));
            }
            finally
            {
                if (native != IntPtr.Zero)
                    Marshal.Release(native);

                Marshal.Release(unknown);
            }
        }
        private ID3D11Device _device;
        private ID3D11DeviceContext _context;
        private ID3D11ComputeShader _computeShader;
        private ID3D11Texture2D _texture;
        private ID3D11UnorderedAccessView _uav;
        private IDXGISwapChain1 _swapChain;
        private ID3D11Texture2D _backBuffer;
        private readonly SwapChainPanel _panel;
        private bool _disposed;
        private uint _width;
        private uint _height;

        private ID3D11Buffer _parameterBuffer;
        private FractalParameters _parameters;

        public float ViewScale
        {
            get
            {
                return _parameters.ViewScale;
            }
        }

        public FractalRenderer(SwapChainPanel panel)
        {
            _parameters = new FractalParameters
            {
                JuliaConstant = new Vector2(0f, 0f),
                EscapeRadius = 100.0f,
                Power = 2,
                ViewCenter = Vector2.Zero,
                ViewScale = 4.0f,

                MaxIterations = 100,
                Function = 1,
                ColoringMethod = 0,
                UseSmooth = 1,
                OrbitTrapType = 4,

                PaletteBase = new Vector3(0.50f, 0.15f, 0.05f),
                PaletteAmplitude = new Vector3(0.50f, 0.35f, 0.25f),
                PaletteFrequency = new Vector3(1.00f, 0.80f, 0.50f),
                PalettePhase = new Vector3(0.00f, 0.05f, 0.10f)
            };

            _panel = panel;

            GetPanelPixelSize(panel, out _width, out _height);

            D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.None,
                new[]
                {
                    FeatureLevel.Level_11_1,
                    FeatureLevel.Level_11_0,
                },
                out _device,
                out _context
            ).CheckError();

            _parameterBuffer = _device.CreateBuffer(
                new BufferDescription
                    {
                        ByteWidth = (uint)Marshal.SizeOf<FractalParameters>(),
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.ConstantBuffer,
                        CPUAccessFlags = CpuAccessFlags.None
                    });

            _context.UpdateSubresource(
            ref _parameters,
            _parameterBuffer);

            _computeShader = CreateComputeShader();

            Createtexture(_width, _height);

            CreateSwapChain(_width, _height);

            _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);

            AttachSwapChain(panel);

            Render();
        }

        public void SetParameters(
            Vector2 juliaConstant,
            uint maxIterations,
            float escapeRadius,
            uint power,
            uint function,
            uint coloringMethod,
            uint useSmooth,
            uint orbitTrapType,
            Vector3 paletteBase,
            Vector3 paletteAmplitude,
            Vector3 paletteFrequency,
            Vector3 palettePhase)
            {
                _parameters = new FractalParameters
                {
                    JuliaConstant = juliaConstant,
                    EscapeRadius = escapeRadius,
                    Power = power,
                ViewCenter = _parameters.ViewCenter,
                ViewScale = _parameters.ViewScale,

                    MaxIterations = maxIterations,
                    Function = function,
                    ColoringMethod = coloringMethod,
                    UseSmooth = useSmooth,
                    OrbitTrapType = orbitTrapType,

                    PaletteBase = paletteBase,
                    PaletteAmplitude = paletteAmplitude,
                    PaletteFrequency = paletteFrequency,
                    PalettePhase = palettePhase
                };

            _context.UpdateSubresource(
                ref _parameters,
                _parameterBuffer);

            Render();
        }

        public void Pan(Vector2 normalizedDelta)
        {
            if (_disposed || _width == 0 || _height == 0)
                return;

            float aspectRatio = (float)_width / _height;
            _parameters.ViewCenter += new Vector2(
                -normalizedDelta.X * aspectRatio * _parameters.ViewScale,
                normalizedDelta.Y * _parameters.ViewScale);

            _context.UpdateSubresource(ref _parameters, _parameterBuffer);
            Render();
        }

        public void ZoomAt(Vector2 normalizedPosition, int wheelDelta)
        {
            if (_disposed || _width == 0 || _height == 0 || wheelDelta == 0)
                return;

            float aspectRatio = (float)_width / _height;
            Vector2 centeredPosition = normalizedPosition - new Vector2(0.5f, 0.5f);
            centeredPosition.X *= aspectRatio;
            centeredPosition.Y = -centeredPosition.Y;

            Vector2 pointUnderCursor = _parameters.ViewCenter + centeredPosition * _parameters.ViewScale;
            float zoomFactor = MathF.Pow(1.1f, -wheelDelta / 120.0f);
            float newScale = Math.Clamp(
                _parameters.ViewScale * zoomFactor,
                0.000001f,
                50.0f);

            _parameters.ViewScale = newScale;
            _parameters.ViewCenter = pointUnderCursor - centeredPosition * newScale;

            _context.UpdateSubresource(ref _parameters, _parameterBuffer);
            Render();
        }

        public void UpdateJuliaConstant(float x, float y)
        {
            _parameters.JuliaConstant = new Vector2(x, y);

            _context.UpdateSubresource(
                ref _parameters,
                _parameterBuffer);

            Render();
        }

        public void Resize(uint width, uint height)
        {
            if (_disposed || width == 0 || height == 0 || (width == _width && height == _height))
                return;

            _context.ClearState();
            _context.Flush();

            _backBuffer.Dispose();
            _uav.Dispose();
            _texture.Dispose();

            _swapChain.ResizeBuffers(
                2,
                width,
                height,
                Format.R8G8B8A8_UNorm,
                SwapChainFlags.None);

            _width = width;
            _height = height;
            _texture = _device.CreateTexture2D(CreateTextureDescription(width, height));
            _uav = _device.CreateUnorderedAccessView(_texture);
            _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);

            Render();
        }

        public async Task SavePngAsync(StorageFile file)
        {
            if (_disposed || _width == 0 || _height == 0)
                return;

            var stagingDescription = _texture.Description;
            stagingDescription.Usage = ResourceUsage.Staging;
            stagingDescription.BindFlags = BindFlags.None;
            stagingDescription.CPUAccessFlags = CpuAccessFlags.Read;

            using var stagingTexture = _device.CreateTexture2D(stagingDescription);
            _context.CopyResource(stagingTexture, _texture);
            _context.Flush();

            var pixels = new byte[checked((int)(_width * _height * 4))];
            var mapped = _context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                for (var y = 0; y < _height; y++)
                {
                    Marshal.Copy(
                        IntPtr.Add(mapped.DataPointer, checked((int)(y * mapped.RowPitch))),
                        pixels,
                        checked((int)(y * _width * 4)),
                        checked((int)(_width * 4)));
                }
            }
            finally
            {
                _context.Unmap(stagingTexture, 0);
            }

            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Rgba8,
                BitmapAlphaMode.Ignore,
                _width,
                _height,
                96,
                96,
                pixels);
            await encoder.FlushAsync();
        }

        private static void GetPanelPixelSize(SwapChainPanel panel, out uint width, out uint height)
        {
            var scale = panel.XamlRoot?.RasterizationScale ?? 1.0;
            width = Math.Max(1u, (uint)Math.Round(panel.ActualWidth * scale));
            height = Math.Max(1u, (uint)Math.Round(panel.ActualHeight * scale));
        }
        private void CreateSwapChain(uint width, uint height)
        {
            using var factory = DXGI.CreateDXGIFactory2<IDXGIFactory2>(false);

            var swapChainDescription = new SwapChainDescription1
            {
                BufferCount = 2,
                BufferUsage = Usage.RenderTargetOutput,
                SampleDescription = new SampleDescription(1, 0),
                Scaling = Scaling.Stretch,
                Width = width,
                Height = height,
                Format = Format.R8G8B8A8_UNorm,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Ignore
            };

            _swapChain = factory.CreateSwapChainForComposition(_device, swapChainDescription);


        }

        private void Createtexture(uint width, uint height)
        {
            _texture = _device.CreateTexture2D(CreateTextureDescription(width, height));
            _uav = _device.CreateUnorderedAccessView(_texture);
        }

        private static Texture2DDescription CreateTextureDescription(uint width, uint height)
        {
            return new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1u,
                ArraySize = 1u,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription { Count = 1u, Quality = 0u },
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.UnorderedAccess,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };
        }

        private void Render()
        {
            _context.CSSetShader(_computeShader);
            _context.CSSetUnorderedAccessView(0, _uav);
            _context.CSSetConstantBuffer(0, _parameterBuffer);
            _context.Dispatch(
            (uint)Math.Ceiling(_texture.Description.Width / 8.0),
            (uint)Math.Ceiling(_texture.Description.Height / 8.0),
            1);

            _context.CSSetConstantBuffer(0, null);
            _context.CSSetShader(null);
            _context.CSSetUnorderedAccessView(0, null);

            _context.CopyResource(_backBuffer, _texture);

            _swapChain.Present(1, PresentFlags.None);
        }

        private ID3D11ComputeShader CreateComputeShader()
        {
            // Компиляция шейдера в массив байт
            var dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var bytecode = Compiler.CompileFromFile(Path.Combine(dir, "DirectHLSL", "Fractal.hlsl"), "main", "cs_5_0");

            // CreateComputeShader принимает массив байт; конвертируем безопасно
            return _device.CreateComputeShader(bytecode.ToArray());
        }
    

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            SetSwapChain(_panel, IntPtr.Zero);
            _context.ClearState();
            _context.Flush();

            _backBuffer.Dispose();
            _swapChain.Dispose();
            _uav.Dispose();
            _texture.Dispose();
            _parameterBuffer.Dispose();
            _computeShader.Dispose();
            _context.Dispose();
            _device.Dispose();
        }
    }
}