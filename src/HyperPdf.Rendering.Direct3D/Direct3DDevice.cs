// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Rendering.Direct3D.Interop;

namespace HyperPdf.Rendering.Direct3D;

/// <summary>
/// The Direct3D device, its context and where frames go: the swap chain of a window, or a texture that tests read back. The real
/// GPU is tried first and Windows' own software rasteriser second, so the backend starts on any machine.
/// </summary>
[DebuggerDisplay("Direct3DDevice: {Width} x {Height}, window {HasWindow}")]
internal sealed unsafe class Direct3DDevice : IDisposable
{
    /// <summary>The hardware driver type.</summary>
    private const uint HardwareDriver = 1;

    /// <summary>The driver type of WARP, the software rasteriser in Windows.</summary>
    private const uint SoftwareDriver = 5;

    /// <summary>The creation flag that allows BGRA render targets.</summary>
    private const uint BgraSupport = 0x20;

    /// <summary>The SDK version every Direct3D 11 application passes.</summary>
    private const uint SdkVersion = 7;

    /// <summary>The DXGI format of 8 bit BGRA.</summary>
    private const uint FormatBgra = 87;

    /// <summary>The DXGI usage of a buffer that is drawn into.</summary>
    private const uint RenderTargetOutput = 0x20;

    /// <summary>The swap effect that flips buffers, the fastest on Windows 10 and later.</summary>
    private const uint FlipDiscard = 4;

    /// <summary>The swap effect that copies buffers, which every Windows version has.</summary>
    private const uint Discard = 0;

    /// <summary>The buffers a flipping swap chain needs.</summary>
    private const uint FlipBuffers = 2;

    /// <summary>The bind flag of a texture a shader reads.</summary>
    private const uint BindShaderResource = 0x8;

    /// <summary>The bind flag of a texture that is drawn into.</summary>
    private const uint BindRenderTarget = 0x20;

    /// <summary>The usage of a texture the CPU reads back.</summary>
    private const uint UsageStaging = 3;

    /// <summary>The CPU access flag for reading.</summary>
    private const uint CpuRead = 0x20000;

    /// <summary>The map type for reading.</summary>
    private const uint MapRead = 1;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The ID3D11Device.</summary>
    private ulong _device;

    /// <summary>The ID3D11DeviceContext.</summary>
    private ulong _context;

    /// <summary>The IDXGISwapChain, or null when frames go to a texture.</summary>
    private ulong _swapChain;

    /// <summary>The texture frames go to when there is no window.</summary>
    private ulong _frameTexture;

    /// <summary>The texture the CPU reads a frame back from.</summary>
    private ulong _readTexture;

    /// <summary>The render target view of the current frame.</summary>
    private ulong _target;

    /// <summary>Initializes a new instance of the <see cref="Direct3DDevice"/> class.</summary>
    /// <param name="window">The window handle, or 0 to draw into a texture.</param>
    /// <exception cref="InvalidOperationException">No Direct3D 11 device could be made.</exception>
    internal Direct3DDevice(nint window)
    {
        if (!TryCreate(window, HardwareDriver) && !TryCreate(window, SoftwareDriver))
        {
            throw new InvalidOperationException("No Direct3D 11 device could be made.");
        }
    }

    /// <summary>Gets the ID3D11Device.</summary>
    internal void* Device => (void*)_device;

    /// <summary>Gets the ID3D11DeviceContext.</summary>
    internal void* Context => (void*)_context;

    /// <summary>Gets the render target view of the current frame, or null while the frame has no size.</summary>
    internal void* Target => (void*)_target;

    /// <summary>Gets a value indicating whether frames go to a window.</summary>
    internal bool HasWindow => _swapChain != 0;

    /// <summary>Gets the frame width in pixels.</summary>
    internal int Width { get; private set; }

    /// <summary>Gets the frame height in pixels.</summary>
    internal int Height { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        ReleaseTarget();
        Direct3DCalls.Release((void*)_swapChain);
        Direct3DCalls.Release((void*)_context);
        Direct3DCalls.Release((void*)_device);
        _swapChain = 0;
        _context = 0;
        _device = 0;
    }

    /// <summary>Makes the frame target again for a new size.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <exception cref="InvalidOperationException">Direct3D could not make the target.</exception>
    internal void Resize(int width, int height)
    {
        ReleaseTarget();
        Width = width;
        Height = height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (_swapChain != 0)
        {
            HResult.Check(Direct3DCalls.ResizeBuffers((void*)_swapChain, (uint)width, (uint)height), "resize the window buffers");
            void* backBuffer;
            HResult.Check(Direct3DCalls.GetBackBuffer((void*)_swapChain, &backBuffer), "get the window buffer");
            void* view;
            var made = Direct3DCalls.CreateRenderTargetView((void*)_device, backBuffer, &view);
            Direct3DCalls.Release(backBuffer);
            HResult.Check(made, "make the window target");
            _target = (ulong)view;
            return;
        }

        _frameTexture = (ulong)CreateTexture(new((uint)width, (uint)height, 1, 1, FormatBgra, 1, 0, 0, BindRenderTarget | BindShaderResource, 0, 0));
        _readTexture = (ulong)CreateTexture(new((uint)width, (uint)height, 1, 1, FormatBgra, 1, 0, UsageStaging, 0, CpuRead, 0));
        void* target;
        HResult.Check(Direct3DCalls.CreateRenderTargetView((void*)_device, (void*)_frameTexture, &target), "make the frame target");
        _target = (ulong)target;
    }

