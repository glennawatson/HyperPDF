// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Rendering.Direct3D12.Interop;

namespace HyperPdf.Rendering.Direct3D12;

/// <summary>
/// The Direct3D 12 device, its queue, the command list a frame is recorded in, and where frames go: the swap chain of a window, or a
/// texture that tests read back. The real GPU is tried first and Windows' own software rasteriser second, so the backend starts on any
/// machine. A frame ends by waiting for the GPU, so everything a frame used is free again when the next one starts.
/// </summary>
[DebuggerDisplay("Direct3D12Device: {Width} x {Height}, window {HasWindow}")]
internal sealed unsafe class Direct3D12Device : IDisposable
{
    /// <summary>The state of a texture a pixel shader reads.</summary>
    internal const uint StatePixelResource = 0x80;

    /// <summary>The state of a resource that is copied to.</summary>
    internal const uint StateCopyDestination = 0x400;

    /// <summary>What a failed Close reports.</summary>
    private const string CloseAction = "close the command list";

    /// <summary>The lowest feature level a device must have, 11.0.</summary>
    private const uint MinimumFeatureLevel = 0xB000;

    /// <summary>The DXGI format of 8 bit BGRA.</summary>
    private const uint FormatBgra = 87;

    /// <summary>The number of buffers of the flip model swap chain.</summary>
    private const int BufferCount = 2;

    /// <summary>The DXGI usage of a buffer that is drawn into.</summary>
    private const uint RenderTargetOutput = 0x20;

    /// <summary>The swap effect that flips buffers and drops the old contents.</summary>
    private const uint FlipDiscard = 4;

    /// <summary>The window association flag that stops DXGI from switching to full screen on Alt+Enter.</summary>
    private const uint NoAltEnter = 2;

    /// <summary>The descriptor heap type of render target views.</summary>
    private const uint RenderTargetHeapType = 2;

    /// <summary>The heap type of memory only the GPU uses.</summary>
    private const uint HeapDefault = 1;

    /// <summary>The heap type of memory the CPU writes and the GPU reads.</summary>
    private const uint HeapUpload = 2;

    /// <summary>The heap type of memory the GPU writes and the CPU reads.</summary>
    private const uint HeapReadback = 3;

    /// <summary>The resource dimension of a buffer.</summary>
    private const uint DimensionBuffer = 1;

    /// <summary>The resource dimension of a two dimensional texture.</summary>
    private const uint DimensionTexture2D = 3;

    /// <summary>The layout of a buffer.</summary>
    private const uint LayoutRowMajor = 1;

    /// <summary>The resource flag that allows drawing into a texture.</summary>
    private const uint AllowRenderTarget = 1;

    /// <summary>The state of a buffer the GPU only reads, which an upload heap requires.</summary>
    private const uint StateGenericRead = 0xAC3;

    /// <summary>The state of a resource the swap chain shows.</summary>
    private const uint StatePresent = 0;

    /// <summary>The state of a resource that is drawn into.</summary>
    private const uint StateRenderTarget = 4;

    /// <summary>The state of a resource that is copied from.</summary>
    private const uint StateCopySource = 0x800;

    /// <summary>The barrier subresource value that means every subresource.</summary>
    private const uint AllSubresources = 0xFFFFFFFF;

    /// <summary>The texture copy location type of a subresource index.</summary>
    private const uint CopySubresource = 0;

    /// <summary>The texture copy location type of a placed footprint in a buffer.</summary>
    private const uint CopyFootprint = 1;

    /// <summary>The row pitch Direct3D 12 requires of a texture copy, in bytes.</summary>
    private const int PitchAlignment = 256;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>How long to wait for the GPU, in milliseconds, before the device counts as hung.</summary>
    private const uint WaitMilliseconds = 10_000;

    /// <summary>The GPU resources behind the two back buffers, or the frame texture in the first entry.</summary>
    private readonly ulong[] _targets = new ulong[BufferCount];

    /// <summary>Why the last adapter was rejected.</summary>
    private string _failure = string.Empty;

    /// <summary>The IDXGIFactory4.</summary>
    private ulong _factory;

    /// <summary>The ID3D12Device.</summary>
    private ulong _device;

    /// <summary>The ID3D12CommandQueue.</summary>
    private ulong _queue;

    /// <summary>The ID3D12CommandAllocator.</summary>
    private ulong _allocator;

