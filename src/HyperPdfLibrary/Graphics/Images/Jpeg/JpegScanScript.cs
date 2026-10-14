// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// Lists the scans an encoder writes: one interleaved scan or one per component for a sequential frame, and a
/// spectral-selection and successive-approximation script for a progressive frame.
/// </summary>
internal static class JpegScanScript
{
    /// <summary>The most scans a script holds.</summary>
    internal const int MaxScans = 16;

    /// <summary>The scans of a progressive gray script.</summary>
    private const int GrayScans = 6;

    /// <summary>The scans of a progressive colour script.</summary>
    private const int ColorScans = 10;

    /// <summary>The last zigzag index.</summary>
    private const int LastIndex = JpegBlock.Length - 1;

    /// <summary>The last coefficient of the low-frequency band that the first luma pass codes alone.</summary>
    private const int LowBandEnd = 5;

    /// <summary>The first coefficient of the high-frequency band.</summary>
    private const int HighBandStart = 6;

    /// <summary>The bit position the DC and chroma passes of a colour script begin at.</summary>
    private const int ChromaShift = 1;

    /// <summary>The bit position the luma AC passes of a colour script begin at.</summary>
    private const int LumaShift = 2;

    /// <summary>The components of a colour image that has luma and two chroma components.</summary>
    private const int ColorComponents = 3;

    /// <summary>The mask of the luma component.</summary>
    private const int Luma = 1;

    /// <summary>The mask of the blue chroma component.</summary>
    private const int BlueChroma = 2;

    /// <summary>The mask of the red chroma component.</summary>
    private const int RedChroma = 4;

    /// <summary>Lists the scans of a frame.</summary>
    /// <param name="components">The number of components.</param>
    /// <param name="progressive">Whether the frame is progressive.</param>
    /// <param name="interleaved">Whether a sequential frame codes all components in one scan.</param>
    /// <param name="scans">Receives the scans.</param>
    /// <returns>The number of scans.</returns>
    internal static int Build(int components, bool progressive, bool interleaved, Span<JpegScanSpec> scans)
    {
        var all = (1 << components) - 1;
        if (!progressive)
        {
            return BuildSequential(components, all, interleaved, scans);
        }

        if (components == 1)
        {
            return BuildGray(scans);
        }

        return components == ColorComponents ? BuildColor(all, scans) : BuildSpectral(components, all, scans);
    }

    /// <summary>Lists the scans of a sequential frame.</summary>
    /// <param name="components">The number of components.</param>
    /// <param name="all">The mask of all components.</param>
    /// <param name="interleaved">Whether one scan codes every component.</param>
    /// <param name="scans">Receives the scans.</param>
    /// <returns>The number of scans.</returns>
    private static int BuildSequential(int components, int all, bool interleaved, Span<JpegScanSpec> scans)
    {
        if (interleaved)
        {
            scans[0] = new(all, 0, LastIndex, 0, 0, false);
            return 1;
        }

        for (var c = 0; c < components; c++)
        {
            scans[c] = new(1 << c, 0, LastIndex, 0, 0, false);
        }

        return components;
    }

    /// <summary>Lists the scans of a progressive gray frame.</summary>
    /// <param name="scans">Receives the scans.</param>
    /// <returns>The number of scans.</returns>
    private static int BuildGray(Span<JpegScanSpec> scans)
    {
        scans[0] = new(Luma, 0, 0, 0, ChromaShift, true);
        scans[1] = new(Luma, 1, LowBandEnd, 0, LumaShift, true);
        scans[2] = new(Luma, HighBandStart, LastIndex, 0, LumaShift, true);
        scans[3] = new(Luma, 1, LastIndex, LumaShift, ChromaShift, true);
        scans[4] = new(Luma, 0, 0, ChromaShift, 0, true);
        scans[5] = new(Luma, 1, LastIndex, ChromaShift, 0, true);
        return GrayScans;
    }

    /// <summary>Lists the scans of a progressive colour frame: the DC coefficients first, then the AC bands with the chroma coded before the finer luma passes.</summary>
    /// <param name="all">The mask of all components.</param>
    /// <param name="scans">Receives the scans.</param>
    /// <returns>The number of scans.</returns>
    private static int BuildColor(int all, Span<JpegScanSpec> scans)
    {
        scans[0] = new(all, 0, 0, 0, ChromaShift, true);
        scans[1] = new(Luma, 1, LowBandEnd, 0, LumaShift, true);
        scans[2] = new(RedChroma, 1, LastIndex, 0, ChromaShift, true);
        scans[3] = new(BlueChroma, 1, LastIndex, 0, ChromaShift, true);
        scans[4] = new(Luma, HighBandStart, LastIndex, 0, LumaShift, true);
        scans[5] = new(Luma, 1, LastIndex, LumaShift, ChromaShift, true);
        scans[6] = new(all, 0, 0, ChromaShift, 0, true);
        scans[7] = new(RedChroma, 1, LastIndex, ChromaShift, 0, true);
        scans[8] = new(BlueChroma, 1, LastIndex, ChromaShift, 0, true);
        scans[9] = new(Luma, 1, LastIndex, ChromaShift, 0, true);
        return ColorScans;
    }

    /// <summary>Lists the scans of a progressive frame with any other component count: spectral selection only.</summary>
    /// <param name="components">The number of components.</param>
    /// <param name="all">The mask of all components.</param>
    /// <param name="scans">Receives the scans.</param>
    /// <returns>The number of scans.</returns>
    private static int BuildSpectral(int components, int all, Span<JpegScanSpec> scans)
    {
        scans[0] = new(all, 0, 0, 0, 0, true);
        for (var c = 0; c < components; c++)
        {
            scans[c + 1] = new(1 << c, 1, LastIndex, 0, 0, true);
        }

        return components + 1;
    }
}
