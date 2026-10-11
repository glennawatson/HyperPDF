// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Selects a native scan resolution that still covers the display image at the zoom band's upper scale.</summary>
internal static class ImageReduction
{
    /// <summary>The greatest number of halvings retained for a display scan.</summary>
    private const int MaximumLevels = 4;

    /// <summary>One halving of both image dimensions.</summary>
    private const int Half = 2;

    /// <summary>Chooses how many source resolution levels can be omitted.</summary>
    /// <param name="dictionary">The image XObject dictionary.</param>
    /// <param name="matrix">The transform from the image unit square to page points.</param>
    /// <param name="scale">The upper device scale of the page recording.</param>
    /// <returns>The safe number of halvings, or zero.</returns>
    internal static int Select(PdfDictionary dictionary, Matrix3x2 matrix, float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0 || !SupportsReduction(dictionary))
        {
            return 0;
        }

        var width = dictionary.GetInt32(KnownName.Width);
        var height = dictionary.GetInt32(KnownName.Height);
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        if (!TryGetDrawnSize(matrix, scale, out var drawnWidth, out var drawnHeight))
        {
            return 0;
        }

        var levels = 0;
        while (levels < MaximumLevels && width >= drawnWidth * Half && height >= drawnHeight * Half)
        {
            levels++;
            drawnWidth *= Half;
            drawnHeight *= Half;
        }

        return levels;
    }

    /// <summary>Measures the image unit square after its page transform and device scale.</summary>
    /// <param name="matrix">The image-to-page matrix.</param>
    /// <param name="scale">Device pixels per point.</param>
    /// <param name="width">Receives displayed width.</param>
    /// <param name="height">Receives displayed height.</param>
    /// <returns>Whether the dimensions are usable.</returns>
    private static bool TryGetDrawnSize(Matrix3x2 matrix, float scale, out float width, out float height)
    {
        width = MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)) * scale;
        height = MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22)) * scale;
        return float.IsFinite(width) && float.IsFinite(height) && width > 0 && height > 0;
    }

    /// <summary>Checks that this is an unmasked scan supported by native reduction.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns>Whether the image can be reduced.</returns>
    private static bool SupportsReduction(PdfDictionary dictionary)
    {
        var maskValue = dictionary.Get(KnownName.Mask);
        if (!maskValue.IsNull && (maskValue.AsStream() is not { } mask || !HasReducibleCodec(mask.Dictionary)))
        {
            return false;
        }

        if (dictionary.GetStream(KnownName.SMask) is { } softMask && !HasReducibleCodec(softMask.Dictionary))
        {
            return false;
        }

        return HasReducibleCodec(dictionary);
    }

    /// <summary>Checks whether a scan or its mask uses a codec with display-size decoding.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns>Whether the final filter supports reduction.</returns>
    private static bool HasReducibleCodec(PdfDictionary dictionary)
    {
        var filter = dictionary.Get(KnownName.Filter);
        var finalFilter = filter.AsArray() is { Count: > 0 } filters ? filters.GetName(filters.Count - 1) : filter.AsName();
        return finalFilter.ToKnownName() is KnownName.JPXDecode or KnownName.JBIG2Decode;
    }
}
