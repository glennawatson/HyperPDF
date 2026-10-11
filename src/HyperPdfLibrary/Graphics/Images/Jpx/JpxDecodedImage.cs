// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The decoded components of a codestream: one plane of integer samples per component at its own resolution, already
/// level-shifted and clamped to the component's range. The planes are pooled; dispose to return them.
/// </summary>
[DebuggerDisplay("JpxDecodedImage: {Geometry.Image.Width}x{Geometry.Image.Height}")]
internal sealed class JpxDecodedImage : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="JpxDecodedImage"/> class with every sample zero.</summary>
    /// <param name="geometry">The image geometry.</param>
    internal JpxDecodedImage(JpxGeometry geometry)
        : this(geometry, 0)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="JpxDecodedImage"/> class at a selected resolution level.</summary>
    /// <param name="geometry">The codestream geometry.</param>
    /// <param name="reductionLevels">The number of finest wavelet levels left out.</param>
    internal JpxDecodedImage(JpxGeometry geometry, int reductionLevels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reductionLevels);
        Geometry = geometry;
        var count = geometry.Components.Length;
        Planes = new int[count][];
        Areas = new JpxRectangle[count];
        for (var c = 0; c < count; c++)
        {
            var area = geometry.ToComponent(geometry.Image, c).Reduce(reductionLevels);
            var length = area.Width * area.Height;
            Areas[c] = area;
            Planes[c] = ScratchPool<int>.Shared.Rent(length);
            Planes[c].AsSpan(0, length).Clear();
        }
    }

    /// <summary>Gets the image geometry.</summary>
    internal JpxGeometry Geometry { get; }

    /// <summary>Gets each component's samples, row by row at the component's width.</summary>
    internal int[][] Planes { get; }

    /// <summary>Gets each component's area in its own sample grid.</summary>
    internal JpxRectangle[] Areas { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        for (var c = 0; c < Planes.Length; c++)
        {
            if (Planes[c].Length <= 0)
            {
                continue;
            }

            ScratchPool<int>.Shared.Return(Planes[c]);
            Planes[c] = [];
        }
    }
}
