// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>What the tags of a TIFF image say about how its pixels are stored.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="BitsPerSample">The bits in each sample: 1, 2, 4, 8 or 16.</param>
/// <param name="SamplesPerPixel">The samples in each pixel.</param>
/// <param name="Compression">The compression number.</param>
/// <param name="Photometric">The photometric interpretation: 0 white is zero, 1 black is zero, 2 RGB, 3 palette, 5 CMYK.</param>
/// <param name="Separate">Whether each sample is stored in its own plane, rather than pixel by pixel.</param>
/// <param name="Predictor">The predictor: 1 none, 2 horizontal differencing.</param>
/// <param name="RowsPerStrip">The rows in each strip.</param>
/// <param name="ReversedBits">Whether the bits of each byte run from the least significant end.</param>
/// <param name="FaxOptions">The Group 3 options (T4Options) tag.</param>
/// <param name="Alpha">The meaning of the first extra sample: 0 none, 1 associated (premultiplied) alpha, 2 unassociated alpha.</param>
[DebuggerDisplay("TiffInfo: {Width}x{Height}, {SamplesPerPixel} x {BitsPerSample} bits, compression {Compression}, photometric {Photometric}")]
internal readonly record struct TiffInfo(
    int Width,
    int Height,
    int BitsPerSample,
    int SamplesPerPixel,
    int Compression,
    int Photometric,
    bool Separate,
    int Predictor,
    int RowsPerStrip,
    bool ReversedBits,
    int FaxOptions,
    int Alpha)
{
    /// <summary>The compression for Modified Huffman run-length coding.</summary>
    internal const int FaxHuffman = 2;

    /// <summary>The compression for Group 3 fax coding.</summary>
    internal const int FaxGroup3 = 3;

    /// <summary>The compression for Group 4 fax coding.</summary>
    internal const int FaxGroup4 = 4;

    /// <summary>The tag for the width.</summary>
    private const int TagWidth = 256;

    /// <summary>The tag for the height.</summary>
    private const int TagHeight = 257;

    /// <summary>The tag for the bits in each sample.</summary>
    private const int TagBitsPerSample = 258;

    /// <summary>The tag for the compression.</summary>
    private const int TagCompression = 259;

    /// <summary>The tag for the photometric interpretation.</summary>
    private const int TagPhotometric = 262;

    /// <summary>The tag for the fill order.</summary>
    private const int TagFillOrder = 266;

    /// <summary>The tag for the samples in each pixel.</summary>
    private const int TagSamplesPerPixel = 277;

    /// <summary>The tag for the rows in each strip.</summary>
    private const int TagRowsPerStrip = 278;

    /// <summary>The tag for the planar configuration.</summary>
    private const int TagPlanar = 284;

    /// <summary>The tag for the Group 3 options.</summary>
    private const int TagFaxOptions = 292;

    /// <summary>The tag for the predictor.</summary>
    private const int TagPredictor = 317;

    /// <summary>The tag for the meaning of extra samples.</summary>
    private const int TagExtraSamples = 338;

    /// <summary>The first tag of a tiled image.</summary>
    private const int TagTileWidth = 322;

    /// <summary>The planar configuration for separate planes.</summary>
    private const int PlanarSeparate = 2;

    /// <summary>The fill order that runs bits from the least significant end.</summary>
    private const int FillOrderReversed = 2;

    /// <summary>The most samples a pixel may have.</summary>
    private const int MaxSamples = 8;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The widest sample.</summary>
    private const int WidestSample = 16;

    /// <summary>A sample of two bits.</summary>
    private const int TwoBits = 2;

    /// <summary>A sample of four bits.</summary>
    private const int FourBits = 4;

    /// <summary>The rounding that makes a bit count a whole number of bytes.</summary>
    private const int RoundToByte = 7;

    /// <summary>Gets a value indicating whether the image is one-bit fax coded.</summary>
    internal bool IsFax => Compression is FaxHuffman or FaxGroup3 or FaxGroup4;

    /// <summary>Gets the bytes in a stored row of a plane, or of the whole pixel when the samples are not separate.</summary>
    internal int RowBytes => (int)((((long)Width * (Separate ? 1 : SamplesPerPixel) * BitsPerSample) + RoundToByte) / ByteBits);

    /// <summary>Gets the planes the image is stored in.</summary>
    internal int Planes => Separate ? SamplesPerPixel : 1;

    /// <summary>Reads the tags that describe the first image.</summary>
    /// <param name="directory">The directory.</param>
    /// <returns>What the tags say.</returns>
    /// <exception cref="InvalidDataException">The image is tiled, or has a size, depth or sample count this reader does not handle.</exception>
    internal static TiffInfo Read(TiffDirectory directory)
    {
        if (directory.Has(TagTileWidth))
        {
            throw new InvalidDataException("This reader does not handle tiled TIFF pictures.");
        }

        var width = directory.GetValue(TagWidth, 0);
        var height = directory.GetValue(TagHeight, 0);
        ImageFileLimits.CheckSize(width, height);
        var bits = (int)directory.GetValue(TagBitsPerSample, 1);
        var samples = (int)directory.GetValue(TagSamplesPerPixel, 1);
        if (!IsSupportedLayout(bits, samples))
        {
            throw new InvalidDataException("The TIFF's sample layout is not one this reader handles.");
        }

        return new(
            (int)width,
            (int)height,
            bits,
            samples,
            (int)directory.GetValue(TagCompression, 1),
            (int)directory.GetValue(TagPhotometric, 1),
            directory.GetValue(TagPlanar, 1) == PlanarSeparate,
            (int)directory.GetValue(TagPredictor, 1),
            GetRowsPerStrip(directory, (int)height),
            directory.GetValue(TagFillOrder, 1) == FillOrderReversed,
            (int)directory.GetValue(TagFaxOptions, 0),
            GetAlpha(directory));
    }

    /// <summary>Determines whether the sample size and count are ones this reader handles.</summary>
    /// <param name="bits">The bits in a sample.</param>
    /// <param name="samples">The samples in a pixel.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool IsSupportedLayout(int bits, int samples) =>
        bits is 1 or TwoBits or FourBits or ByteBits or WidestSample && samples is >= 1 and <= MaxSamples;

    /// <summary>Gets the rows in each strip, which is the whole image when the tag is missing.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="height">The image height.</param>
    /// <returns>The rows.</returns>
    private static int GetRowsPerStrip(TiffDirectory directory, int height)
    {
        var rows = directory.GetValue(TagRowsPerStrip, uint.MaxValue);
        return (int)Math.Min(rows == 0 ? uint.MaxValue : rows, (uint)height);
    }

    /// <summary>Gets the meaning of the first extra sample.</summary>
    /// <param name="directory">The directory.</param>
    /// <returns>0 when there is none, 1 for premultiplied alpha, 2 for straight alpha.</returns>
    private static int GetAlpha(TiffDirectory directory)
    {
        var extras = directory.GetValues(TagExtraSamples, 1);
        return extras.Length > 0 ? (int)extras[0] : 0;
    }
}
