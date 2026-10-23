// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Conformance.VeraPdf;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks byte-string Note identifiers without preventing damaged documents from opening.</summary>
public sealed class AccessibilityNoteIdentifierTests
{
    /// <summary>The number of content items in the competing-ID test.</summary>
    private const int NotePair = 2;

    /// <summary>The PDF/UA-1 clause for Note identifiers.</summary>
    private const string NoteClause = "7.9";

    /// <summary>A Note ID is a byte string, including a uniquely used empty string.</summary>
    /// <param name="entries">The Note's ID entry.</param>
    /// <param name="expected">The expected number of identifier findings.</param>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", 1)]
    [Arguments("/ID /note", 1)]
    [Arguments("/ID 1", 1)]
    [Arguments("/ID null", 1)]
    [Arguments("/ID ()", 0)]
    [Arguments("/ID (note)", 0)]
    public async Task IdentifierMustBeAString(string entries, int expected, CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { Layout = (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element("Note", parent, $"/K 0 {entries}")) }));
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(PdfAccessibilityCode.NoteIdentifierInvalid)).IsEqualTo(expected);
        await Assert.That(PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.RawType).IsEqualTo("Note");
    }

    /// <summary>Uniqueness applies to bytes and to all structure elements, regardless of reading order.</summary>
    /// <param name="otherType">The competing element's type.</param>
    /// <param name="otherId">The competing ID value.</param>
    /// <param name="noteFirst">Whether the Note comes first.</param>
    /// <param name="expected">The expected number of invalid Notes.</param>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Note", "(note)", true, 2)]
    [Arguments("P", "(note)", true, 1)]
    [Arguments("P", "(note)", false, 1)]
    [Arguments("Note", "<6E6F7465>", false, 2)]
    [Arguments("Note", "<FEFF006E006F00740065>", false, 0)]
    [Arguments("Note", "(other)", false, 0)]
    public async Task IdentifiersAreUniqueBytes(string otherType, string otherId, bool noteFirst, int expected, CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()
        {
            Content = AccessibilityPdfs.Lines(NotePair),
            Layout = (pdf, parent) =>
            {
                var note = pdf.Element("Note", parent, "/K 0 /ID (note)");
                var other = pdf.Element(otherType, parent, $"/K 1 /ID {otherId}");
                return noteFirst ? AccessibilityPdfs.Kids(note, other) : AccessibilityPdfs.Kids(other, note);
            },
        }));
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(PdfAccessibilityCode.NoteIdentifierInvalid)).IsEqualTo(expected);
    }

    /// <summary>Role-mapped Notes carry the same requirement.</summary>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RoleMappedNotesRequireIdentifiers(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()
        {
            RootExtra = "/RoleMap << /Endnote /Note >>",
            Layout = static (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element("Endnote", parent, "/K 0")),
        }));
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(PdfAccessibilityCode.NoteIdentifierInvalid)).IsEqualTo(1);
    }

    /// <summary>Part 2 uses explicit references rather than the part 1 Note-ID requirement.</summary>
    /// <param name="cancellationToken">Cancels the report.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartTwoDoesNotRequireNoteIdentifiers(CancellationToken cancellationToken)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()
        {
            Xmp = AccessibilityPdfs.Packet(AccessibilityPdfs.PartTwo, true, "2024"),
            CatalogExtra = "/Version /2.0",
            Pdf20Root = true,
            Layout = static (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element("FENote", parent, "/K 0")),
        }));
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(PdfAccessibilityCode.NoteIdentifierInvalid)).IsEqualTo(0);
    }

    /// <summary>Checks independent detection, retaining the empty-ID interpretation difference explicitly.</summary>
    /// <param name="firstId">The first Note's ID entry.</param>
    /// <param name="secondId">The second Note's ID entry.</param>
    /// <param name="expected">The library's expected identifier finding count.</param>
    /// <param name="independentFailure">Whether veraPDF reports a Note-ID rule failure.</param>
    /// <param name="cancellationToken">Cancels writing and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", "/ID (second)", 1, true)]
    [Arguments("/ID (first)", "/ID (second)", 0, false)]
    [Arguments("/ID (first)", "/ID (first)", NotePair, true)]
    [Arguments("/ID (first)", "/ID <6669727374>", NotePair, true)]
    [Arguments("/ID ()", "/ID (second)", 0, true)]
    [NotInParallel(nameof(VeraPdfValidator))]
    public async Task NoteIdentifierRulesComparedWithVeraPdf(string firstId, string secondId, int expected, bool independentFailure, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("note-identifiers");
        var bytes = AccessibilityPdfs.Build(new()
        {
            Content = AccessibilityPdfs.Lines(NotePair),
            Layout = (pdf, parent) => AccessibilityPdfs.Kids(
                pdf.Element("Note", parent, $"/K 0 {firstId}"),
                pdf.Element("Note", parent, $"/K 1 {secondId}")),
        });
        var path = Path.Combine(folder.Path, "notes.pdf");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        using var document = TaggedTestDocument.Open(bytes);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(PdfAccessibilityCode.NoteIdentifierInvalid)).IsEqualTo(expected);
        var jobs = await VeraPdfValidator.ValidateAsync(home, "ua1", [path], cancellationToken);
        var job = jobs[Path.GetFullPath(path)];
        await Assert.That(job.ProfileName).IsEqualTo("PDF/UA-1 validation profile");
        await Assert.That(job.Failures.Exists(static failure => failure.Clause == NoteClause)).IsEqualTo(independentFailure).Because(job.FailureSummary);
    }
}
