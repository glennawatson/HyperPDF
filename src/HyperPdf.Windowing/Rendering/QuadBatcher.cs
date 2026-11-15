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

    /// <summary>The runs of the current frame.</summary>
    private readonly List<QuadBatch<TTile>> _batches = [];

    /// <summary>The vertices of the current frame, grown when a frame has more.</summary>
    private QuadVertex[] _vertices = new QuadVertex[FirstVertexCapacity];

    /// <summary>The number of vertices in the current frame.</summary>
    private int _vertexCount;

    /// <summary>Gets the vertices of the current frame.</summary>
    public ReadOnlySpan<QuadVertex> Vertices => _vertices.AsSpan(0, _vertexCount);

    /// <summary>Gets the runs of the current frame, in drawing order.</summary>
    public ReadOnlySpan<QuadBatch<TTile>> Batches => CollectionsMarshal.AsSpan(_batches);

    /// <summary>Starts a new frame.</summary>
    public void Reset()
    {
        _batches.Clear();
        _vertexCount = 0;
    }

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

        var color = PremultipliedColor.ToVertexColor(argb);
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

    /// <summary>Extends the last run by one quad, or starts a new run when the texture or sampler changes.</summary>
    /// <param name="tile">The tile, or <see langword="null"/> for a solid fill.</param>
    /// <param name="smooth">Whether the tile is sampled smoothly.</param>
    private void Join(TTile? tile, bool smooth)
    {
        if (_batches.Count > 0)
        {
            var last = _batches[^1];
            if (!last.IsClear && ReferenceEquals(last.Tile, tile) && last.Smooth == smooth)
            {
                _batches[^1] = last with { Count = last.Count + QuadVertices };
                return;
            }
        }

        _batches.Add(new(IsClear: false, tile, smooth, _vertexCount, QuadVertices, 0));
    }
}
