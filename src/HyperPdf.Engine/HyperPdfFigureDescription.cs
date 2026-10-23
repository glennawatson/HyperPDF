// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Optimizing;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Engine;

/// <summary>Gives the figures found while tags are inferred a description, by drawing each one and asking a describer.</summary>
internal static class HyperPdfFigureDescription
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The length of the longer side of the picture shown to the describer, in pixels; twice what a typical model reads.</summary>
    private const float TargetSide = 448F;

    /// <summary>The smallest scale used, in pixels to the point.</summary>
    private const float MinimumScale = 0.25F;

    /// <summary>The largest scale used, in pixels to the point.</summary>
    private const float MaximumScale = 8F;

    /// <summary>The most pixels drawn for one figure, which keeps the buffer small.</summary>
    private const int MaximumPixels = 4_000_000;

    /// <summary>Makes the provider the library calls for each figure.</summary>
    /// <param name="self">The open document, which draws the figures.</param>
    /// <param name="describer">The describer, or <see langword="null"/> for no descriptions.</param>
    /// <param name="cancellationToken">Cancels the descriptions.</param>
    /// <returns>The provider, or <see langword="null"/> when there is no describer.</returns>
    internal static PdfFigureAltTextProvider? CreateProvider(HyperPdfDocument self, IFigureDescriber? describer, CancellationToken cancellationToken) =>
        describer is null ? null : (pageIndex, bounds, _) => Describe(self, describer, pageIndex, bounds, cancellationToken);

    /// <summary>Draws a figure and describes it.</summary>
    /// <param name="self">The open document.</param>
    /// <param name="describer">The describer.</param>
    /// <param name="pageIndex">The zero based page.</param>
    /// <param name="bounds">The figure's box in viewer space.</param>
    /// <param name="cancellationToken">Cancels the description.</param>
    /// <returns>The description, or <see langword="null"/> when the figure could not be drawn or described.</returns>
    private static string? Describe(HyperPdfDocument self, IFigureDescriber describer, int pageIndex, PdfViewerRect bounds, CancellationToken cancellationToken)
    {
        if (bounds.IsEmpty)
        {
            return null;
        }

        var scale = Math.Clamp(TargetSide / MathF.Max(bounds.Width, bounds.Height), MinimumScale, MaximumScale);
        var width = Math.Max(1, (int)MathF.Ceiling(bounds.Width * scale));
        var height = Math.Max(1, (int)MathF.Ceiling(bounds.Height * scale));
        if ((long)width * height > MaximumPixels)
        {
            return null;
        }

        var pixels = ArrayPool<byte>.Shared.Rent(width * height * BytesPerPixel);
        try
        {
            var info = new PageRenderInfo(pageIndex, scale, PageRotation.None, (int)MathF.Floor(bounds.Left * scale), (int)MathF.Floor(bounds.Top * scale), RenderFlags.None);
            return HyperPdfRendering.Render(self, in info, new(pixels, width, height, width * BytesPerPixel))
                ? describer.Describe(pixels.AsSpan(0, width * height * BytesPerPixel), width, height, cancellationToken)
                : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }
}
