// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks of <see cref="CcittEncoder"/> against MuPDF's <c>mutool</c>, an independent reader.</summary>
public sealed partial class CcittEncoderTests
{
    /// <summary>The reason a test is skipped.</summary>
    private const string MutoolMissing = "mutool is not installed.";

    /// <summary>The grey level of a 1 bit in a DeviceGray image.</summary>
    private const byte WhiteLevel = 255;

    /// <summary>The widths rendered: not a multiple of eight, and wider than a few bytes.</summary>
    private static readonly int[] MutoolWidths = [13, 203];

    /// <summary>The patterns rendered: text-like strokes and noise.</summary>
    private static readonly Pattern[] MutoolPatterns = [Pattern.Strokes, Pattern.Noise];

    /// <summary>Checks that mutool renders the pixels that were encoded, for every option combination of a scheme.</summary>
    /// <param name="k">The /K value.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(MixedK)]
    public async Task Encode_AllOptionCombinations_RenderTheSameInMutool(int k, CancellationToken cancellationToken)
    {
        foreach (var width in MutoolWidths)
        {
            foreach (var pattern in MutoolPatterns)
            {
                var rows = Make(width, MatrixHeight, pattern);
                for (var mask = 0; mask < OptionMasks; mask++)
                {
                    var options = FromMask(k, width, mask);
                    var encoded = EncodeWith(rows, width, MatrixHeight, options);
                    var rendered = await MutoolBilevelRenderer.RenderAsync(width, MatrixHeight, DecodeParameters(options, MatrixHeight), encoded, cancellationToken);
                    if (rendered is null)
                    {
                        Skip.Test(MutoolMissing);
                        return;
                    }

                    await Assert.That(rendered).IsEquivalentTo(ExpectedLevels(rows, width, MatrixHeight)).Because($"{width} wide, {pattern}, {options}");
                }
            }
        }
    }

    /// <summary>Writes the CCITTFaxDecode parameters of an option set.</summary>
    /// <param name="options">The options.</param>
    /// <param name="height">The image height.</param>
    /// <returns>The dictionary entries.</returns>
    private static string DecodeParameters(CcittParameters options, int height) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"/K {options.K} /Columns {options.Columns} /Rows {height} /BlackIs1 {Flag(options.BlackIs1)} "
            + $"/EncodedByteAlign {Flag(options.EncodedByteAlign)} /EndOfLine {Flag(options.EndOfLine)} /EndOfBlock {Flag(options.EndOfBlock)}");

    /// <summary>Writes a PDF boolean.</summary>
    /// <param name="value">The value.</param>
    /// <returns><c>true</c> or <c>false</c>.</returns>
    private static string Flag(bool value) => value ? "true" : "false";

    /// <summary>Lists the grey levels a DeviceGray image of these rows shows: white where the bit is 1.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>One level per pixel.</returns>
    private static byte[] ExpectedLevels(byte[] rows, int width, int height)
    {
        var stride = (width + ByteBits - 1) / ByteBits;
        var levels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var set = (rows[(y * stride) + (x / ByteBits)] >> (ByteBits - 1 - (x % ByteBits))) & 1;
                levels[(y * width) + x] = set == 1 ? WhiteLevel : (byte)0;
            }
        }

        return levels;
    }
}
