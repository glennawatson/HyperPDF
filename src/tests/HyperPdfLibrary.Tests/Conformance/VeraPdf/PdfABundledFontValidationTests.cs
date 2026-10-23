// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>Embeds the fonts bundled in the library in PDF/A files and checks them with veraPDF.</summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfABundledFontValidationTests
{
    /// <summary>The text shown, with an accent and a Windows ANSI quotation mark.</summary>
    private const string Line = "Hello World é “quoted”";

    /// <summary>A bundled font in a PDF/A file passes veraPDF and keeps its claim.</summary>
    /// <param name="part">The PDF/A part.</param>
    /// <param name="conformance">The conformance level.</param>
    /// <param name="family">The bundled family.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "B", PdfBundledFontFamily.Sans)]
    [Arguments(2, "U", PdfBundledFontFamily.Sans)]
    [Arguments(3, "B", PdfBundledFontFamily.Sans)]
    [Arguments(1, "B", PdfBundledFontFamily.Serif)]
    [Arguments(2, "U", PdfBundledFontFamily.Serif)]
    [Arguments(3, "B", PdfBundledFontFamily.Serif)]
    [Arguments(1, "B", PdfBundledFontFamily.Fixed)]
    [Arguments(2, "U", PdfBundledFontFamily.Fixed)]
    [Arguments(3, "B", PdfBundledFontFamily.Fixed)]
    public async Task BundledFontsPassTheClaimedProfile(int part, string conformance, PdfBundledFontFamily family, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var level = new PdfAFixtureLevel(part, conformance);
        var source = PdfBundledFonts.Open(family, false, false).Source!;
        var pdf = PdfAFixtures.Create(level, source, Line);
        using var folder = new PdfAOutputFolder($"bundled-{level.Flavour}-{family}");
        var path = Path.Combine(folder.Path, "font.pdf");
        await File.WriteAllBytesAsync(path, pdf, cancellationToken);

        var results = await VeraPdfValidator.ValidateAsync(home, VeraPdfValidator.DetectFlavour, [path], cancellationToken);
        var job = results[Path.GetFullPath(path)];

        await Assert.That(PdfAReadingComparison.ClaimFromProfile(job.ProfileName)).IsEqualTo(level.Label);
        await Assert.That(job.IsCompliant).IsTrue().Because($"the bundled {family} font fails {job.FailureSummary}");
    }
}
