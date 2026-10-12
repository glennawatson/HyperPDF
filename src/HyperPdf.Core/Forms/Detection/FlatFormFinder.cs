// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Forms.Detection;

/// <summary>
/// Finds the places to write on a page of a form without fillable fields. The page is drawn without its annotations
/// or fields at two pixels per point, so thin rules stay solid, then read by a <see cref="FormRegionDetector"/>. The
/// pixel buffers are rented and returned, and one detector is reused under a lock, so it is safe from any thread.
/// </summary>
[DebuggerDisplay("FlatFormFinder")]
public sealed class FlatFormFinder
{
    /// <summary>Pixels per point the page is read at.</summary>
    private const float ReadScale = 2;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The largest side read, in pixels, so a poster-sized page does not take too much memory.</summary>
    private const int MaxSide = 4096;

    /// <summary>The detector, reused.</summary>
    private readonly FormRegionDetector _detector = new();

    /// <summary>Guards the detector.</summary>
    private readonly Lock _gate = new();

    /// <summary>Finds the places to write on a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">Receives the places, in page space.</param>
    public void Find(IDocument document, int pageIndex, List<FormRegion> output)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        var sizes = document.GetPageSizes();
        if ((uint)pageIndex >= (uint)sizes.Length)
        {
            return;
        }

        var size = sizes[pageIndex];
        var scale = Math.Min(ReadScale, MaxSide / Math.Max(size.Width, size.Height));
        var width = Math.Max(1, (int)MathF.Ceiling(size.Width * scale));
        var height = Math.Max(1, (int)MathF.Ceiling(size.Height * scale));
        var pixels = ArrayPool<byte>.Shared.Rent(width * height * PixelBytes);
        var luma = ArrayPool<byte>.Shared.Rent(width * height);
        try
        {
            var bgra = pixels.AsSpan(0, width * height * PixelBytes);
            bgra.Fill(byte.MaxValue);
            if (!document.Render(new(pageIndex, scale, PageRotation.None, 0, 0, RenderFlags.None), new(bgra, width, height, width * PixelBytes)))
            {
                return;
            }

            DarkPixels.ToLuminance(bgra, luma.AsSpan(0, width * height));
            lock (_gate)
            {
                _detector.Detect(luma.AsSpan(0, width * height), (width, height, width), scale, output);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(luma);
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }
}
