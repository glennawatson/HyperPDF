// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>Names the values of chroma_format_idc, which H.264 and H.265 share.</summary>
internal static class ChromaSampling
{
    /// <summary>Luma only.</summary>
    internal const int Monochrome = 0;

    /// <summary>Chroma has half the width and half the height.</summary>
    internal const int Yuv420 = 1;

    /// <summary>Chroma has half the width and the full height.</summary>
    internal const int Yuv422 = 2;

    /// <summary>Chroma has the full width and height.</summary>
    internal const int Yuv444 = 3;

    /// <summary>The subsampling of a halved dimension.</summary>
    private const int Halved = 2;

    /// <summary>Gets the horizontal subsampling of the chroma planes.</summary>
    /// <param name="format">The chroma format.</param>
    /// <returns>2 for 4:2:0 and 4:2:2, otherwise 1.</returns>
    internal static int SubWidth(int format) => format is Yuv420 or Yuv422 ? Halved : 1;

    /// <summary>Gets the vertical subsampling of the chroma planes.</summary>
    /// <param name="format">The chroma format.</param>
    /// <returns>2 for 4:2:0, otherwise 1.</returns>
    internal static int SubHeight(int format) => format == Yuv420 ? Halved : 1;
}
