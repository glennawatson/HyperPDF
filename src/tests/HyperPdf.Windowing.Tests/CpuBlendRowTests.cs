// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Cpu;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that blending a row gives the one pixel formula's bytes for opaque, clear and see-through pixels.</summary>
public sealed class CpuBlendRowTests
{
    /// <summary>The bytes of a pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the alpha byte.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The largest channel value.</summary>
    private const int ChannelMax = 255;

    /// <summary>The kinds of source pixel the rows cycle through: opaque, clear, and see-through.</summary>
    private const int Kinds = 3;

    /// <summary>The pixels in each run of one kind, so runs fall across and within vector widths.</summary>
    private const int RunLength = 5;

    /// <summary>A step that spreads the channel values.</summary>
    private const int Step = 37;

    /// <summary>Blending a row matches the one pixel formula.</summary>
    /// <param name="pixels">The pixels in the row.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(4)]
    [Arguments(7)]
    [Arguments(64)]
    [Arguments(301)]
    public async Task BlendRowMatchesTheOnePixelFormula(int pixels)
    {
        var to = new byte[pixels * BytesPerPixel];
        var from = new byte[to.Length];
        for (var p = 0; p < pixels; p++)
        {
            var offset = p * BytesPerPixel;
            for (var c = 0; c < BytesPerPixel; c++)
            {
                to[offset + c] = (byte)((p * Step) + (c * BytesPerPixel));
            }

            Fill(from.AsSpan(offset, BytesPerPixel), (p / RunLength) % Kinds, p);
        }

        var expected = (byte[])to.Clone();
        for (var offset = 0; offset < from.Length; offset += BytesPerPixel)
        {
            var inverse = ChannelMax - from[offset + AlphaOffset];
            for (var c = 0; c < BytesPerPixel; c++)
            {
                expected[offset + c] = (byte)Math.Min(ChannelMax, from[offset + c] + PremultipliedColor.DivideBy255(expected[offset + c] * inverse));
            }
        }

        CpuBlend.BlendRow(to, from);

        await Assert.That(to.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Fills a premultiplied source pixel of a kind.</summary>
    /// <param name="pixel">The pixel's bytes.</param>
    /// <param name="kind">Opaque, clear, or see-through.</param>
    /// <param name="index">The pixel's place, which spreads its values.</param>
    private static void Fill(Span<byte> pixel, int kind, int index)
    {
        var alpha = kind switch
        {
            0 => ChannelMax,
            1 => 0,
            _ => (index * Step) % ChannelMax,
        };

        for (var c = 0; c < AlphaOffset; c++)
        {
            // Premultiplied colour is never more than its alpha.
            pixel[c] = (byte)(alpha == 0 ? 0 : (index + (c * Step)) % (alpha + 1));
        }

        pixel[AlphaOffset] = (byte)alpha;
    }
}
