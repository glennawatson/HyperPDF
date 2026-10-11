// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Tests.Rendering;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Renders text in embedded fonts of every kind through the default font factory.</summary>
[NotInParallel]
public sealed class FontRenderTests
{
    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>A device column inside A.</summary>
    private const int InsideX = 22;

    /// <summary>A device row inside A: 34 points up the 100 point page.</summary>
    private const int InsideY = 66;

    /// <summary>A device row above A.</summary>
    private const int AboveY = 30;

    /// <summary>A device column right of A.</summary>
    private const int RightX = 60;

    /// <summary>The descriptor flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The descriptor flags of a non-symbolic font.</summary>
    private const int Nonsymbolic = 32;

    /// <summary>A channel value below this means the pixel contains glyph ink.</summary>
    private const byte InkThreshold = 200;

    /// <summary>The vertical text used to compare default and overridden advances.</summary>
    private const string VerticalContent = "BT /F1 30 Tf 50 80 Td <00220023> Tj ET";

    /// <summary>The content that shows A at 50 points from (10, 20).</summary>
    private const string SimpleContent = "BT /F1 50 Tf 10 20 Td (A) Tj ET";

    /// <summary>The content that shows CID 34 at 50 points from (10, 20).</summary>
    private const string CompositeContent = "BT /F1 50 Tf 10 20 Td <0022> Tj ET";

    /// <summary>An embedded TrueType font draws A.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task TrueTypeTextDraws() => AssertDrawsA(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = Nonsymbolic, Entries = "/Encoding /WinAnsiEncoding", Content = SimpleContent, });

    /// <summary>An embedded Type 1 font draws A.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task Type1TextDraws()
    {
        var program = TestFontPrograms.Type1(out var length1);
        return AssertDrawsA(new()
        {
            Subtype = "Type1",
            Program = program,
            FileKey = "FontFile",
            FileEntries = string.Create(CultureInfo.InvariantCulture, $"/Length1 {length1}"),
            Flags = Symbolic,
            Content = SimpleContent,
        });
    }

    /// <summary>An embedded CFF font draws A.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task CffTextDraws() => AssertDrawsA(new()
    {
        Subtype = "Type1",
        Program = TestFontPrograms.Cff(),
        FileKey = "FontFile3",
        FileEntries = "/Subtype /Type1C",
        Flags = Nonsymbolic,
        Content = SimpleContent,
    });

    /// <summary>An embedded CIDFontType2 font draws CID 34, which is A.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task CompositeTextDraws() => AssertDrawsA(new() { Subtype = "Type0", Program = TestFont.Create(), Flags = Symbolic, Content = CompositeContent, });

    /// <summary>An overridden vertical advance moves the second glyph farther down while keeping the same column.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalTextUsesW2Advance()
    {
        var font = new FontSpec { Subtype = "Type0", Program = TestFont.Create(), Flags = Symbolic, CidEncoding = "/Identity-V", CidEntries = "/DW2 [880 -1000]", Content = VerticalContent, };
        using var defaultPage = new RenderTestPage(FontTestDocument.Build(font));
        using var changedPage = new RenderTestPage(FontTestDocument.Build(font with { CidEntries = "/DW2 [880 -1000] /W2 [34 [-2000 250 800]]" }));
        var normal = InkExtentOf(defaultPage.RenderPage());
        var changed = InkExtentOf(changedPage.RenderPage());

        await Assert.That(normal.IsEmpty).IsFalse();
        await Assert.That(changed.IsEmpty).IsFalse();
        await Assert.That(changed.MaxY).IsGreaterThan(normal.MaxY);
        await Assert.That(changed.MinX).IsEqualTo(normal.MinX).Within(Tolerance);
        await Assert.That(changed.MaxX).IsEqualTo(normal.MaxX).Within(Tolerance);
    }

    /// <summary>Renders a spec's page and checks A is drawn where expected.</summary>
    /// <param name="spec">The font.</param>
    /// <returns>A task.</returns>
    private static async Task AssertDrawsA(FontSpec spec)
    {
        using var page = new RenderTestPage(FontTestDocument.Build(spec));
        var image = page.RenderPage();

        await Assert.That(image.IsNear(InsideX, InsideY, Rgb.Black, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(InsideX, AboveY, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(RightX, InsideY, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>Finds the ink bounds of a rendered page.</summary>
    /// <param name="image">The page.</param>
    /// <returns>The bounds, or an empty extent.</returns>
    private static InkExtent InkExtentOf(RenderedImage image)
    {
        var minX = image.Width;
        var minY = image.Height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.Pixels[((y * image.Width) + x) * RenderedImage.BytesPerPixel] >= InkThreshold)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return new(minX, maxX, minY, maxY);
    }

    /// <summary>The outer pixels containing text ink.</summary>
    /// <param name="MinX">The left edge.</param>
    /// <param name="MaxX">The right edge.</param>
    /// <param name="MinY">The top edge.</param>
    /// <param name="MaxY">The bottom edge.</param>
    private readonly record struct InkExtent(int MinX, int MaxX, int MinY, int MaxY)
    {
        /// <summary>Gets whether the page has no ink.</summary>
        internal bool IsEmpty => MaxX < MinX || MaxY < MinY;
    }
}
