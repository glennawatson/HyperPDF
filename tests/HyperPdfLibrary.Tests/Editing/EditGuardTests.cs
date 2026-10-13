// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests that document-level edits are refused when a certification or a PDF/A claim does not allow them.</summary>
public sealed class EditGuardTests
{
    /// <summary>DocMDP level 1: no changes.</summary>
    private const int NoChanges = 1;

    /// <summary>DocMDP level 3: form filling, signing and annotations.</summary>
    private const int Annotating = 3;

    /// <summary>The third part of ISO 19005.</summary>
    private const int PdfAPart3 = 3;

    /// <summary>A certified document refuses a name tree edit at every level, saying why, and changes nothing.</summary>
    /// <param name="level">The DocMDP level.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(NoChanges)]
    [Arguments(Annotating)]
    public async Task CertifiedDocumentRefusesNameTreeEdits(int level)
    {
        using var document = PdfDocumentReader.Open(EditGuardSamples.Certified(level), null);

        var thrown = await Assert.That(() => PdfDocumentNameTrees.SetEntry(document, PdfNameTreeKind.Destinations, "a", PdfValue.FromInteger(1))).Throws<InvalidOperationException>();

        await Assert.That(thrown!.Message).Contains("certified");
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(0);
        await Assert.That(PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations)).IsEmpty();
    }

    /// <summary>A PDF/A-1 document refuses embedded files and JavaScript; PDF/A-3 takes embedded files but not JavaScript.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfAClaimRefusesWhatItForbids()
    {
        using var a1 = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A1B), null);
        using var a3 = PdfDocumentReader.Open(PdfAFixtures.Create(new(PdfAPart3, "B")), null);

        await Assert.That(() => PdfDocumentNameTrees.SetEntry(a1, PdfNameTreeKind.EmbeddedFiles, "a", default)).Throws<InvalidOperationException>();
        await Assert.That(() => PdfDocumentNameTrees.SetEntry(a1, PdfNameTreeKind.JavaScript, "a", default)).Throws<InvalidOperationException>();
        await Assert.That(() => PdfDocumentNameTrees.SetEntry(a3, PdfNameTreeKind.JavaScript, "a", default)).Throws<InvalidOperationException>();
        PdfDocumentNameTrees.SetEntry(a3, PdfNameTreeKind.Destinations, "a", PdfValue.FromArray(new(a3.Objects)));
        await Assert.That(PdfDocumentNameTrees.GetEntries(a3, PdfNameTreeKind.Destinations).Count).IsEqualTo(1);
    }
}
