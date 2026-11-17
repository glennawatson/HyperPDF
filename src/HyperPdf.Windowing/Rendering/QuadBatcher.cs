// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Layout;

namespace HyperPdf.Windowing.Rendering;

/// <summary>
/// Gathers the quads of one frame for a GPU backend: every quad becomes six vertices in one array, and neighbouring quads that share
/// a texture and sampler join into one run, so the backend uploads once and makes few draw calls. The arrays are kept between
/// frames, so a steady frame allocates nothing.
/// </summary>
/// <typeparam name="TTile">The backend's tile type.</typeparam>
[DebuggerDisplay("QuadBatcher: {_vertexCount} vertices in {_batches.Count} runs")]
public sealed class QuadBatcher<TTile>
    where TTile : class
{
    /// <summary>The vertices of one quad, as two triangles.</summary>
    private const int QuadVertices = 6;

    /// <summary>The vertices the first array holds, enough for a few hundred quads.</summary>
    private const int FirstVertexCapacity = 4096;

    /// <summary>The alpha of a colour that is fully opaque.</summary>
    private const uint OpaqueAlpha = 255;

    /// <summary>The bit shift of the alpha channel in 0xAARRGGBB.</summary>
    private const int AlphaShift = 24;

    /// <summary>The mask of the red, green and blue bits of a colour.</summary>
    private const uint ColorMask = 0x00FFFFFF;

    /// <summary>The runs of the current frame.</summary>
    private readonly List<QuadBatch<TTile>> _batches = [];

    /// <summary>The vertices of the current frame, grown when a frame has more.</summary>
    private QuadVertex[] _vertices = new QuadVertex[FirstVertexCapacity];

    /// <summary>The number of vertices in the current frame.</summary>
    private int _vertexCount;

    /// <summary>The rectangle the quads being added are limited to, or <see langword="null"/> for the whole frame.</summary>
    private LayoutRect? _clip;

    /// <summary>How much the quads being added count, out of 255.</summary>
    private uint _opacity = OpaqueAlpha;

    /// <summary>Gets the vertices of the current frame.</summary>
    public ReadOnlySpan<QuadVertex> Vertices => _vertices.AsSpan(0, _vertexCount);

    /// <summary>Gets the runs of the current frame, in drawing order.</summary>
    public ReadOnlySpan<QuadBatch<TTile>> Batches => CollectionsMarshal.AsSpan(_batches);

    /// <summary>Starts a new frame.</summary>
    public void Reset()
    {
        _batches.Clear();
        _vertexCount = 0;
        _clip = null;
        _opacity = OpaqueAlpha;
    }

    /// <summary>Limits the quads added from now on to a rectangle of the frame, until <see cref="ClearClip"/>.</summary>
    /// <param name="area">The rectangle in frame pixels.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetClip(in LayoutRect area) => _clip = area;

    /// <summary>Lets the quads added from now on reach the whole frame again.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearClip() => _clip = null;

    /// <summary>
    /// Makes the quads added from now on count for only that much, until it is set to 1. The shaders multiply what they draw by the vertex
    /// colour, so a tile fades by the colour being white at that alpha and a fill by its own alpha being lower.
    /// </summary>
    /// <param name="opacity">How much the quads count, from 0 to 1.</param>
    public void SetOpacity(double opacity) => _opacity = (uint)Math.Round(Math.Clamp(opacity, 0, 1) * OpaqueAlpha);

    /// <summary>Adds a clear of the whole frame.</summary>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddClear(uint argb) => _batches.Add(new(IsClear: true, null, Smooth: false, _vertexCount, 0, argb));

    /// <summary>Adds a quad, joining the run before it when both use the same texture and sampler.</summary>
    /// <param name="tile">The tile the quad shows, or <see langword="null"/> for a solid fill.</param>
    /// <param name="smooth">Whether the tile is sampled smoothly.</param>
    /// <param name="area">Where the quad goes, in frame pixels.</param>
    /// <param name="argb">The colour as 0xAARRGGBB, white for a tile.</param>
    public void AddQuad(TTile? tile, bool smooth, in LayoutRect area, uint argb)
    {
        if (_vertexCount + QuadVertices > _vertices.Length)
        {
            Array.Resize(ref _vertices, _vertices.Length + _vertices.Length);
        }

        var color = PremultipliedColor.ToVertexColor(_opacity == OpaqueAlpha ? argb : Faded(argb, _opacity));
        var left = (float)area.X;
        var top = (float)area.Y;
        var right = (float)(area.X + area.Width);
        var bottom = (float)(area.Y + area.Height);
        var topLeft = new QuadVertex(left, top, 0, 0, color);
        var topRight = new QuadVertex(right, top, 1, 0, color);
        var bottomLeft = new QuadVertex(left, bottom, 0, 1, color);
        var bottomRight = new QuadVertex(right, bottom, 1, 1, color);

        // Two triangles that share the diagonal from the top right to the bottom left.
        ReadOnlySpan<QuadVertex> quad = [topLeft, topRight, bottomLeft, topRight, bottomRight, bottomLeft];
        quad.CopyTo(_vertices.AsSpan(_vertexCount, QuadVertices));
        Join(tile, smooth);
        _vertexCount += QuadVertices;
    }

    /// <summary>Lowers the alpha of a colour by an opacity.</summary>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    /// <param name="opacity">The opacity out of 255.</param>
    /// <returns>The colour with its alpha scaled.</returns>
    private static uint Faded(uint argb, uint opacity)
    {
        var alpha = (uint)PremultipliedColor.DivideBy255((int)((argb >> AlphaShift) * opacity));
        return (argb & ColorMask) | (alpha << AlphaShift);
    }

    /// <summary>Extends the last run by one quad, or starts a new run when the texture or sampler changes.</summary>
    /// <param name="tile">The tile, or <see langword="null"/> for a solid fill.</param>
    /// <param name="smooth">Whether the tile is sampled smoothly.</param>
    private void Join(TTile? tile, bool smooth)
    {
        if (_batches.Count > 0)
        {
            var last = _batches[^1];
            if (!last.IsClear && ReferenceEquals(last.Tile, tile) && last.Smooth == smooth && last.Clip == _clip)
            {
                _batches[^1] = last with { Count = last.Count + QuadVertices };
                return;
            }
        }

        _batches.Add(new(IsClear: false, tile, smooth, _vertexCount, QuadVertices, 0) { Clip = _clip });
    }
}
