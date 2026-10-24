// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Checks the throwing font-open helpers agree with the structured-result forms.</summary>
public sealed class FontOpenOrThrowTests
{
    /// <summary>A bundled font that opens returns the same non-null source kind as the result form.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task OpenOrThrowReturnsTheBundledFont()
    {
        var result = PdfBundledFonts.Open(PdfBundledFontFamily.Symbol, false, false);
        var source = PdfBundledFonts.OpenOrThrow(PdfBundledFontFamily.Symbol, false, false);

        await Assert.That(result.IsEmbeddable).IsTrue();
        await Assert.That(source.PostScriptName).IsEqualTo(result.Source!.PostScriptName);
    }

    /// <summary>A damaged font file throws a <see cref="PdfException"/> that carries the refusal text.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task OpenOrThrowThrowsWithTheRefusal()
    {
        byte[] damaged = [1, 2, 3, 4];
        var result = PdfFontSource.Open(damaged);

        var exception = Assert.Throws<PdfException>(() => PdfFontSource.OpenOrThrow(damaged));

        await Assert.That(result.IsEmbeddable).IsFalse();
        await Assert.That(exception.Error).IsEqualTo(PdfError.FontRefused);
        await Assert.That(exception.Message).IsEqualTo(result.Refusal);
    }

    /// <summary>A null font file still reports the argument error, as the result form does.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task OpenOrThrowRejectsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(static () => PdfFontSource.OpenOrThrow(null!));

        await Assert.That(exception.ParamName).IsEqualTo("fontFile");
    }
}
