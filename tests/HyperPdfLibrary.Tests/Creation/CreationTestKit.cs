// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Tests.Writing;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Opens the test font, checks created files with qpdf and reads back pixels.</summary>
internal static class CreationTestKit
{
    /// <summary>The bytes of a rendered pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The offset of the green sample in a rendered pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red sample in a rendered pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>Opens the synthetic test font and fails the test when it is refused.</summary>
    /// <returns>The font.</returns>
    /// <exception cref="InvalidOperationException">The font is refused; the message is the reason.</exception>
    internal static PdfFontSource OpenFont()
    {
        var result = PdfFontSource.Open(TestFont.Create(), 0);
        return result.Source ?? throw new InvalidOperationException(result.Refusal);
    }

    /// <summary>Checks a document with <c>qpdf --check</c>, skipping the test when qpdf is not installed.</summary>
    /// <param name="name">The file name without extension.</param>
    /// <param name="pdf">The document.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the file is clean, or fails the test when it is not.</returns>
    internal static async Task CheckWithQpdfAsync(string name, byte[] pdf, CancellationToken cancellationToken)
    {
        CreatedFiles.Keep($"{name}.pdf", pdf);
        var outcome = await QpdfCheck.RunAsync(name, pdf, cancellationToken);
        if (outcome is not { } result)
        {
            Skip.Test("qpdf is not installed.");
            return;
        }

        await Assert.That(result.IsClean).IsTrue().Because(result.Output);
    }

    /// <summary>Reads a pixel of a render.</summary>
    /// <param name="image">The render.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row, from the top.</param>
    /// <returns>The red, green and blue levels.</returns>
    internal static PixelColor PixelAt(RenderedImage image, int x, int y)
    {
        var at = ((y * image.Width) + x) * PixelBytes;
        return new(image.Pixels[at + RedOffset], image.Pixels[at + GreenOffset], image.Pixels[at]);
    }
}
