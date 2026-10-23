// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Forms;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests byte parsing of the default appearance's font and colour.</summary>
public sealed class DefaultAppearanceTests
{
    /// <summary>Font names decode valid hex escapes and retain damaged escapes.</summary>
    /// <param name="input">The default appearance bytes as text.</param>
    /// <param name="expected">The decoded resource name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/F#31 12 Tf", "F1")]
    [Arguments("/F#GG 12 Tf", "F#GG")]
    [Arguments("/F# 12 Tf", "F#")]
    [Arguments("/F1\0 12\0Tf", "F1")]
    [Arguments("0 g/F1 12 Tf", "F1")]
    [Arguments("/F1 % font resource\n12 Tf", "F1")]
    [Arguments("/g 12 Tf", "g")]
    public async Task FontNameUsesPdfBytes(string input, string expected)
    {
        var name = Encoding.UTF8.GetString(DefaultAppearance.Parse(Encoding.UTF8.GetBytes(input)).FontName);

        await Assert.That(name).IsEqualTo(expected);
    }

    /// <summary>Unused colour components stay zero when the last colour uses fewer operands.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayDoesNotRetainOtherColorComponents()
    {
        var color = DefaultAppearance.Parse("1 1 1 1 k 0 g"u8).Color;

        await Assert.That(color).IsEqualTo(new(DaColorKind.Gray, 0, 0, 0, 0));
    }

    /// <summary>Operators inside comments cannot change the field colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CommentDoesNotChangeTextColor()
    {
        var color = DefaultAppearance.Parse("1 0 0 rg%0 g"u8).Color;

        await Assert.That(color).IsEqualTo(new(DaColorKind.Rgb, 1, 0, 0, 0));
    }

    /// <summary>Non-finite operands cannot be emitted into a regenerated appearance.</summary>
    /// <param name="input">The damaged default appearance.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("NaN g")]
    [Arguments("Infinity g")]
    [Arguments("1e999 g")]
    public async Task NonFiniteNumbersFallBackToZero(string input)
    {
        var color = DefaultAppearance.Parse(Encoding.UTF8.GetBytes(input)).Color;

        await Assert.That(color.First).IsEqualTo(0F);
    }
}
