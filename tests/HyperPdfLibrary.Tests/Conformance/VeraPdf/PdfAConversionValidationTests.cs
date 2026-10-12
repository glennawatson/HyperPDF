// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>Converts generated documents to PDF/A and checks every converted file with veraPDF.</summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfAConversionValidationTests
{
    /// <summary>
    /// A page with non-embedded Helvetica, a JavaScript open action, a launch link and no output intent converts to each
    /// level, and veraPDF passes the result.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <param name="flavour">The veraPDF flavour of the level.</param>
    /// <param name="cancellationToken">Cancels the conversions and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfALevel.PdfA1B, "1b")]
    [Arguments(PdfALevel.PdfA2B, "2b")]
    [Arguments(PdfALevel.PdfA2U, "2u")]
    [Arguments(PdfALevel.PdfA3B, "3b")]
    [Arguments(PdfALevel.PdfA4, "4")]
    public async Task ConvertedFilesPassVeraPdf(PdfALevel level, string flavour, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder($"converted-{flavour}");
        var files = new List<string>();
        foreach (var (name, source) in Sources())
        {
            using var document = PdfDocumentReader.Open(source, null);
            var path = Path.Combine(folder.Path, $"{name}.pdf");
            await using (var stream = File.Create(path))
            {
                var result = await PdfAConverter.SaveAsPdfAAsync(document, level, stream, cancellationToken);
                await Assert.That(result.Converted).IsTrue().Because(string.Join("; ", result.Problems.Select(static p => p.Description)));
            }

            files.Add(path);
        }

        var results = await VeraPdfValidator.ValidateAsync(home, flavour, files, cancellationToken);
        foreach (var file in files)
        {
            var job = results[Path.GetFullPath(file)];
            await Assert.That(job.IsCompliant).IsTrue().Because($"{Path.GetFileName(file)} fails {job.FailureSummary}");
        }
    }

    /// <summary>Gets the generated sources.</summary>
    /// <returns>Each source's name and bytes.</returns>
    private static IEnumerable<KeyValuePair<string, byte[]>> Sources() =>
    [
        new("helvetica", ConversionTestPdf.HelveticaText()),
        new("scripts", ConversionTestPdf.Scripts()),
        new("invisible-text", ConversionTestPdf.InvisibleText()),
        new("unused-dingbats", ConversionTestPdf.UnusedDingbatsInFormResources()),
        new("dingbats-field", ConversionTestPdf.DingbatsFieldWithSavedAppearance()),
        new("unused-operator", ConversionTestPdf.UnusedUndefinedOperatorForm()),
        new("unused-cmyk", ConversionTestPdf.UnusedCmykForm()),
        new("unused-nesting", ConversionTestPdf.UnusedDeepNestingForm()),
        new("unused-transparency", ConversionTestPdf.UnusedTransparentGraphicsState()),
    ];
}