    /// <summary>The ID3D12GraphicsCommandList.</summary>
    private ulong _list;

    /// <summary>The ID3D12Fence.</summary>
    private ulong _fence;

    /// <summary>The ID3D12DescriptorHeap of render target views.</summary>
    private ulong _targetHeap;

    /// <summary>The IDXGISwapChain3, or 0 when frames go to a texture.</summary>
    private ulong _swapChain;

    /// <summary>The buffer the CPU reads a frame back from.</summary>
    private ulong _readBack;

    /// <summary>The event that is set when the fence reaches a value.</summary>
    private nint _fenceEvent;

    /// <summary>The last value the queue was asked to signal.</summary>
    private ulong _fenceValue;

    /// <summary>The CPU address of the first render target view.</summary>
    private nuint _targetStart;

    /// <summary>The bytes between two render target views.</summary>
    private uint _targetStride;

    /// <summary>The bytes per row of the read back buffer.</summary>
    private int _readPitch;

    /// <summary>The back buffer the frame being drawn goes to.</summary>
    private int _index;

    /// <summary>Initializes a new instance of the <see cref="Direct3D12Device"/> class.</summary>
    /// <param name="window">The window handle, or 0 to draw into a texture.</param>
    /// <exception cref="InvalidOperationException">No Direct3D 12 device could be made.</exception>
    internal Direct3D12Device(nint window)
    {
        if (!TryCreate(window, software: false) && !TryCreate(window, software: true))
        {
            throw new InvalidOperationException($"No Direct3D 12 device could be made: {_failure}");
        }
    }

    /// <summary>Gets the ID3D12Device.</summary>
    internal void* Device => (void*)_device;

    /// <summary>Gets the command list of the frame being recorded.</summary>
    internal void* List => (void*)_list;

    /// <summary>Gets the CPU descriptor of the render target of the frame being recorded.</summary>
    internal nuint Target => _targetStart + (nuint)(_index * (int)_targetStride);

    /// <summary>Gets a value indicating whether frames have a target, which is false while the frame has no size.</summary>
    internal bool HasTarget => _targets[0] != 0;

    /// <summary>Gets a value indicating whether frames go to a window.</summary>
    internal bool HasWindow => _swapChain != 0;

    /// <summary>Gets the frame width in pixels.</summary>
    internal int Width { get; private set; }

    /// <summary>Gets the frame height in pixels.</summary>
    internal int Height { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_queue != 0 && _fence != 0)
        {
            // The GPU may still run the last frame, and releasing what it uses would crash the driver.
            TryWaitIdle();
        }

