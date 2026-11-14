// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Rendering.Metal.Interop;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Metal;

/// <summary>
/// The Metal objects of the backend and the drawing of a frame: the GPU, its command queue, the pipeline with premultiplied
/// blending, the two samplers, a white texture for solid fills, the vertex buffer and where frames go, which is a CAMetalLayer or
/// a texture that tests read back.
/// </summary>
[DebuggerDisplay("MetalDevice: {Width} x {Height}")]
internal sealed unsafe class MetalDevice : IDisposable
{
    /// <summary>The pixel format of 8 bit BGRA.</summary>
    private const nuint FormatBgra = 80;

    /// <summary>The blend factor one.</summary>
    private const nuint BlendOne = 1;

    /// <summary>The blend factor one minus the source alpha.</summary>
    private const nuint BlendOneMinusSourceAlpha = 5;

    /// <summary>The sampler filter that blends the four nearest texels.</summary>
    private const nuint FilterLinear = 1;

    /// <summary>The texture usage of a texture that is drawn into and read by shaders.</summary>
    private const nuint UsageRenderTargetAndRead = 5;

    /// <summary>The load action that keeps what the frame held.</summary>
    private const nuint LoadActionLoad = 1;

    /// <summary>The load action that clears the frame.</summary>
    private const nuint LoadActionClear = 2;

    /// <summary>The store action that keeps what was drawn.</summary>
    private const nuint StoreActionStore = 1;

    /// <summary>The primitive type of a triangle list.</summary>
    private const nuint PrimitiveTriangle = 3;

    /// <summary>The storage mode of a texture with a CPU copy the GPU must be asked to refresh.</summary>
    private const nuint StorageManaged = 1;

    /// <summary>The buffer index of the frame constants in the vertex function.</summary>
    private const nuint FrameConstantsIndex = 1;

    /// <summary>The size of the frame constants: the two scale floats and two unused floats.</summary>
    private const int FrameConstantsFloats = 4;

    /// <summary>The width and height of clip space, which runs from -1 to 1.</summary>
    private const float ClipSpan = 2F;

    /// <summary>The largest channel value.</summary>
    private const double ChannelMax = 255;

    /// <summary>The mask of one channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The bit shift of the red channel in 0xAARRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit shift of the green channel in 0xAARRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The bit shift of the alpha channel in 0xAARRGGBB.</summary>
    private const int AlphaShift = 24;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The vertices the first vertex buffer holds, enough for a few hundred quads.</summary>
    private const int FirstVertexCapacity = 4096;

    /// <summary>The CAMetalLayer frames are shown in, or 0 when they go to a texture.</summary>
    private readonly ulong _layer;

    /// <summary>The MTLDevice.</summary>
    private ulong _device;

    /// <summary>The MTLCommandQueue.</summary>
    private ulong _queue;

    /// <summary>The MTLRenderPipelineState.</summary>
    private ulong _pipeline;

    /// <summary>The sampler that picks the nearest texel.</summary>
    private ulong _nearest;

    /// <summary>The sampler that blends the four nearest texels.</summary>
    private ulong _linear;

    /// <summary>The white texture solid fills sample.</summary>
    private ulong _white;

    /// <summary>The vertex buffer.</summary>
    private ulong _vertices;

    /// <summary>The texture frames go to when there is no layer.</summary>
    private ulong _frameTexture;

    /// <summary>The number of vertices the vertex buffer holds.</summary>
    private int _vertexCapacity;

