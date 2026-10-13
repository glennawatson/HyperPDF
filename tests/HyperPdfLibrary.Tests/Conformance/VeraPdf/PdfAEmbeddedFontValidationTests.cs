// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Tests.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Embeds fonts in PDF/A files with the library and checks them with veraPDF: ISO 19005 requires every font to be
/// embedded and its program, widths and encoding to be complete, so a subset font must pass its claimed profile.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfAEmbeddedFontValidationTests
{
    /// <summary>The text shown, using capitals and a space.</summary>
    private const string Plain = "Hello World";

    /// <summary>The text shown with an accented character, which the CFF test font builds with the accented form of endchar.</summary>
    private const string Accented = "Hello World é";

    /// <summary>A subset TrueType or OpenType CFF font in a PDF/A file passes veraPDF and keeps its claim.</summary>
    /// <param name="part">The PDF/A part.</param>
    /// <param name="conformance">The conformance level.</param>
    /// <param name="cff">Whether the font has CFF outlines rather than TrueType ones.</param>
    /// <param name="line">The text shown.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "B", false, Accented)]
    [Arguments(2, "B", false, Accented)]
    [Arguments(2, "U", false, Accented)]
    [Arguments(3, "B", false, Accented)]
    [Arguments(1, "B", true, Plain)]
    [Arguments(2, "U", true, Plain)]
    [Arguments(3, "B", true, Plain)]
    [Arguments(2, "U", true, Accented)]
    [Arguments(3, "B", true, Accented)]
    public async Task SubsetFontsPassTheClaimedProfile(int part, string conformance, bool cff, string line, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var level = new PdfAFixtureLevel(part, conformance);
        var file = cff ? TestCffFont.Create() : TestFont.Create();
        var pdf = PdfAFixtures.Create(level, EmbeddedFontPdf.Open(file, 0), line);
        using var folder = new PdfAOutputFolder($"font-{level.Flavour}-{(cff ? "cff" : "truetype")}");
        var path = Path.Combine(folder.Path, "font.pdf");
        await File.WriteAllBytesAsync(path, pdf, cancellationToken);

        var results = await VeraPdfValidator.ValidateAsync(home, VeraPdfValidator.DetectFlavour, [path], cancellationToken);
        var job = results[Path.GetFullPath(path)];

        await Assert.That(PdfAReadingComparison.ClaimFromProfile(job.ProfileName)).IsEqualTo(level.Label);
        await Assert.That(job.IsCompliant).IsTrue().Because($"the embedded font fails {job.FailureSummary}");
    }
}
