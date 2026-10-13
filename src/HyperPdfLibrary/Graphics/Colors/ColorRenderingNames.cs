// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>Reads rendering intent names (ISO 32000-2 8.6.5.8) and /UseBlackPtComp names (8.6.5.9) from their UTF-8 spelling.</summary>
internal static class ColorRenderingNames
{
    /// <summary>Reads a rendering intent name.</summary>
    /// <param name="spelling">The name's bytes, without the slash.</param>
    /// <returns>The intent; relative colorimetric for a name PDF does not define, as ISO 32000-2 8.6.5.8 requires.</returns>
    internal static IccIntent ReadIntent(ReadOnlySpan<byte> spelling)
    {
        if (spelling.SequenceEqual("Perceptual"u8))
        {
            return IccIntent.Perceptual;
        }

        if (spelling.SequenceEqual("Saturation"u8))
        {
            return IccIntent.Saturation;
        }

        return spelling.SequenceEqual("AbsoluteColorimetric"u8) ? IccIntent.AbsoluteColorimetric : IccIntent.RelativeColorimetric;
    }

    /// <summary>Reads a /UseBlackPtComp name, which must be spelled exactly ON, OFF or Default.</summary>
    /// <param name="spelling">The name's bytes, without the slash.</param>
    /// <param name="mode">Receives the value.</param>
    /// <returns><see langword="true"/> for one of the three names; <see langword="false"/> for any other name.</returns>
    internal static bool TryReadBlackPointMode(ReadOnlySpan<byte> spelling, out BlackPointCompensationMode mode)
    {
        if (spelling.SequenceEqual("ON"u8))
        {
            mode = BlackPointCompensationMode.On;
            return true;
        }

        if (spelling.SequenceEqual("OFF"u8))
        {
            mode = BlackPointCompensationMode.Off;
            return true;
        }

        mode = BlackPointCompensationMode.Default;
        return spelling.SequenceEqual("Default"u8);
    }
}
