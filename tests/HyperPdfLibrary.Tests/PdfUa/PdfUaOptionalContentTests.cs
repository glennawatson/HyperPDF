// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Tests.Accessibility;
using HyperPdfLibrary.Tests.Conformance.VeraPdf;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.PdfUa;

/// <summary>Checks the conditional configuration names and prohibited automatic states of PDF/UA-1 clause 7.10.</summary>
public sealed class PdfUaOptionalContentTests
{
    /// <summary>The title edit that forces an incremental revision.</summary>
    private const string EditedTitle = "Edited";

    /// <summary>Reports defective configurations without preventing the document from opening.</summary>
    /// <param name="properties">The optional-content properties.</param>
    /// <param name="expected">The number of configuration findings.</param>
    /// <param name="cancellationToken">Cancels reporting.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", 0)]
    [Arguments("/D << >>", 0)]
    [Arguments("/D << >> /Configs []", 0)]
    [Arguments("/D << >> /Configs [null 1 /Unknown]", 0)]
    [Arguments("/D << /AS [] >>", 1)]
    [Arguments("/D << /AS null >>", 0)]
    [Arguments("/D << /Name (Default) >> /Configs [<< /Name (Other) >>]", 0)]
    [Arguments("/D << >> /Configs [<< /Name (Other) >>]", 1)]
    [Arguments("/D << /Name (Default) >> /Configs [<< >>]", 1)]
    [Arguments("/D << /Name () >> /Configs [<< /Name /WrongType >>]", 2)]
    [Arguments("/D << /Name <FEFF> >> /Configs [<< /Name <FFFE> >>]", 2)]
    [Arguments("/D << /Name <FEFF0041> >> /Configs [<< /Name <FEFF0042> >>]", 0)]
    [Arguments("/D << /Name ( ) >> /Configs [<< /Name ( ) >>]", 0)]
    [Arguments("/D << /Name (Default) /AS [] >> /Configs [<< /Name (Other) /AS [] >>]", 2)]
    public async Task ReadReportsConfigurationDefects(string properties, int expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(properties), null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(PdfAccessibilityCode.OptionalContentNameMissing) + report.GetCount(PdfAccessibilityCode.OptionalContentAutomaticState)).IsEqualTo(expected);
        await Assert.That(document.PageCount).IsEqualTo(1);
    }

    /// <summary>Neither save mode silently keeps a PDF/UA-1 claim with invalid optional-content configuration data.</summary>
    /// <param name="incremental">Whether to append an incremental revision.</param>
    /// <param name="properties">The defective optional-content properties.</param>
    /// <param name="cancellationToken">Cancels saving.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, "/D << /AS [] >>")]
    [Arguments(true, "/D << /AS [] >>")]
    [Arguments(false, "/D << >> /Configs [<< /Name (Other) >>]")]
    [Arguments(true, "/D << >> /Configs [<< /Name (Other) >>]")]
    [Arguments(false, "/D << /Name (Default) >> /Configs [<< >>]")]
    [Arguments(true, "/D << /Name (Default) >> /Configs [<< >>]")]
    public async Task SaveRefusesInvalidClaim(bool incremental, string properties, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(properties), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();
        var caught = await Assert.That(async () => await SaveAsync(document, output, report, incremental, cancellationToken)).Throws<PdfException>();
        var error = await Assert.That(caught).IsNotNull();
        await Assert.That(error.Error).IsEqualTo(PdfError.ClaimUnmet);
        await Assert.That(error.Message).Contains("Clause 7.10");
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>The shared configuration rules apply to either PDF/UA part, not to unclaimed or unknown profiles.</summary>
    /// <param name="part">The identification part; zero omits it.</param>
    /// <param name="expected">The expected number of optional-content findings.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, 0)]
    [Arguments(1, 1)]
    [Arguments(2, 1)]
    [Arguments(3, 0)]
    public async Task ReadScopesRulesToKnownClaims(int part, int expected, CancellationToken cancellationToken)
    {
        var bytes = AccessibilityPdfs.Build(new() { Xmp = AccessibilityPdfs.Packet(part, true), CatalogExtra = "/OCProperties << /OCGs [] /D << /AS [] >> >>" });
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(PdfAccessibilityCode.OptionalContentNameMissing) + report.GetCount(PdfAccessibilityCode.OptionalContentAutomaticState)).IsEqualTo(expected);
    }

    /// <summary>Explicitly dropping an unmet claim preserves layer behaviour rather than guessing a repair.</summary>
    /// <param name="incremental">Whether to append an incremental revision.</param>
    /// <param name="cancellationToken">Cancels saving and reopening.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RemovingClaimPreservesConfiguration(bool incremental, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("/D << /AS [] >>"), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var report = new PdfSaveReport { UnmetUaClaim = PdfUaUnmetClaimAction.RemoveClaim };
        await using var output = new MemoryStream();
        await SaveAsync(document, output, report, incremental, cancellationToken);
        await Assert.That(report.UaClaim!.Removed).IsTrue();
        using var saved = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var configuration = saved.Catalog.GetDictionary(KnownName.OCProperties)!.GetDictionary(KnownName.D)!;
        await Assert.That(configuration.GetArray(KnownName.AS)).IsNotNull();
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(saved, cancellationToken);
        await Assert.That(accessibility.Claim.Part).IsNotEqualTo(PdfUaClaim.Part1);
    }

    /// <summary>Both save modes keep valid configuration names and the PDF/UA-1 identification.</summary>
    /// <param name="incremental">Whether to append an incremental revision.</param>
    /// <param name="cancellationToken">Cancels saving and reopening.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ValidConfigurationPreservesClaim(bool incremental, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("/D << /Name (Default) >> /Configs [<< /Name (Other) >>]"), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();
        await SaveAsync(document, output, report, incremental, cancellationToken);
        await Assert.That(report.UaClaim!.GetUnrepaired()).IsEmpty();
        using var saved = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var properties = saved.Catalog.GetDictionary(KnownName.OCProperties)!;
        await Assert.That(properties.GetDictionary(KnownName.D)!.GetText(KnownName.Name)).IsEqualTo("Default");
        await Assert.That(properties.GetArray(KnownName.Configs)!.GetDictionary(0)!.GetText(KnownName.Name)).IsEqualTo("Other");
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(saved, cancellationToken);
        await Assert.That(accessibility.Claim.Part).IsEqualTo(PdfUaClaim.Part1);
    }

    /// <summary>Compares configuration rules, retaining veraPDF's empty-Configs name-check difference explicitly.</summary>
    /// <param name="properties">The optional-content properties.</param>
    /// <param name="invalid">Whether the supplied clause 7.10 requires a finding.</param>
    /// <param name="independentInvalid">Whether veraPDF reports a clause 7.10 failure.</param>
    /// <param name="cancellationToken">Cancels writing and validation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("/D << /AS [] >>", true, true)]
    [Arguments("/D << >> /Configs [<< /Name (Other) >>]", true, true)]
    [Arguments("/D << /Name (Default) >> /Configs [<< >>]", true, true)]
    [Arguments("/D << /Name (Default) >> /Configs [<< /Name (Other) >>]", false, false)]
    [Arguments("/D << >> /Configs []", false, true)]
    [NotInParallel(nameof(VeraPdfValidator))]
    public async Task ConfigurationRulesComparedWithVeraPdf(string properties, bool invalid, bool independentInvalid, CancellationToken cancellationToken)
    {
        const string clause = "7.10";
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("optional-content");
        var path = Path.Combine(folder.Path, "configurations.pdf");
        var bytes = Build(properties);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var count = report.GetCount(PdfAccessibilityCode.OptionalContentNameMissing) + report.GetCount(PdfAccessibilityCode.OptionalContentAutomaticState);
        await Assert.That(count > 0).IsEqualTo(invalid);
        var jobs = await VeraPdfValidator.ValidateAsync(home, "ua1", [path], cancellationToken);
        var job = jobs[Path.GetFullPath(path)];
        await Assert.That(job.ProfileName).IsEqualTo("PDF/UA-1 validation profile");
        await Assert.That(job.Failures.Exists(static failure => failure.Clause == clause)).IsEqualTo(independentInvalid).Because(job.FailureSummary);
    }

    /// <summary>Builds an otherwise accessible one-page document with the supplied configuration dictionaries.</summary>
    /// <param name="properties">The optional-content property entries.</param>
    /// <returns>The document bytes.</returns>
    private static byte[] Build(string properties) =>
        AccessibilityPdfs.Build(new() { CatalogExtra = properties.Length == 0 ? string.Empty : $"/OCProperties << /OCGs [] {properties} >>" });

    /// <summary>Saves through the selected writing entry point.</summary>
    /// <param name="document">The edited document.</param>
    /// <param name="output">Receives saved bytes.</param>
    /// <param name="report">Receives the claim decision.</param>
    /// <param name="incremental">Whether to append a revision.</param>
    /// <param name="cancellationToken">Cancels writing.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static Task SaveAsync(PdfDocument document, MemoryStream output, PdfSaveReport report, bool incremental, CancellationToken cancellationToken) =>
        incremental
            ? PdfIncrementalWriter.SaveAsync(document.Objects, output, report, cancellationToken)
            : PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, cancellationToken);
}
