// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>
/// Reads the first image of a TIFF file: strips of 1 to 16 bits a sample, uncompressed or compressed with LZW, deflate,
/// PackBits or the fax codings, as grey, palette, RGB (with alpha) or CMYK.
/// </summary>
internal static class TiffFileDecoder
{
    /// <summary>The tag for the palette.</summary>
    private const int TagColorMap = 320;

    /// <summary>The most palette values read: three channels of 256 entries.</summary>
    private const int MaxColorMapValues = 3 * 256;

    /// <summary>Reads a TIFF file.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the decode between strips.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The file is damaged, or uses a feature this reader does not handle.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfRasterImage Decode(ReadOnlySpan<byte> file, CancellationToken cancellationToken)
    {
        var directory = new TiffDirectory(file.ToArray());
        var info = TiffInfo.Read(directory);
        if (info.IsFax)
        {
            return new(PdfRasterFormat.Bilevel1, info.Width, info.Height, TiffStrips.ReadFax(directory, info, cancellationToken));
        }

        var samples = TiffStrips.ReadSamples(directory, info, cancellationToken);
        return TiffPixels.Convert(info, samples, directory.GetValues(TagColorMap, MaxColorMapValues));
    }
}
