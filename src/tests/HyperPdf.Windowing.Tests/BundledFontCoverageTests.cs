// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Viewer.Text;

namespace HyperPdf.Windowing.Tests;

/// <summary>Tests which characters the font that ships with the viewer can draw by itself.</summary>
public sealed class BundledFontCoverageTests
{
    /// <summary>The small triangle once written after the names of the tool bar's menus.</summary>
    private const int DownTriangle = 0x25BE;

    /// <summary>
    /// The font that ships with the viewer has no small down triangle, so text that uses one sends the window to look
    /// through the system's font files. The tool bar must mark its menus with an icon instead.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BundledFontHasNoDownTriangle()
    {
        var font = UiFont.Load(false);

        await Assert.That(font).IsNotNull();
        await Assert.That(font!.Has(new(DownTriangle))).IsFalse();
        await Assert.That(font.Has(new('A'))).IsTrue();
    }
}