    /// <summary>Initializes a new instance of the <see cref="MetalDevice"/> class.</summary>
    /// <param name="layer">The CAMetalLayer, or 0 to draw into a texture.</param>
    /// <exception cref="InvalidOperationException">The machine has no GPU Metal can use, or Metal rejected the shaders.</exception>
    internal MetalDevice(nint layer)
    {
        _layer = (ulong)layer;
        var device = NativeMethods.CreateSystemDefaultDevice();
        if (device == 0)
        {
            throw new InvalidOperationException("The machine has no GPU Metal can use.");
        }

        _device = (ulong)device;
        var pool = NativeMethods.AutoreleasePoolPush();
        try
        {
            _queue = (ulong)NativeMethods.Send(device, Selectors.NewCommandQueue);
            _pipeline = (ulong)CreatePipeline(device);
            _nearest = (ulong)CreateSampler(device, 0);
            _linear = (ulong)CreateSampler(device, FilterLinear);
            _white = (ulong)CreateTexture(device, 1, 1, renderTarget: false);
            var white = uint.MaxValue;
            NativeMethods.SendReplaceRegion((nint)_white, Selectors.ReplaceRegion, new(0, 0, 0, 1, 1, 1), 0, &white, sizeof(uint));
            _vertexCapacity = FirstVertexCapacity;
            _vertices = (ulong)NativeMethods.SendNewBuffer(device, Selectors.NewBuffer, (nuint)(FirstVertexCapacity * sizeof(QuadVertex)), 0);
            if (layer != 0)
            {
                NativeMethods.SendVoid(layer, Selectors.SetDevice, device);
                NativeMethods.SendVoidIndex(layer, Selectors.SetPixelFormat, FormatBgra);
                NativeMethods.SendVoidFlag(layer, Selectors.SetFramebufferOnly, 1);
            }
        }
        catch (InvalidOperationException)
        {
            Dispose();
            throw;
        }
        finally
        {
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>Gets the frame width in pixels.</summary>
    internal int Width { get; private set; }

    /// <summary>Gets the frame height in pixels.</summary>
    internal int Height { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Release(ref _frameTexture);
        Release(ref _vertices);
        Release(ref _white);
        Release(ref _linear);
        Release(ref _nearest);
        Release(ref _pipeline);
        Release(ref _queue);
        Release(ref _device);
    }

    /// <summary>Releases the textures of tiles that were disposed since the last frame.</summary>
    /// <param name="retired">The textures.</param>
    internal static void ReleaseRetired(ConcurrentQueue<ulong> retired)
    {
        while (retired.TryDequeue(out var texture))
        {
            NativeMethods.SendVoid((nint)texture, Selectors.Release);
        }
    }

    /// <summary>Makes the frame target again for a new size.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal void Resize(int width, int height)
    {
        Width = width;
        Height = height;
        Release(ref _frameTexture);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (_layer != 0)
        {
            NativeMethods.SendSize((nint)_layer, Selectors.SetDrawableSize, new(width, height));
            return;
        }

        var pool = NativeMethods.AutoreleasePoolPush();
        _frameTexture = (ulong)CreateTexture((nint)_device, width, height, renderTarget: true);
        NativeMethods.AutoreleasePoolPop(pool);
    }

    /// <summary>Uploads what changed, draws the runs of a frame and presents it.</summary>
    /// <param name="quads">The quads of the frame.</param>
    internal void DrawFrame(QuadBatcher<MetalTileSurface> quads)
    {
        // The command buffer, the pass descriptor and the drawable are autoreleased, so each frame has its own pool.
        var pool = NativeMethods.AutoreleasePoolPush();
        try
        {
            var drawable = _layer != 0 ? NativeMethods.Send((nint)_layer, Selectors.NextDrawable) : 0;
            var target = drawable != 0 ? NativeMethods.Send(drawable, Selectors.Texture) : (nint)_frameTexture;
            if (target == 0)
            {
                return;
            }

            UploadTiles(quads.Batches);
            UploadVertices(quads.Vertices);
            var commands = NativeMethods.Send((nint)_queue, Selectors.CommandBuffer);
            var encoder = NativeMethods.Send(commands, Selectors.RenderCommandEncoder, DescribePass(target, quads.Batches));
            Encode(encoder, quads.Batches);
            NativeMethods.SendVoid(encoder, Selectors.EndEncoding);
            if (drawable != 0)
            {
                NativeMethods.SendVoid(commands, Selectors.PresentDrawable, drawable);
            }

            NativeMethods.SendVoid(commands, Selectors.Commit);
        }
        finally
        {
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>Reads the frame texture as BGRA with the top line first. Only frames drawn without a layer can be read.</summary>
    /// <param name="destination">Receives width * height * 4 bytes.</param>
    /// <exception cref="InvalidOperationException">The frame goes to a layer.</exception>
    internal void ReadPixels(Span<byte> destination)
    {
        if (_frameTexture == 0)
        {
            throw new InvalidOperationException("Only a frame drawn without a layer can be read back.");
        }

        var pool = NativeMethods.AutoreleasePoolPush();
        try
        {
            // Waiting on an empty command buffer waits for the frame before it, because the queue runs its buffers in order.
            var commands = NativeMethods.Send((nint)_queue, Selectors.CommandBuffer);
            if (NativeMethods.SendCount((nint)_frameTexture, Selectors.StorageMode) == StorageManaged)
            {
                var blit = NativeMethods.Send(commands, Selectors.BlitCommandEncoder);
                NativeMethods.SendVoid(blit, Selectors.SynchronizeResource, (nint)_frameTexture);
                NativeMethods.SendVoid(blit, Selectors.EndEncoding);
            }

            NativeMethods.SendVoid(commands, Selectors.Commit);
            NativeMethods.SendVoid(commands, Selectors.WaitUntilCompleted);
            fixed (byte* pixels = destination)
            {
                NativeMethods.SendGetBytes((nint)_frameTexture, Selectors.GetBytes, pixels, (nuint)(Width * BytesPerPixel), new(0, 0, 0, (nuint)Width, (nuint)Height, 1), 0);
            }
        }
        finally
        {
            NativeMethods.AutoreleasePoolPop(pool);
        }
    }

    /// <summary>Compiles the shaders and makes the pipeline with premultiplied alpha blending.</summary>
    /// <param name="device">The GPU.</param>
    /// <returns>The pipeline state.</returns>
    /// <exception cref="InvalidOperationException">Metal rejected the shaders or the pipeline.</exception>
    private static nint CreatePipeline(nint device)
    {
        nint error;
        nint library;
        fixed (byte* source = MetalShaderSource.Text)
        {
            library = NativeMethods.SendCompile(device, Selectors.NewLibraryWithSource, Text(source), 0, &error);
        }

        if (library == 0)
        {
            throw new InvalidOperationException("Metal rejected the compositor shaders.");
        }

        var vertex = NewFunction(library, "vertexMain\0"u8);
        var fragment = NewFunction(library, "fragmentMain\0"u8);
        var descriptor = NativeMethods.Send(NativeMethods.Send(NativeMethods.GetClass("MTLRenderPipelineDescriptor"), Selectors.Alloc), Selectors.Init);
        NativeMethods.SendVoid(descriptor, Selectors.SetVertexFunction, vertex);
        NativeMethods.SendVoid(descriptor, Selectors.SetFragmentFunction, fragment);
        var attachment = NativeMethods.SendIndex(NativeMethods.Send(descriptor, Selectors.ColorAttachments), Selectors.ObjectAtIndexedSubscript, 0);
        NativeMethods.SendVoidIndex(attachment, Selectors.SetPixelFormat, FormatBgra);
        NativeMethods.SendVoidFlag(attachment, Selectors.SetBlendingEnabled, 1);
        NativeMethods.SendVoidIndex(attachment, Selectors.SetSourceRgbBlendFactor, BlendOne);
        NativeMethods.SendVoidIndex(attachment, Selectors.SetSourceAlphaBlendFactor, BlendOne);
        NativeMethods.SendVoidIndex(attachment, Selectors.SetDestinationRgbBlendFactor, BlendOneMinusSourceAlpha);
        NativeMethods.SendVoidIndex(attachment, Selectors.SetDestinationAlphaBlendFactor, BlendOneMinusSourceAlpha);
        var pipeline = NativeMethods.SendWithError(device, Selectors.NewRenderPipelineState, descriptor, &error);
        NativeMethods.SendVoid(descriptor, Selectors.Release);
        NativeMethods.SendVoid(fragment, Selectors.Release);
        NativeMethods.SendVoid(vertex, Selectors.Release);
        NativeMethods.SendVoid(library, Selectors.Release);
        return pipeline != 0 ? pipeline : throw new InvalidOperationException("Metal rejected the compositor pipeline.");
    }

    /// <summary>Gets a function of a shader library.</summary>
    /// <param name="library">The library.</param>
    /// <param name="name">The null terminated function name.</param>
    /// <returns>The function.</returns>
    private static nint NewFunction(nint library, ReadOnlySpan<byte> name)
    {
        fixed (byte* text = name)
        {
            return NativeMethods.Send(library, Selectors.NewFunctionWithName, Text(text));
        }
    }

    /// <summary>Makes an autoreleased NSString.</summary>
    /// <param name="text">The null terminated UTF-8 text.</param>
    /// <returns>The string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nint Text(byte* text) => NativeMethods.SendText(NativeMethods.GetClass("NSString"), Selectors.StringWithUtf8, text);

    /// <summary>Makes a sampler that clamps at the edges, which is Metal's default.</summary>
    /// <param name="device">The GPU.</param>
    /// <param name="filter">The filter, 0 for the nearest texel.</param>
    /// <returns>The sampler state.</returns>
    private static nint CreateSampler(nint device, nuint filter)
    {
        var descriptor = NativeMethods.Send(NativeMethods.Send(NativeMethods.GetClass("MTLSamplerDescriptor"), Selectors.Alloc), Selectors.Init);
        NativeMethods.SendVoidIndex(descriptor, Selectors.SetMinFilter, filter);
        NativeMethods.SendVoidIndex(descriptor, Selectors.SetMagFilter, filter);
        var sampler = NativeMethods.Send(device, Selectors.NewSamplerState, descriptor);
        NativeMethods.SendVoid(descriptor, Selectors.Release);
        return sampler;
    }

    /// <summary>Makes a BGRA texture.</summary>
    /// <param name="device">The GPU.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="renderTarget">Whether frames are drawn into the texture.</param>
    /// <returns>The texture.</returns>
    private static nint CreateTexture(nint device, int width, int height, bool renderTarget)
    {
        var descriptor = NativeMethods.SendTextureDescriptor(NativeMethods.GetClass("MTLTextureDescriptor"), Selectors.Texture2DDescriptor, FormatBgra, (nuint)width, (nuint)height, 0);
        if (renderTarget)
        {
            NativeMethods.SendVoidIndex(descriptor, Selectors.SetUsage, UsageRenderTargetAndRead);
        }

        return NativeMethods.Send(device, Selectors.NewTexture, descriptor);
    }

    /// <summary>Releases an object and forgets it.</summary>
    /// <param name="handle">The object, or 0.</param>
    private static void Release(ref ulong handle)
    {
        if (handle == 0)
        {
            return;
        }

        NativeMethods.SendVoid((nint)handle, Selectors.Release);
        handle = 0;
    }

    /// <summary>Describes the render pass of a frame: where it draws and whether it starts from a clear.</summary>
    /// <param name="target">The texture drawn into.</param>
    /// <param name="batches">The runs of the frame.</param>
    /// <returns>The autoreleased pass descriptor.</returns>
    private static nint DescribePass(nint target, ReadOnlySpan<QuadBatch<MetalTileSurface>> batches)
    {
        var pass = NativeMethods.Send(NativeMethods.GetClass("MTLRenderPassDescriptor"), Selectors.RenderPassDescriptor);
        var attachment = NativeMethods.SendIndex(NativeMethods.Send(pass, Selectors.ColorAttachments), Selectors.ObjectAtIndexedSubscript, 0);
        NativeMethods.SendVoid(attachment, Selectors.SetTexture, target);
        NativeMethods.SendVoidIndex(attachment, Selectors.SetStoreAction, StoreActionStore);
        if (batches.IsEmpty || !batches[0].IsClear)
        {
            NativeMethods.SendVoidIndex(attachment, Selectors.SetLoadAction, LoadActionLoad);
            return pass;
        }

        var color = PremultipliedColor.FromArgb(batches[0].Color);
        NativeMethods.SendVoidIndex(attachment, Selectors.SetLoadAction, LoadActionClear);
        NativeMethods.SendClearColor(
            attachment,
            Selectors.SetClearColor,
            new(((color >> RedShift) & ChannelMask) / ChannelMax, ((color >> GreenShift) & ChannelMask) / ChannelMax, (color & ChannelMask) / ChannelMax, (color >> AlphaShift) / ChannelMax));
        return pass;
    }

    /// <summary>Makes the texture of every tile in the frame that has none and uploads the pixels of the tiles that changed.</summary>
    /// <param name="batches">The runs of the frame.</param>
    private void UploadTiles(ReadOnlySpan<QuadBatch<MetalTileSurface>> batches)
    {
        foreach (ref readonly var batch in batches)
        {
            if (batch.Tile is not { } tile)
            {
                continue;
            }

            if (tile.Texture == 0)
            {
                tile.Texture = (ulong)CreateTexture((nint)_device, tile.Width, tile.Height, renderTarget: false);
            }

            // Taking the dirty flag clears it, so a tile drawn twice in one frame is uploaded only the first time.
            if (tile.Texture != 0 && tile.TakeDirty())
            {
                fixed (byte* pixels = tile.Pixels)
                {
                    NativeMethods.SendReplaceRegion((nint)tile.Texture, Selectors.ReplaceRegion, new(0, 0, 0, (nuint)tile.Width, (nuint)tile.Height, 1), 0, pixels, (nuint)tile.Stride);
                }
            }
        }
    }

    /// <summary>Copies the frame's vertices into the vertex buffer, making a larger buffer when they do not fit.</summary>
    /// <param name="vertices">The vertices.</param>
    private void UploadVertices(ReadOnlySpan<QuadVertex> vertices)
    {
        if (vertices.Length > _vertexCapacity)
        {
            Release(ref _vertices);
            _vertexCapacity = Math.Max(vertices.Length, _vertexCapacity + _vertexCapacity);
            _vertices = (ulong)NativeMethods.SendNewBuffer((nint)_device, Selectors.NewBuffer, (nuint)(_vertexCapacity * sizeof(QuadVertex)), 0);
        }

        if (!vertices.IsEmpty && _vertices != 0)
        {
            vertices.CopyTo(new((void*)NativeMethods.Send((nint)_vertices, Selectors.Contents), vertices.Length));
        }
    }

    /// <summary>Encodes every run in order, changing the texture and sampler only when a run needs a different one.</summary>
    /// <param name="encoder">The render command encoder.</param>
    /// <param name="batches">The runs of the frame.</param>
    private void Encode(nint encoder, ReadOnlySpan<QuadBatch<MetalTileSurface>> batches)
    {
        var scale = stackalloc float[FrameConstantsFloats];
        scale[0] = ClipSpan / Width;
        scale[1] = ClipSpan / Height;
        NativeMethods.SendVoid(encoder, Selectors.SetRenderPipelineState, (nint)_pipeline);
        NativeMethods.SendVertexBuffer(encoder, Selectors.SetVertexBuffer, (nint)_vertices, 0, 0);
        NativeMethods.SendVertexBytes(encoder, Selectors.SetVertexBytes, scale, FrameConstantsFloats * sizeof(float), FrameConstantsIndex);
        ulong boundTexture = 0;
        ulong boundSampler = 0;
        foreach (ref readonly var batch in batches)
        {
            if (batch.IsClear)
            {
                // The pass starts from the clear, so there is nothing to draw for it.
                continue;
            }

            var texture = batch.Tile is { } tile ? tile.Texture : _white;
            if (texture != boundTexture)
            {
                NativeMethods.SendVoidAtIndex(encoder, Selectors.SetFragmentTexture, (nint)texture, 0);
                boundTexture = texture;
            }

            var sampler = batch.Smooth ? _linear : _nearest;
            if (sampler != boundSampler)
            {
                NativeMethods.SendVoidAtIndex(encoder, Selectors.SetFragmentSamplerState, (nint)sampler, 0);
                boundSampler = sampler;
            }

            NativeMethods.SendDraw(encoder, Selectors.DrawPrimitives, PrimitiveTriangle, (nuint)batch.First, (nuint)batch.Count);
        }
    }
}