        ReleaseObjects();
    }

    /// <summary>Rounds a size up to a multiple of a power of two.</summary>
    /// <param name="value">The size.</param>
    /// <param name="alignment">The alignment, a power of two.</param>
    /// <returns>The rounded size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AlignUp(int value, int alignment) => (value + alignment - 1) & ~(alignment - 1);

    /// <summary>Makes the frame target again for a new size.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <exception cref="InvalidOperationException">Direct3D 12 could not make the target.</exception>
    internal void Resize(int width, int height)
    {
        WaitIdle();
        ReleaseTargets();
        Width = width;
        Height = height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (_swapChain != 0)
        {
            HResult.Check(Direct3D12Calls.ResizeBuffers((void*)_swapChain, (uint)width, (uint)height), "resize the window buffers");
            for (var i = 0; i < BufferCount; i++)
            {
                void* buffer;
                HResult.Check(Direct3D12Calls.GetBackBuffer((void*)_swapChain, (uint)i, &buffer), "get a window buffer");
                _targets[i] = (ulong)buffer;
            }
        }
        else
        {
            _targets[0] = (ulong)CreateTexture(width, height, AllowRenderTarget, StateRenderTarget);
            _readPitch = AlignUp(width * BytesPerPixel, PitchAlignment);
            _readBack = (ulong)CreateBuffer(HeapReadback, (ulong)_readPitch * (ulong)height, StateCopyDestination);
        }

        for (var i = 0; i < BufferCount && _targets[i] != 0; i++)
        {
            Direct3D12Calls.CreateRenderTargetView((void*)_device, (void*)_targets[i], _targetStart + (nuint)(i * (int)_targetStride));
        }
    }

    /// <summary>Starts recording a frame: the allocator and list start over and a window buffer becomes a render target.</summary>
    /// <exception cref="InvalidOperationException">Direct3D 12 could not start the list.</exception>
    internal void BeginFrame()
    {
        _index = _swapChain != 0 ? (int)Direct3D12Calls.CurrentBackBuffer((void*)_swapChain) : 0;
        HResult.Check(Direct3D12Calls.Reset((void*)_allocator), "reset the command allocator");
        HResult.Check(CommandListCalls.Reset((void*)_list, (void*)_allocator), "reset the command list");
        if (_swapChain != 0)
        {
            Transition(_targets[_index], StatePresent, StateRenderTarget);
        }
    }

    /// <summary>Finishes the frame: runs the commands, shows the frame in the window and waits for the GPU.</summary>
    /// <exception cref="InvalidOperationException">Direct3D 12 could not run the frame.</exception>
    internal void EndFrame()
    {
        if (_swapChain != 0)
        {
            Transition(_targets[_index], StateRenderTarget, StatePresent);
        }

        HResult.Check(CommandListCalls.Close((void*)_list), CloseAction);
        Direct3D12Calls.Execute((void*)_queue, (void*)_list);
        if (_swapChain != 0)
        {
            // A lost or removed device fails here, and the wait below then ends at once.
            _ = Direct3D12Calls.Present((void*)_swapChain, 1);
        }

        WaitIdle();
    }

    /// <summary>Reads the frame texture as BGRA with the top line first. Only frames drawn without a window can be read.</summary>
    /// <param name="destination">Receives width * height * 4 bytes.</param>
    /// <exception cref="InvalidOperationException">The frame goes to a window, or Direct3D 12 could not read it.</exception>
    internal void ReadPixels(Span<byte> destination)
    {
        if (_swapChain != 0 || _targets[0] == 0 || _readBack == 0)
        {
            throw new InvalidOperationException("Only a frame drawn without a window can be read back.");
        }

        HResult.Check(Direct3D12Calls.Reset((void*)_allocator), "reset the command allocator");
        HResult.Check(CommandListCalls.Reset((void*)_list, (void*)_allocator), "reset the command list");
        Transition(_targets[0], StateRenderTarget, StateCopySource);
        var source = new TextureCopyLocation(_targets[0], CopySubresource, 0, 0, 0, 0, 0, 0);
        var target = new TextureCopyLocation(_readBack, CopyFootprint, 0, FormatBgra, (uint)Width, (uint)Height, 1, (uint)_readPitch);
        CommandListCalls.CopyTexture((void*)_list, &target, &source);
        Transition(_targets[0], StateCopySource, StateRenderTarget);
        HResult.Check(CommandListCalls.Close((void*)_list), CloseAction);
        Direct3D12Calls.Execute((void*)_queue, (void*)_list);
        WaitIdle();

        void* mapped;
        HResult.Check(Direct3D12Calls.Map((void*)_readBack, &mapped), "read the frame");
        var rowBytes = Width * BytesPerPixel;
        for (var row = 0; row < Height; row++)
        {
            new ReadOnlySpan<byte>((byte*)mapped + (row * _readPitch), rowBytes).CopyTo(destination.Slice(row * rowBytes, rowBytes));
        }

        Direct3D12Calls.Unmap((void*)_readBack);
    }

    /// <summary>Records a transition barrier for a resource into the command list.</summary>
    /// <param name="resource">The resource.</param>
    /// <param name="before">The state it is in.</param>
    /// <param name="after">The state it goes to.</param>
    internal void Transition(ulong resource, uint before, uint after)
    {
        var barrier = new ResourceBarrier(0, 0, resource, AllSubresources, before, after);
        CommandListCalls.Barrier((void*)_list, &barrier);
    }

    /// <summary>Makes a buffer.</summary>
    /// <param name="heapType">Upload, readback or default memory.</param>
    /// <param name="size">The size in bytes.</param>
    /// <param name="state">The state the buffer starts in.</param>
    /// <returns>The ID3D12Resource.</returns>
    /// <exception cref="InvalidOperationException">Direct3D 12 could not make the buffer.</exception>
    internal void* CreateBuffer(uint heapType, ulong size, uint state)
    {
        var heap = new HeapProperties(heapType, 0, 0, 1, 1);
        var description = new ResourceDescription(DimensionBuffer, 0, size, 1, 1, 1, 0, 1, 0, LayoutRowMajor, 0);
        void* buffer;
        HResult.Check(Direct3D12Calls.CreateCommittedResource((void*)_device, &heap, &description, state, &buffer), "make a buffer");
        return buffer;
    }

    /// <summary>Makes an upload buffer, which the CPU writes and the GPU reads.</summary>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The ID3D12Resource.</returns>
    /// <exception cref="InvalidOperationException">Direct3D 12 could not make the buffer.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void* CreateUploadBuffer(ulong size) => CreateBuffer(HeapUpload, size, StateGenericRead);

    /// <summary>Makes a BGRA texture in memory only the GPU uses.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="flags">The resource flags.</param>
    /// <param name="state">The state the texture starts in.</param>
    /// <returns>The ID3D12Resource.</returns>
    /// <exception cref="InvalidOperationException">Direct3D 12 could not make the texture.</exception>
    internal void* CreateTexture(int width, int height, uint flags, uint state)
    {
        var heap = new HeapProperties(HeapDefault, 0, 0, 1, 1);
        var description = new ResourceDescription(DimensionTexture2D, 0, (ulong)width, (uint)height, 1, 1, FormatBgra, 1, 0, 0, flags);
        void* texture;
        HResult.Check(Direct3D12Calls.CreateCommittedResource((void*)_device, &heap, &description, state, &texture), "make a texture");
        return texture;
    }

    /// <summary>Makes the device for one kind of adapter, with a swap chain when there is a window.</summary>
    /// <param name="window">The window handle, or 0.</param>
    /// <param name="software">True to use Windows' software rasteriser instead of the GPU.</param>
    /// <returns><see langword="true"/> when everything was made.</returns>
    private bool TryCreate(nint window, bool software)
    {
        try
        {
            CreateDevice(software);
            CreateQueueAndList();

            // A driver can make a device and still be unable to allocate, so one small texture proves the adapter works before the caller relies on it.
            Direct3D12Calls.Release(CreateTexture(1, 1, AllowRenderTarget, StateRenderTarget));
            if (window != 0)
            {
                CreateSwapChain(window);
            }

            return true;
        }
        catch (InvalidOperationException ex)
        {
            // This adapter cannot do it, so the caller tries the next kind. The reason stays for the message if none works.
            _failure = ex.Message;
            ReleaseObjects();
            return false;
        }
    }

    /// <summary>Makes the factory and the device.</summary>
    /// <param name="software">True to use Windows' software rasteriser.</param>
    /// <exception cref="InvalidOperationException">No device could be made.</exception>
    private void CreateDevice(bool software)
    {
        var factoryId = Guids.Factory;
        void* factory;
        HResult.Check(NativeMethods.CreateFactory(0, &factoryId, &factory), "make the DXGI factory");
        _factory = (ulong)factory;

        void* adapter = null;
        if (software)
        {
            HResult.Check(Direct3D12Calls.EnumWarpAdapter(factory, &adapter), "find the software adapter");
        }

        var deviceId = Guids.Device;
        void* device;
        var made = NativeMethods.CreateDevice(adapter, MinimumFeatureLevel, &deviceId, &device);
        Direct3D12Calls.Release(adapter);
        HResult.Check(made, "make the device");
        _device = (ulong)device;
    }

    /// <summary>Makes the queue, the allocator, the list, the fence and the render target view heap.</summary>
    /// <exception cref="InvalidOperationException">Direct3D 12 rejected one of the objects.</exception>
    private void CreateQueueAndList()
    {
        var queueDescription = new CommandQueueDescription(0, 0, 0, 0);
        void* queue;
        HResult.Check(Direct3D12Calls.CreateCommandQueue((void*)_device, &queueDescription, &queue), "make the command queue");
        _queue = (ulong)queue;

        void* allocator;
        HResult.Check(Direct3D12Calls.CreateCommandAllocator((void*)_device, &allocator), "make the command allocator");
        _allocator = (ulong)allocator;

        void* list;
        HResult.Check(Direct3D12Calls.CreateCommandList((void*)_device, allocator, &list), "make the command list");
        _list = (ulong)list;

        // A new list is open, and a frame starts by resetting it.
        HResult.Check(CommandListCalls.Close(list), CloseAction);

        void* fence;
        HResult.Check(Direct3D12Calls.CreateFence((void*)_device, &fence), "make the fence");
        _fence = (ulong)fence;
        _fenceEvent = NativeMethods.CreateEvent(0, 0, 0, 0);
        if (_fenceEvent == 0)
        {
            throw new InvalidOperationException("Windows could not make the fence event.");
        }

        var heapDescription = new DescriptorHeapDescription(RenderTargetHeapType, BufferCount, 0, 0);
        void* heap;
        HResult.Check(Direct3D12Calls.CreateDescriptorHeap((void*)_device, &heapDescription, &heap), "make the target heap");
        _targetHeap = (ulong)heap;
        _targetStart = Direct3D12Calls.CpuHeapStart(heap);
        _targetStride = Direct3D12Calls.DescriptorIncrement((void*)_device, RenderTargetHeapType);
    }

    /// <summary>Makes the flip model swap chain of the window.</summary>
    /// <param name="window">The window handle.</param>
    /// <exception cref="InvalidOperationException">The swap chain could not be made.</exception>
    private void CreateSwapChain(nint window)
    {
        var description = new SwapChainDescription1(0, 0, FormatBgra, 0, 1, 0, RenderTargetOutput, BufferCount, 0, FlipDiscard, 0, 0);
        void* first;
        HResult.Check(Direct3D12Calls.CreateSwapChainForWindow((void*)_factory, (void*)_queue, window, &description, &first), "make the swap chain");
        var id = Guids.SwapChain;
        void* swapChain;
        var asked = Direct3D12Calls.QueryInterface(first, &id, &swapChain);
        Direct3D12Calls.Release(first);
        HResult.Check(asked, "use the swap chain");
        _swapChain = (ulong)swapChain;
        _ = Direct3D12Calls.MakeWindowAssociation((void*)_factory, window, NoAltEnter);
    }

    /// <summary>Waits until the GPU has finished everything queued so far.</summary>
    /// <exception cref="InvalidOperationException">The GPU did not finish in time.</exception>
    private void WaitIdle()
    {
        var value = ++_fenceValue;
        HResult.Check(Direct3D12Calls.Signal((void*)_queue, (void*)_fence, value), "signal the fence");
        if (Direct3D12Calls.CompletedValue((void*)_fence) >= value)
        {
            return;
        }

        HResult.Check(Direct3D12Calls.SetEventOnCompletion((void*)_fence, value, _fenceEvent), "wait for the GPU");
        if (NativeMethods.WaitForSingleObject(_fenceEvent, WaitMilliseconds) != 0)
        {
            throw new InvalidOperationException("The GPU did not finish the frame in time.");
        }
    }

    /// <summary>Waits for the GPU while the device is being disposed, where a failure has nothing left to protect.</summary>
    private void TryWaitIdle()
    {
        try
        {
            WaitIdle();
        }
        catch (InvalidOperationException)
        {
            // A hung or removed device cannot be waited for, and the objects are released regardless.
        }
    }

    /// <summary>Releases the frame targets and the texture and buffer behind them.</summary>
    private void ReleaseTargets()
    {
        for (var i = 0; i < _targets.Length; i++)
        {
            Direct3D12Calls.Release((void*)_targets[i]);
            _targets[i] = 0;
        }

        Direct3D12Calls.Release((void*)_readBack);
        _readBack = 0;
    }

    /// <summary>Releases every Direct3D object the device holds.</summary>
    private void ReleaseObjects()
    {
        ReleaseTargets();
        Direct3D12Calls.Release((void*)_swapChain);
        Direct3D12Calls.Release((void*)_targetHeap);
        Direct3D12Calls.Release((void*)_fence);
        Direct3D12Calls.Release((void*)_list);
        Direct3D12Calls.Release((void*)_allocator);
        Direct3D12Calls.Release((void*)_queue);
        Direct3D12Calls.Release((void*)_device);
        Direct3D12Calls.Release((void*)_factory);
        if (_fenceEvent != 0)
        {
            _ = NativeMethods.CloseHandle(_fenceEvent);
        }

        _swapChain = 0;
        _targetHeap = 0;
        _fence = 0;
        _list = 0;
        _allocator = 0;
        _queue = 0;
        _device = 0;
        _factory = 0;
        _fenceEvent = 0;
        _fenceValue = 0;
    }
}
