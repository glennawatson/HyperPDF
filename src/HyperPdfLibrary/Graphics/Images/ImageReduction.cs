// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Select(PdfDictionary dictionary, Matrix3x2 matrix, float scale) => Select(dictionary, matrix, scale, false);

    /// <summary>Chooses how many halvings can be omitted, counting an area average of the decoded pixels for codecs that cannot decode a lower resolution.</summary>
    /// <param name="dictionary">The image XObject dictionary.</param>
    /// <param name="matrix">The transform from the image unit square to page points.</param>
    /// <param name="scale">The upper device scale of the page recording.</param>
    /// <param name="reduceDecoded">Whether the decoded pixels of any other codec may be averaged down by the same halvings.</param>
    /// <returns>The safe number of halvings, or zero.</returns>
    internal static int Select(PdfDictionary dictionary, Matrix3x2 matrix, float scale, bool reduceDecoded) =>
        float.IsFinite(scale) && scale > 0 && (reduceDecoded || SupportsReduction(dictionary))
            ? Select(dictionary.GetInt32(KnownName.Width), dictionary.GetInt32(KnownName.Height), matrix, scale)
            : 0;

    /// <summary>Chooses how many halvings keep an image of a known size at least as large as it is drawn.</summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="matrix">The transform from the image unit square to page points.</param>
    /// <param name="scale">The upper device scale of the page recording.</param>
    /// <returns>The number of halvings, or zero.</returns>
    internal static int Select(int width, int height, Matrix3x2 matrix, float scale) =>
        float.IsFinite(scale) && scale > 0 && width > 0 && height > 0 && TryGetDrawnSize(matrix, scale, out var drawnWidth, out var drawnHeight)
            ? CountLevels(width, height, drawnWidth, drawnHeight)
            : 0;

    /// <summary>Chooses how many source resolution levels can be omitted when the caller needs no more than a given size.</summary>
    /// <param name="dictionary">The image XObject dictionary.</param>
    /// <param name="maxWidth">The width in pixels the caller can use.</param>
    /// <param name="maxHeight">The height in pixels the caller can use.</param>
    /// <returns>The safe number of halvings, or zero; the reduced image is still at least as large as the given size in both directions.</returns>
    internal static int Select(PdfDictionary dictionary, int maxWidth, int maxHeight) =>
        maxWidth > 0 && maxHeight > 0 && SupportsReduction(dictionary)
            ? CountLevels(dictionary.GetInt32(KnownName.Width), dictionary.GetInt32(KnownName.Height), maxWidth, maxHeight)
            : 0;

    /// <summary>Gets the halvings the image's own codec can decode, out of a requested number.</summary>
    /// <param name="dictionary">The image XObject dictionary.</param>
    /// <param name="requested">The halvings requested for the image.</param>
    /// <returns>The requested halvings when the codec decodes a lower resolution, else zero; the caller averages the decoded pixels instead.</returns>
    internal static int NativeLevels(PdfDictionary dictionary, int requested) => requested > 0 && SupportsReduction(dictionary) ? requested : 0;

    /// <summary>Gets the halvings still to be applied to the pixels of a decoded image, once the codec has taken off the ones it could.</summary>
    /// <param name="declaredWidth">The image's declared width in pixels.</param>
    /// <param name="decodedWidth">The width of the decoded pixels.</param>
    /// <param name="requested">The halvings requested for the image.</param>
    /// <returns>The remaining halvings, or zero when the decoded width is not a whole number of halvings below the declared one.</returns>
    internal static int RemainingLevels(int declaredWidth, int decodedWidth, int requested)
    {
        for (var applied = 0; applied < requested; applied++)
        {
            if (ImageBoxReduction.ReducedSize(declaredWidth, applied) == decodedWidth)
            {
                return requested - applied;
            }
        }

        return 0;
    }

    /// <summary>Counts the halvings that keep an image at least as large as the size it is drawn at.</summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="drawnWidth">The width needed.</param>
    /// <param name="drawnHeight">The height needed.</param>
    /// <returns>The halvings, at most <see cref="MaximumLevels"/>.</returns>
    private static int CountLevels(int width, int height, float drawnWidth, float drawnHeight)
    {
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
