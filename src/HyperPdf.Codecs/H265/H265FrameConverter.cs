// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Turns a decoded picture into the 8 bit 4:2:0 frame the video player shows: cropped, reduced to 8 bits and, for other chroma formats, resampled.</summary>
internal static class H265FrameConverter
{
    /// <summary>The value of a chroma sample of a monochrome picture.</summary>
    private const byte NeutralChroma = 128;

    /// <summary>The subsampling of the chroma planes of the output frame along each axis.</summary>
    private const int OutputSubsampling = 2;

    /// <summary>The bit depth of the output frame.</summary>
    private const int OutputBits = 8;

    /// <summary>Gets the size of the shown picture.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The width and height in luma samples.</returns>
    internal static BlockRect ShownSize(H265Sps sps) =>
        new(sps.Crop.Left, sps.Crop.Top, sps.Width - sps.Crop.Left - sps.Crop.Right, sps.Height - sps.Crop.Top - sps.Crop.Bottom);

    /// <summary>Converts a picture.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="sps">The sequence parameter set the picture was decoded with.</param>
    /// <param name="frame">The frame to fill, of the shown size; every sample is written.</param>
    /// <returns>The frame.</returns>
    internal static VideoFrame Convert(H265Picture picture, H265Sps sps, VideoFrame frame)
    {
        var shown = ShownSize(sps);
        CopyPlane(picture.Luma, picture.Width, shown, 1, 1, frame.Y, sps.BitDepthLuma);
        if (sps.ChromaFormat == ChromaSampling.Monochrome)
        {
            Array.Fill(frame.U, NeutralChroma);
            Array.Fill(frame.V, NeutralChroma);
            return frame;
        }

        var subW = sps.SubWidthC;
        var subH = sps.SubHeightC;
        var area = new BlockRect(shown.X / subW, shown.Y / subH, frame.ChromaWidth, frame.ChromaHeight);
        CopyPlane(picture.Cb, picture.ChromaWidth, area, OutputSubsampling / subW, OutputSubsampling / subH, frame.U, sps.BitDepthChroma);
        CopyPlane(picture.Cr, picture.ChromaWidth, area, OutputSubsampling / subW, OutputSubsampling / subH, frame.V, sps.BitDepthChroma);
        return frame;
    }

    /// <summary>Copies an area of a plane, reducing the samples to 8 bits.</summary>
    /// <param name="plane">The source plane.</param>
    /// <param name="stride">The samples in a row of the source.</param>
    /// <param name="area">The area to copy, in source samples (the width and height are those of the output).</param>
    /// <param name="stepX">The source columns between output columns.</param>
    /// <param name="stepY">The source rows between output rows.</param>
    /// <param name="target">The output plane, row after row with no padding.</param>
    /// <param name="bitDepth">The bit depth of the source.</param>
    private static void CopyPlane(ushort[] plane, int stride, BlockRect area, int stepX, int stepY, byte[] target, int bitDepth)
    {
        var shift = bitDepth - OutputBits;
        var round = shift > 0 ? 1 << (shift - 1) : 0;
        for (var row = 0; row < area.Height; row++)
        {
            var source = ((area.Y + (row * stepY)) * stride) + area.X;
            var destination = row * area.Width;
            for (var column = 0; column < area.Width; column++)
            {
                var value = (plane[source + (column * stepX)] + round) >> shift;
                target[destination + column] = (byte)Math.Min(value, byte.MaxValue);
            }
        }
    }
}
