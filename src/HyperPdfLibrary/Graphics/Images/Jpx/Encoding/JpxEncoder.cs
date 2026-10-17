// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// Encodes an image as JPEG 2000 (ISO 15444-1): the codestream of main header, tile-parts and end marker, wrapped in the
/// JP2 file format on request. The block coder is the embedded block coding of annex D; the packets follow annex B. The
/// encoder starts with two guard bits and begins again with one more when a code-block has more bit-planes than its
/// sub-band's exponent allows, which a lossless file of extreme content can need.
/// </summary>
internal static class JpxEncoder
{
    /// <summary>Encodes an image.</summary>
    /// <param name="image">The image, whose own samples are not read.</param>
    /// <param name="samples">The interleaved samples.</param>
    /// <param name="options">The options.</param>
    /// <param name="output">Receives the file; bytes already in it stay.</param>
    /// <param name="pools">The caller's reusable storage.</param>
    /// <param name="cancellationToken">Cancels between code-blocks and tiles.</param>
    /// <exception cref="ArgumentOutOfRangeException">An image or option value is out of range.</exception>
    /// <exception cref="ArgumentException">The samples do not fill the image, or the colour description does not fit it.</exception>
    /// <exception cref="InvalidOperationException">The samples need more magnitude bit-planes than a codestream can declare, or a tile is too finely divided.</exception>
    internal static void Encode(PdfJpxImage image, ReadOnlySpan<byte> samples, PdfJpxOptions options, ref PooledBuffer output, ScratchPools pools, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        image.ValidateShape();
        if (samples.Length < (long)image.Width * image.Height * image.Components * image.BytesPerSample)
        {
            throw new ArgumentException("The samples do not fill the image.", nameof(samples));
        }

        var settings = JpxEncodeSettings.Create(image, options);
        if (options.WriteFileFormat)
        {
            JpxFileBoxes.CheckColour(image);
        }

        var start = output.Length;
        for (var guard = JpxEncodeSettings.DefaultGuardBits; guard <= JpxEncodeSettings.MaxGuardBits; guard++)
        {
            // Another guard bit gives every sub-band one more magnitude bit-plane.
            output.Length = start;
            if (EncodeWithGuardBits(image, samples, options.WriteFileFormat, settings with { GuardBits = guard }, ref output, pools, cancellationToken))
            {
                return;
            }
        }

        throw new InvalidOperationException("The samples need more magnitude bit-planes than the codestream can declare.");
    }

    /// <summary>Writes the file with a number of guard bits.</summary>
    /// <param name="image">The image.</param>
    /// <param name="samples">The interleaved samples.</param>
    /// <param name="wrap">Whether to write the JP2 boxes.</param>
    /// <param name="settings">The coding parameters.</param>
    /// <param name="output">The file.</param>
    /// <param name="pools">The caller's reusable storage.</param>
    /// <param name="cancellationToken">Cancels between code-blocks and tiles.</param>
    /// <returns><see langword="false"/> when a code-block has more bit-planes than its sub-band allows.</returns>
    /// <exception cref="InvalidOperationException">A segment the encoder wrote is invalid, or a tile is too finely divided.</exception>
    private static bool EncodeWithGuardBits(
        PdfJpxImage image,
        ReadOnlySpan<byte> samples,
        bool wrap,
        JpxEncodeSettings settings,
        ref PooledBuffer output,
        ScratchPools pools,
        CancellationToken cancellationToken)
    {
        var box = 0;
        if (wrap)
        {
            JpxFileBoxes.WriteHeader(ref output, image);
            box = JpxFileBoxes.BeginCodestream(ref output);
        }

        var quantization = JpxQuantPlan.Create(settings);
        var siz = JpxEncodeHeaders.Siz(settings);
        var cod = JpxEncodeHeaders.Cod(settings);
        var qcd = quantization.ToSegment();
        JpxEncodeHeaders.WriteMarker(ref output, JpxMarkers.StartOfCodestream);
        JpxEncodeHeaders.WriteSegment(ref output, JpxMarkers.ImageSize, siz);
        JpxEncodeHeaders.WriteSegment(ref output, JpxMarkers.CodingStyle, cod);
        JpxEncodeHeaders.WriteSegment(ref output, JpxMarkers.Quantization, qcd);
        var geometry = JpxGeometry.Read(siz) ?? throw new InvalidOperationException("The SIZ segment the encoder wrote is invalid.");
        using var tiles = JpxTileEncoding.Create(settings, geometry, ResolveParameters(settings, cod, qcd), quantization, pools);
        var count = settings.TilesAcross * settings.TilesDown;
        for (var tile = 0; tile < count; tile++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!JpxTileEncoding.Encode(tiles, samples, tile, ref output, cancellationToken))
            {
                return false;
            }
        }

        JpxEncodeHeaders.WriteMarker(ref output, JpxMarkers.EndOfCodestream);
        if (wrap)
        {
            JpxFileBoxes.EndCodestream(ref output, box);
        }

        return true;
    }

    /// <summary>Resolves the tile parameters the way a decoder reads the markers, so layout and codestream agree.</summary>
    /// <param name="settings">The coding parameters.</param>
    /// <param name="cod">The COD segment.</param>
    /// <param name="qcd">The QCD segment.</param>
    /// <returns>The parameters.</returns>
    /// <exception cref="InvalidOperationException">A segment the encoder wrote is invalid.</exception>
    private static JpxTileParameters ResolveParameters(JpxEncodeSettings settings, byte[] cod, byte[] qcd)
    {
        var main = new JpxHeaderState(settings.Components);
        if (!main.Read(JpxMarkers.CodingStyle, cod, 0) || !main.Read(JpxMarkers.Quantization, qcd, 0))
        {
            throw new InvalidOperationException("The COD or QCD segment the encoder wrote is invalid.");
        }

        return JpxTileParameters.Resolve(main, new(settings.Components));
    }
}
