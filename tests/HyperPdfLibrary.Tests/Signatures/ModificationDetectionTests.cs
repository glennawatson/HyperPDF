// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Tests that changes made after signing are found, classified and judged against DocMDP and field locks.</summary>
public sealed class ModificationDetectionTests
{
    /// <summary>DocMDP P 1: no changes.</summary>
    private const int NoChanges = 1;

    /// <summary>DocMDP P 2: form filling and signing.</summary>
    private const int FormFill = 2;

    /// <summary>DocMDP P 3: also annotations.</summary>
    private const int Annotate = 3;

    /// <summary>A lock on the text field.</summary>
    private const string NameLock = "<< /Type /SigFieldLock /Action /Include /Fields [(Name)] >>";

    /// <summary>A lock on every field except the text field.</summary>
    private const string ExceptNameLock = "<< /Type /SigFieldLock /Action /Exclude /Fields [(Name)] >>";

    /// <summary>An added comment is an annotation change, allowed without DocMDP, and the signature stays valid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationIsDetected()
    {
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.AddAnnotation(Sign(0, string.Empty)));

        using (Assert.Multiple())
        {
            await Assert.That(report.ModifiedAfterSigning).IsTrue();
            await Assert.That(report.CoversWholeDocument).IsFalse();
            await Assert.That(report.ChangeKinds).IsEqualTo(PdfModificationKinds.Annotation);
            await Assert.That(report.PermittedByMdp).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(ChangedObjects(report)).Contains(SignatureSamples.AddedAnnotation);
        }
    }

    /// <summary>A changed field value is a form fill naming the field.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormFillIsDetected()
    {
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.FillForm(Sign(0, string.Empty)));

        await Assert.That(report.ChangeKinds).IsEqualTo(PdfModificationKinds.FormFill);
        await Assert.That(report.Changes[0].FieldName).IsEqualTo("Name");
        await Assert.That(report.Changes[0].ObjectNumber).IsEqualTo(SignatureSamples.NameField);
    }

    /// <summary>A rotated page is a page change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageChangeIsDetected()
    {
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.ChangePage(Sign(0, string.Empty)));

        await Assert.That(report.ChangeKinds).IsEqualTo(PdfModificationKinds.Pages);
    }

    /// <summary>A catalog change that is not form, page or validation data is another change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherChangeIsDetected()
    {
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.ChangeCatalog(Sign(0, string.Empty), 0));

        await Assert.That(report.ChangeKinds).IsEqualTo(PdfModificationKinds.Other);
    }

    /// <summary>DocMDP P 1 rejects an added comment.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoChangesRejectsAnnotation()
    {
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.AddAnnotation(Sign(NoChanges, string.Empty)));

        await Assert.That(report.Signature.IsCertification).IsTrue();
        await Assert.That(report.Signature.DocMdp).IsEqualTo(PdfMdpPermission.NoChanges);
        await Assert.That(report.PermittedByMdp).IsFalse();
        await Assert.That(report.SignatureValid).IsTrue();
    }

    /// <summary>DocMDP P 2 allows form filling but not comments.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormFillPermissionAllowsOnlyForms()
    {
        var filled = SignatureFixtures.ValidateSingle(SignatureSamples.FillForm(Sign(FormFill, string.Empty)));
        var commented = SignatureFixtures.ValidateSingle(SignatureSamples.AddAnnotation(Sign(FormFill, string.Empty)));

        await Assert.That(filled.PermittedByMdp).IsTrue();
        await Assert.That(commented.PermittedByMdp).IsFalse();
    }

    /// <summary>DocMDP P 3 allows comments, but not page changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotatePermissionAllowsComments()
    {
        var commented = SignatureFixtures.ValidateSingle(SignatureSamples.AddAnnotation(Sign(Annotate, string.Empty)));
        var rotated = SignatureFixtures.ValidateSingle(SignatureSamples.ChangePage(Sign(Annotate, string.Empty)));

        await Assert.That(commented.PermittedByMdp).IsTrue();
        await Assert.That(rotated.PermittedByMdp).IsFalse();
    }

    /// <summary>A field lock that includes a field rejects filling it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncludeLockRejectsLockedField()
    {
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.FillForm(Sign(0, NameLock)));

        await Assert.That(report.Signature.Lock!.Action).IsEqualTo(PdfFieldLockAction.Include);
        await Assert.That(report.Signature.Lock.Fields).IsEquivalentTo(["Name"]);
        await Assert.That(report.PermittedByMdp).IsFalse();
    }

    /// <summary>A field lock that excludes a field allows filling it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExcludeLockAllowsExcludedField()
    {
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.FillForm(Sign(0, ExceptNameLock)));

        await Assert.That(report.PermittedByMdp).IsTrue();
    }

    /// <summary>Adding a /DSS store is allowed even when no changes are permitted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecurityStoreIsAllowedUnderNoChanges()
    {
        var signed = Sign(NoChanges, string.Empty);
        var file = SignatureSamples.AddSecurityStore(signed, SignatureSamples.SignedCatalog(NoChanges), [SignatureFixtures.Signer.RawData], new(1, 0), string.Empty);
        var report = SignatureFixtures.ValidateSingle(file);

        await Assert.That(report.ChangeKinds).IsEqualTo(PdfModificationKinds.SecurityStore);
        await Assert.That(report.PermittedByMdp).IsTrue();
    }

    /// <summary>Signs the sample form.</summary>
    /// <param name="docMdp">The DocMDP permission, or 0.</param>
    /// <param name="fieldLock">A /Lock dictionary, or an empty string.</param>
    /// <returns>The signed file.</returns>
    private static byte[] Sign(int docMdp, string fieldLock) => SignatureSamples.Signed(SignatureFixtures.Signer, docMdp, fieldLock);

    /// <summary>Lists the numbers of the changed objects.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The numbers.</returns>
    private static List<int> ChangedObjects(PdfSignatureValidationReport report)
    {
        var numbers = new List<int>();
        foreach (var change in report.Changes)
        {
            numbers.Add(change.ObjectNumber);
        }

        return numbers;
    }
}
