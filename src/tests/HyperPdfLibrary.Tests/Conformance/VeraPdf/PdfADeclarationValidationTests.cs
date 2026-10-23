// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>Checks with veraPDF that a PDF/A file keeps meeting its claim after a PDF Declaration and its extension schema are added.</summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class PdfADeclarationValidationTests
{
    /// <summary>The profile the test file declares.</summary>
    private const string Profile = "http://pdfa.org/declarations/wtpdf/#reuse1.0";

    /// <summary>The number of declarations the test adds.</summary>
    private const int Two = 2;

    /// <summary>The control: the same declaration written without the extension schema breaks the claim of parts 1 to 3, so the schema is what makes the file pass.</summary>
    /// <param name="part">The PDF/A part.</param>
    /// <param name="cancellationToken">Cancels the saves and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task DeclarationWithoutTheSchemaFails(int part, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var level = new PdfAFixtureLevel(part, "B");
        using var folder = new PdfAOutputFolder($"declaration-control-{level.Flavour}");
        var path = Path.Combine(folder.Path, "undescribed.pdf");
        using (var document = PdfDocumentReader.Open(PdfAFixtures.Create(level), null))
        {
            var stream = document.Catalog.GetStream(KnownName.Metadata)!;
            var packet = PdfDeclarationWriter.Add(stream.DecodeToArray(cancellationToken), new(Profile, []))!;
            StoreEditing.Replace(document.Objects, document.Catalog.GetRaw(KnownName.Metadata).AsReference(), PdfValue.FromStream(new(stream.Dictionary.Clone(), packet)));

            // The control must stay broken, so it is written without the claim check, which would repair it.
            await File.WriteAllBytesAsync(path, PdfCompactWriter.SaveUnchecked(document.Objects, PdfCompactOptions.Default), cancellationToken);
        }

        var job = (await VeraPdfValidator.ValidateAsync(home, level.Flavour, [path], cancellationToken))[Path.GetFullPath(path)];

        await Assert.That(job.IsCompliant).IsFalse().Because("a declaration without its extension schema breaks the claim");
    }

    /// <summary>A file in each PDF/A part passes veraPDF after a declaration with claim data is added, and still passes after a second declaration.</summary>
    /// <param name="part">The PDF/A part.</param>
    /// <param name="conformance">The conformance level, or <see langword="null"/> for part 4.</param>
    /// <param name="cancellationToken">Cancels the saves and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "B")]
    [Arguments(2, "B")]
    [Arguments(2, "U")]
    [Arguments(3, "B")]
    [Arguments(4, null)]
    public async Task DeclarationKeepsThePdfAClaim(int part, string? conformance, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var level = new PdfAFixtureLevel(part, conformance);
        using var folder = new PdfAOutputFolder($"declaration-{level.Flavour}");
        var path = Path.Combine(folder.Path, "declared.pdf");
        using (var document = PdfDocumentReader.Open(PdfAFixtures.Create(level), null))
        {
            var claim = new PdfDeclarationClaim("Example Validator & Co", "2026-10-19", "Certified <tester>", "https://example.org/report");
            PdfDocumentDeclarations.Add(document, new(Profile, [claim]));
            PdfDocumentDeclarations.Add(document, new("https://www.w3.org/TR/WCAG22/", []));
            await File.WriteAllBytesAsync(path, PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), cancellationToken);
        }

        var results = await VeraPdfValidator.ValidateAsync(home, VeraPdfValidator.DetectFlavour, [path], cancellationToken);
        var job = results[Path.GetFullPath(path)];
        using var reopened = PdfDocumentReader.Open(path, null);

        using (Assert.Multiple())
        {
            await Assert.That(PdfAReadingComparison.ClaimFromProfile(job.ProfileName)).IsEqualTo(level.Label);
            await Assert.That(job.IsCompliant).IsTrue().Because($"the declared file fails {job.FailureSummary}");
            await Assert.That(PdfDocumentMetadata.GetXmp(reopened)!.Declarations.Length).IsEqualTo(Two).Because("the saved file still holds both declarations");
        }
    }
}
