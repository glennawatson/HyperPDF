// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Checks that a host can read the font files embedded in the library.</summary>
public sealed class BundledFontFileTests
{
    /// <summary>A family number past the last defined family.</summary>
    private const PdfBundledFontFamily UndefinedFamily = (PdfBundledFontFamily)99;

    /// <summary>The first four bytes of a TrueType font file.</summary>
    private static readonly byte[] TrueTypeHeader = [0x00, 0x01, 0x00, 0x00];

    /// <summary>The file of each built in family opens as the font the embedding path opens.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the face is bold.</param>
    /// <param name="italic">Whether the face slants.</param>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    [Arguments(PdfBundledFontFamily.Sans, false, false)]
    [Arguments(PdfBundledFontFamily.Serif, true, false)]
    [Arguments(PdfBundledFontFamily.Fixed, false, true)]
    [Arguments(PdfBundledFontFamily.Sans, true, true)]
    public async Task TheFileOpensAsTheEmbeddedFont(PdfBundledFontFamily family, bool bold, bool italic)
    {
        var file = PdfBundledFonts.GetFontFile(family, bold, italic);
        var opened = PdfBundledFonts.OpenOrThrow(family, bold, italic);

        await Assert.That(file).IsNotNull();
        await Assert.That(file!.AsSpan(0, TrueTypeHeader.Length).SequenceEqual(TrueTypeHeader)).IsTrue();
        await Assert.That(PdfFontSource.OpenOrThrow(file).PostScriptName).IsEqualTo(opened.PostScriptName);
    }

    /// <summary>Each call returns its own copy, so a host cannot change the font the library draws with.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task EachCallReturnsACopy()
    {
        var first = PdfBundledFonts.GetFontFile(PdfBundledFontFamily.Sans, false, false)!;
        first[0] = 0xFF;
        var second = PdfBundledFonts.GetFontFile(PdfBundledFontFamily.Sans, false, false)!;

        await Assert.That(second[0]).IsEqualTo((byte)0);
    }

    /// <summary>A family number that is not defined is refused.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task AnUndefinedFamilyIsRefused() =>
        await Assert.That(static () => PdfBundledFonts.GetFontFile(UndefinedFamily, false, false)).Throws<ArgumentOutOfRangeException>();
}
