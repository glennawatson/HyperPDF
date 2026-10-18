// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Checks span-based CSS lookup without changing its case or fallback rules.</summary>
public sealed class RichStyleReaderTests
{
    /// <summary>Named colours preserve their RGB values regardless of case.</summary>
    /// <param name="name">The colour name.</param>
    /// <param name="expected">The RGB value.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments("black", 0x000000)]
    [Arguments("white", 0xFFFFFF)]
    [Arguments("red", 0xFF0000)]
    [Arguments("green", 0x008000)]
    [Arguments("blue", 0x0000FF)]
    [Arguments("yellow", 0xFFFF00)]
    [Arguments("gray", 0x808080)]
    [Arguments("grey", 0x808080)]
    [Arguments("silver", 0xC0C0C0)]
    [Arguments("maroon", 0x800000)]
    [Arguments("navy", 0x000080)]
    [Arguments("purple", 0x800080)]
    [Arguments("teal", 0x008080)]
    [Arguments("olive", 0x808000)]
    [Arguments("lime", 0x00FF00)]
    [Arguments("aqua", 0x00FFFF)]
    [Arguments("fuchsia", 0xFF00FF)]
    [Arguments("orange", 0xFFA500)]
    public async Task NamedColorsIgnoreCase(string name, int expected)
    {
        var valid = RichStyleReader.TryReadColor(name.ToUpperInvariant(), out var color);
        await Assert.That(valid).IsTrue();
        await Assert.That(color).IsEqualTo(expected);
    }

    /// <summary>Mixed-case declarations retain every supported style setting.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DeclarationsKeepStylesAndIgnoreUnknownProperties()
    {
        const int size = 18;
        const int right = 2;
        var style = RichStyleReader.Apply(
            "FONT-FAMILY:Courier;FONT-SIZE:18pt;FONT-WEIGHT:bold;FONT-STYLE:italic;TEXT-DECORATION:underline line-through;TEXT-ALIGN:right;COLOR:ORANGE;XFA-SPACERUN:YES;unknown:ignored",
            RichStyle.None);
        await Assert.That(style).IsEqualTo(new(RichFamily.Mono, size, true, true, true, true, 0xFFA500, right, true));
    }

    /// <summary>Unsupported colour words are rejected without changing other settings.</summary>
    /// <param name="name">The unsupported name.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("redx")]
    [Arguments("unknown")]
    public async Task UnknownColorIsRejected(string name)
    {
        await Assert.That(RichStyleReader.TryReadColor(name, out var color)).IsFalse();
        await Assert.That(color).IsEqualTo(0);
        await Assert.That(RichStyleReader.Apply("color:unknown", RichStyle.None)).IsEqualTo(RichStyle.None);
    }
}
