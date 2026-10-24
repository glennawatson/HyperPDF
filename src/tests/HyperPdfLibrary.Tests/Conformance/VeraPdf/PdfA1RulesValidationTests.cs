// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// The independent PDF/A-1 validator veraPDF gives the same verdict as HyperPdf on each hand-built PDF/A-1b case for
/// the font, transparency, annotation and action rules of the corrigenda.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfA1RulesValidationTests
{
    /// <summary>The veraPDF flavour of PDF/A-1b.</summary>
    private const string Flavour = "1b";

    /// <summary>Each case's rule passes or fails in veraPDF as the case expects, and as HyperPdf finds.</summary>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task VeraPdfAgreesOnEachCase(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("pdfa1-rules");
        var cases = PdfA1Cases.All();
        var paths = new List<string>();
        foreach (var item in cases)
        {
            var path = Path.Combine(folder.Path, $"{item.Name}.pdf");
            await File.WriteAllBytesAsync(path, item.File, cancellationToken);
            paths.Add(path);
        }

        var jobs = await VeraPdfValidator.ValidateAsync(home, Flavour, paths, cancellationToken);
        var disagreements = new List<string>();
        for (var i = 0; i < cases.Length; i++)
        {
            var reference = PdfARuleResults.Reference(PdfALevel.PdfA1B, cases[i].Rule);
            var job = jobs[Path.GetFullPath(paths[i])];
            var veraFails = job.Failures.Exists(failure => failure.Clause == reference.Clause && failure.TestNumber == reference.Test);
            if (veraFails != cases[i].Fails)
            {
                disagreements.Add($"{cases[i].Name}: §{reference.Clause} test {reference.Test} expected {(cases[i].Fails ? "fail" : "pass")}, veraPDF {(veraFails ? "fails" : "passes")}");
            }
        }

        await Assert.That(disagreements).IsEmpty().Because(string.Join(Environment.NewLine, disagreements));
    }
}
