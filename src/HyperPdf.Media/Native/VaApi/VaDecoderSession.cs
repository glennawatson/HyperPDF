// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Codecs;
using HyperPdf.Codecs.Video;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>A VA-API decoding context and the surfaces pictures are decoded into: it hands out surfaces, submits the buffers of a picture and reads decoded pictures back.</summary>
[DebuggerDisplay("VaDecoderSession: {_free.Count} free surfaces")]
internal sealed unsafe class VaDecoderSession : IDisposable
{
    /// <summary>The entry point that decodes pictures (<c>VAEntrypointVLD</c>).</summary>
    private const int DecodeEntrypoint = 1;

    /// <summary>The configuration attribute that names the render target format (<c>VAConfigAttribRTFormat</c>).</summary>
    private const int RenderTargetFormat = 0;

    /// <summary>The render target format of 8 bit 4:2:0 pictures.</summary>
    private const uint Yuv420 = 1;

    /// <summary>The shift from a luma row to a chroma row of 4:2:0 pictures.</summary>
    private const int ChromaRowShift = 1;

    /// <summary>The size of a <c>VAImage</c> in bytes.</summary>
    private const int ImageSize = 120;

    /// <summary>The offset in a <c>VAImage</c> of its identity.</summary>
    private const int ImageIdAt = 0;

    /// <summary>The offset in a <c>VAImage</c> of its buffer.</summary>
    private const int ImageBufferAt = 52;

    /// <summary>The offset in a <c>VAImage</c> of the bytes of data it holds.</summary>
    private const int ImageDataSizeAt = 60;

    /// <summary>The offset in a <c>VAImage</c> of the row pitch of each plane.</summary>
    private const int ImagePitchesAt = 68;

    /// <summary>The offset in a <c>VAImage</c> of the start of each plane.</summary>
    private const int ImageOffsetsAt = 80;

    /// <summary>The display.</summary>
    private readonly VaDisplay _display;

    /// <summary>The surfaces not in use.</summary>
    private readonly Stack<uint> _free = new();

    /// <summary>Every surface, so they can be destroyed.</summary>
    private readonly uint[] _surfaces;

    /// <summary>The buffers made for the picture being submitted.</summary>
    private readonly List<uint> _buffers = [];

    /// <summary>The configuration.</summary>
    private uint _config;

    /// <summary>The context.</summary>
    private uint _context;

    /// <summary>Initializes a new instance of the <see cref="VaDecoderSession"/> class.</summary>
    /// <param name="display">The display.</param>
    /// <param name="surfaceCount">The number of surfaces.</param>
    private VaDecoderSession(VaDisplay display, int surfaceCount)
    {
        _display = display;
        _surfaces = new uint[surfaceCount];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_display.IsInvalid)
        {
            DestroyAll();
        }