    /// <summary>Shows the frame in the window and waits for the display's refresh. Does nothing without a window.</summary>
    internal void Present()
    {
        if (_swapChain != 0)
        {
            _ = Direct3DCalls.Present((void*)_swapChain, 1);
        }
    }

    /// <summary>Reads the frame texture as BGRA with the top line first. Only frames drawn without a window can be read.</summary>
    /// <param name="destination">Receives width * height * 4 bytes.</param>
    /// <exception cref="InvalidOperationException">The frame goes to a window, or Direct3D could not map it.</exception>
    internal void ReadPixels(Span<byte> destination)
    {
        if (_frameTexture == 0 || _readTexture == 0)
        {
            throw new InvalidOperationException("Only a frame drawn without a window can be read back.");
        }

        Direct3DCalls.CopyResource((void*)_context, (void*)_readTexture, (void*)_frameTexture);
        MappedSubresource mapped;
        HResult.Check(Direct3DCalls.Map((void*)_context, (void*)_readTexture, MapRead, &mapped), "read the frame");
        var rowBytes = Width * BytesPerPixel;
        for (var row = 0; row < Height; row++)
        {
            new ReadOnlySpan<byte>((byte*)mapped.Data + (row * (int)mapped.RowPitch), rowBytes).CopyTo(destination.Slice(row * rowBytes, rowBytes));
        }

        Direct3DCalls.Unmap((void*)_context, (void*)_readTexture);
    }

    /// <summary>Makes a BGRA texture.</summary>
    /// <param name="description">The texture description.</param>
    /// <returns>The ID3D11Texture2D.</returns>
    /// <exception cref="InvalidOperationException">Direct3D could not make the texture.</exception>
    internal void* CreateTexture(in Texture2DDescription description)
    {
        void* texture;
        fixed (Texture2DDescription* pointer = &description)
        {
            HResult.Check(Direct3DCalls.CreateTexture2D((void*)_device, pointer, null, &texture), "make a texture");
        }

        return texture;
    }

    /// <summary>Makes the device for one driver type, with a swap chain when there is a window.</summary>
    /// <param name="window">The window handle, or 0.</param>
    /// <param name="driver">The driver type.</param>
    /// <returns><see langword="true"/> when the device was made.</returns>
    private bool TryCreate(nint window, uint driver)
    {
        void* device;
        void* context;
        if (window == 0)
        {
            if (NativeMethods.CreateDevice(null, driver, 0, BgraSupport, null, 0, SdkVersion, &device, null, &context) < 0)
            {
                return false;
            }

            _device = (ulong)device;
            _context = (ulong)context;
            return true;
        }

        return TryCreateWithSwapChain(window, driver, FlipDiscard, FlipBuffers) || TryCreateWithSwapChain(window, driver, Discard, 1);
    }

    /// <summary>Makes the device and the swap chain of a window.</summary>
    /// <param name="window">The window handle.</param>
    /// <param name="driver">The driver type.</param>
    /// <param name="swapEffect">How buffers are swapped.</param>
    /// <param name="buffers">The number of buffers.</param>
    /// <returns><see langword="true"/> when both were made.</returns>
    private bool TryCreateWithSwapChain(nint window, uint driver, uint swapEffect, uint buffers)
    {
        var description = new SwapChainDescription(0, 0, 0, 0, FormatBgra, 0, 0, 1, 0, RenderTargetOutput, buffers, (ulong)window, 1, swapEffect, 0);
        void* swapChain;
        void* device;
        void* context;
        if (NativeMethods.CreateDeviceAndSwapChain(null, driver, 0, BgraSupport, null, 0, SdkVersion, &description, &swapChain, &device, null, &context) < 0)
        {
            return false;
        }

        _swapChain = (ulong)swapChain;
        _device = (ulong)device;
        _context = (ulong)context;
        return true;
    }

    /// <summary>Releases the frame target and the textures behind it.</summary>
    private void ReleaseTarget()
    {
        if (_context != 0)
        {
            // A swap chain cannot resize its buffers while the context still draws into one.
            Direct3DCalls.SetRenderTarget((void*)_context, null);
        }

        Direct3DCalls.Release((void*)_target);
        Direct3DCalls.Release((void*)_frameTexture);
        Direct3DCalls.Release((void*)_readTexture);
        _target = 0;
        _frameTexture = 0;
        _readTexture = 0;
    }
}
