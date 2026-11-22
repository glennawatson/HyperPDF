// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Works out how many bytes an image stream decodes to from its dictionary, so the buffer its samples inflate into is rented
/// once at that size. Without it the buffer doubles its way up, and each size it passes through stays in the shared pool.
/// </summary>
internal static class DecodedLengthEstimate
{
    /// <summary>The bits in a byte, and the sample depth an image has when it names none.</summary>
    private const int ByteBits = 8;

    /// <summary>The deepest samples an image can have.</summary>
    private const int MostBits = 16;

    /// <summary>The components of an RGB or Lab colour.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK colour, the most an estimate is made for.</summary>
    private const int CmykComponents = 4;

    /// <summary>The most deflate can expand its input: a little over a thousand times.</summary>
    private const int MostExpansion = 1032;

    /// <summary>Estimates the decoded length of an image stream's samples.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="encodedLength">The length of the encoded data, which bounds what a dictionary can claim.</param>
    /// <returns>The length in bytes, or 0 when the stream is not an image whose sample layout is plain to read.</returns>
    internal static int ForImage(PdfDictionary dictionary, int encodedLength)
    {
        var width = dictionary.GetInt32(KnownName.Width);
        var height = dictionary.GetInt32(KnownName.Height);
        var isMask = dictionary.GetBoolean(KnownName.ImageMask);
        var components = isMask ? 1 : Components(dictionary.Get(KnownName.ColorSpace));
        if (width <= 0 || height <= 0 || components == 0)
        {
            return 0;
        }

        var bits = isMask ? 1 : Math.Clamp(dictionary.GetInt32(KnownName.BitsPerComponent, ByteBits), 1, MostBits);

        // Rows are padded to whole bytes, and a PNG predictor puts one tag byte before each row.
        var rowBytes = ((((long)width * components * bits) + ByteBits - 1) / ByteBits) + 1;
        var claimed = rowBytes >= PdfLimits.MaxDecodedLength ? PdfLimits.MaxDecodedLength : rowBytes * height;
        return (int)Math.Min(claimed, Math.Min((long)encodedLength * MostExpansion, PdfLimits.MaxDecodedLength));
    }

    /// <summary>Gets the components of a colour space whose count is plain to read.</summary>
    /// <param name="space">The /ColorSpace value.</param>
    /// <returns>The components, or 0 when the space would need parsing in full.</returns>
    private static int Components(PdfValue space) =>
        space.AsArray() is { Count: > 1 } array ? FamilyComponents(array) : DeviceComponents(space.AsName());

    /// <summary>Gets the components of a colour space written as an array.</summary>
    /// <param name="array">The array, which starts with the family name.</param>
    /// <returns>The components, or 0 when the family would need parsing in full.</returns>
    private static int FamilyComponents(PdfArray array) => array.GetName(0).ToKnownName() switch
    {
        KnownName.ICCBased => Math.Clamp(array.Get(1).AsStream()?.Dictionary.GetInt32(KnownName.N) ?? 0, 0, CmykComponents),
        KnownName.Indexed or KnownName.CalGray => 1,
        KnownName.CalRGB or KnownName.Lab => RgbComponents,
        _ => 0,
    };

    /// <summary>Gets the components of a device colour space.</summary>
    /// <param name="name">The colour space name.</param>
    /// <returns>The components, or 0 for any other name.</returns>
    private static int DeviceComponents(PdfName name) => name.ToKnownName() switch
    {
        KnownName.DeviceGray => 1,
        KnownName.DeviceRGB => RgbComponents,
        KnownName.DeviceCMYK => CmykComponents,
        _ => 0,
    };
}