        _display.Dispose();
    }

    /// <summary>Opens a display and makes a context that decodes pictures of a profile.</summary>
    /// <param name="profile">The VA-API profile.</param>
    /// <param name="width">The coded width in pixels.</param>
    /// <param name="height">The coded height in pixels.</param>
    /// <param name="surfaceCount">The number of surfaces.</param>
    /// <param name="failure">Receives why no session was made, when none was.</param>
    /// <returns>The session, or <see langword="null"/> when this system cannot decode the profile.</returns>
    internal static VaDecoderSession? TryOpen(int profile, int width, int height, int surfaceCount, out DecoderFailure failure)
    {
        var display = VaDisplay.TryOpen(out failure);
        if (display is null)
        {
            return null;
        }

        var session = new VaDecoderSession(display, surfaceCount);
        if (session.Start(profile, width, height, out failure))
        {
            return session;
        }

        session.Dispose();
        return null;
    }

    /// <summary>Takes a surface to decode into.</summary>
    /// <returns>The surface.</returns>
    /// <exception cref="InvalidDataException">Every surface is in use.</exception>
    internal uint Rent() => _free.TryPop(out var surface) ? surface : throw new InvalidDataException("The decoder ran out of surfaces.");

    /// <summary>Gives a surface back.</summary>
    /// <param name="surface">The surface.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Return(uint surface) => _free.Push(surface);

    /// <summary>Starts a picture.</summary>
    /// <param name="target">The surface the picture is decoded into.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Begin(uint target) => Require(NativeMethods.BeginPicture(_display.Display, _context, target), "begin a picture");

    /// <summary>Gives the driver a buffer of parameters for the picture being submitted.</summary>
    /// <param name="type">The buffer type.</param>
    /// <param name="block">The contents.</param>
    internal void Add(int type, VaBlock block)
    {
        fixed (byte* bytes = block.Bytes)
        {
            Add(type, bytes, (uint)block.Bytes.Length);
        }
    }

    /// <summary>Gives the driver a buffer of data for the picture being submitted.</summary>
    /// <param name="type">The buffer type.</param>
    /// <param name="data">The contents, which are copied.</param>
    /// <param name="size">The bytes.</param>
    internal void Add(int type, byte* data, uint size)
    {
        uint buffer;
        Require(NativeMethods.CreateBuffer(_display.Display, _context, type, size, 1, data, &buffer), "make a buffer");
        _buffers.Add(buffer);
    }

    /// <summary>Hands the driver every buffer added since the picture began and finishes the picture, then lets the buffers go.</summary>
    internal void End()
    {
        try
        {
            var buffers = _buffers.ToArray();
            fixed (uint* pointer = buffers)
            {
                Require(NativeMethods.RenderPicture(_display.Display, _context, pointer, buffers.Length), "submit a picture");
            }

            Require(NativeMethods.EndPicture(_display.Display, _context), "finish a picture");
        }
        finally
        {
            foreach (var buffer in _buffers)
            {
                _ = NativeMethods.DestroyBuffer(_display.Display, buffer);
            }

            _buffers.Clear();
        }
    }

    /// <summary>Waits for a picture to be decoded and copies it into a frame.</summary>
    /// <param name="surface">The surface the picture was decoded into.</param>
    /// <param name="frame">Receives the picture; its size says how much is copied.</param>
    /// <param name="left">The column in the surface where the frame starts.</param>
    /// <param name="top">The row in the surface where the frame starts.</param>
    /// <exception cref="InvalidDataException">The picture could not be read back.</exception>
    internal void Read(uint surface, VideoFrame frame, int left, int top)
    {
        Require(NativeMethods.SyncSurface(_display.Display, surface), "wait for a picture");
        var image = stackalloc byte[ImageSize];
        Require(NativeMethods.DeriveImage(_display.Display, surface, image), "read a picture");
        try
        {
            void* memory;
            var buffer = *(uint*)(image + ImageBufferAt);
            Require(NativeMethods.MapBuffer(_display.Display, buffer, &memory), "map a picture");
            try
            {
                CopyPlanes(image, (byte*)memory, frame, left, top);
            }
            finally
            {
                _ = NativeMethods.UnmapBuffer(_display.Display, buffer);
            }
        }
        finally
        {
            _ = NativeMethods.DestroyImage(_display.Display, *(uint*)(image + ImageIdAt));
        }
    }

    /// <summary>Throws when a VA-API call failed.</summary>
    /// <param name="status">The VAStatus.</param>
    /// <param name="step">What was being done.</param>
    /// <exception cref="InvalidDataException">The call failed.</exception>
    private static void Require(int status, string step)
    {
        if (status != 0)
        {
            throw new InvalidDataException($"The graphics driver could not {step} (0x{status:X8}).");
        }
    }

    /// <summary>Copies the planes of a mapped NV12 image into a frame.</summary>
    /// <param name="image">The <c>VAImage</c> structure.</param>
    /// <param name="memory">The mapped memory.</param>
    /// <param name="frame">Receives the picture.</param>
    /// <param name="left">The column where the frame starts.</param>
    /// <param name="top">The row where the frame starts.</param>
    private static void CopyPlanes(byte* image, byte* memory, VideoFrame frame, int left, int top)
    {
        var pitches = (uint*)(image + ImagePitchesAt);
        var offsets = (uint*)(image + ImageOffsetsAt);
        var dataSize = *(uint*)(image + ImageDataSizeAt);
        var lumaPitch = (int)pitches[0];
        var colourPitch = (int)pitches[1];
        var lumaStart = (top * lumaPitch) + left;
        var colourStart = ((top >> ChromaRowShift) * colourPitch) + left;
        var lumaLength = (int)offsets[1] - (int)offsets[0] - lumaStart;
        var colourLength = (int)dataSize - (int)offsets[1] - colourStart;
        Nv12Planes.Copy(
            new(memory + offsets[0] + lumaStart, lumaLength),
            lumaPitch,
            new(memory + offsets[1] + colourStart, colourLength),
            colourPitch,
            frame);
    }

    /// <summary>Destroys the context, the surfaces and the configuration that were made.</summary>
    private void DestroyAll()
    {
        if (_context != 0)
        {
            _ = NativeMethods.DestroyContext(_display.Display, _context);
        }

        fixed (uint* pointer = _surfaces)
        {
            _ = NativeMethods.DestroySurfaces(_display.Display, pointer, _surfaces.Length);
        }

        if (_config != 0)
        {
            _ = NativeMethods.DestroyConfig(_display.Display, _config);
        }
    }

    /// <summary>Makes the configuration, the surfaces and the context.</summary>
    /// <param name="profile">The VA-API profile.</param>
    /// <param name="width">The coded width in pixels.</param>
    /// <param name="height">The coded height in pixels.</param>
    /// <param name="failure">Receives why it failed, when it did.</param>
    /// <returns><see langword="true"/> when the context is ready.</returns>
    private bool Start(int profile, int width, int height, out DecoderFailure failure)
    {
        var attribute = new ConfigAttribute(RenderTargetFormat, Yuv420);
        uint config;
        var status = NativeMethods.CreateConfig(_display.Display, profile, DecodeEntrypoint, &attribute, 1, &config);
        if (status != 0)
        {
            failure = new("The graphics driver cannot decode this coding", status);
            return false;
        }

        _config = config;
        fixed (uint* surfaces = _surfaces)
        {
            status = NativeMethods.CreateSurfaces(_display.Display, Yuv420, (uint)width, (uint)height, surfaces, (uint)_surfaces.Length, null, 0);
            if (status != 0)
            {
                failure = new("The graphics driver could not make surfaces", status);
                return false;
            }

            uint context;
            status = NativeMethods.CreateContext(_display.Display, _config, width, height, 0, surfaces, _surfaces.Length, &context);
            if (status != 0)
            {
                failure = new("The graphics driver could not make a decoding context", status);
                return false;
            }

            _context = context;
        }

        foreach (var surface in _surfaces)
        {
            _free.Push(surface);
        }

        failure = default;
        return true;
    }
}
