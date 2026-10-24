// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Video;

/// <summary>The fixed numbers of the conversion from Y, Cb and Cr to blue, green, red and alpha, shared by the scalar and vector code.</summary>
internal static class YuvConstants
{
    /// <summary>The value of a colour sample that has no colour.</summary>
    internal const int ColorCentre = 128;

    /// <summary>The rounding added before the fractional bits are dropped.</summary>
    internal const int Rounding = 128;

    /// <summary>The fractional bits of the weights.</summary>
    internal const int FractionBits = 8;

    /// <summary>The largest value of a colour channel.</summary>
    internal const int ChannelMax = 255;

    /// <summary>The bits to move green up in a pixel.</summary>
    internal const int GreenShift = 8;

    /// <summary>The bits to move red up in a pixel.</summary>
    internal const int RedShift = 16;

    /// <summary>The alpha byte of an opaque pixel, in the top byte of a pixel.</summary>
    internal const int OpaqueAlpha = unchecked((int)0xFF000000);
}
