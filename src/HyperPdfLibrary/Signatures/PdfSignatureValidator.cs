// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Checks every signature of a document and reports byte ranges, later changes, MDP and the CMS result.</summary>
internal static class PdfSignatureValidator
{
    /// <summary>Checks every signature.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">How to check.</param>
    /// <returns>One report per entry of <see cref="PdfDocumentSignatureValidation.GetSignatureDetails"/>.</returns>
    internal static PdfSignatureValidationReport[] Validate(PdfDocument document, PdfSignatureValidationOptions options)
    {
        var details = PdfDocumentSignatureValidation.GetSignatureDetails(document);
        var revisions = PdfDocumentSignatureValidation.GetRevisionArray(document);
        var file = document.Objects.Source;
        var ranges = new PdfByteRangeCheck[details.Count];
        for (var i = 0; i < ranges.Length; i++)
        {
            var field = details[i].Field;
            ranges[i] = PdfByteRangeChecker.Check(field.ByteRange, field.Contents.Length, file, revisions);
        }

        var context = CmsContext.Create(PdfDocumentSignatureValidation.GetSecurityStore(document), options);
        var reports = new PdfSignatureValidationReport[ranges.Length];
        for (var i = 0; i < reports.Length; i++)
        {
            var range = ranges[i];
            var modified = range.IsValid && !PdfByteRangeChecker.IsBlank(file, range.SignedEnd);
            var changes = modified ? Changes(document, range.SignedEnd) : [];
            Evaluate(changes, details, ranges, range.SignedEnd);
            reports[i] = new(details[i], range, modified, changes, PdfCmsVerifier.Verify(details[i], range, file, context));
        }

        return reports;
    }

    /// <summary>Lists the changes made after a signed revision.</summary>
    /// <param name="document">The document.</param>
    /// <param name="signedEnd">The position after the signed bytes.</param>
    /// <returns>The changes; one unclassified change when the signed revision cannot be read.</returns>
    private static PdfObjectChange[] Changes(PdfDocument document, long signedEnd)
    {
        try
        {
            // The earlier revision borrows the document's security handler, so disposing it frees only its own caches.
            using var signed = StoreRevisions.OpenRevision(document.Objects, signedEnd);
            return PdfRevisionComparer.Compare(document.Objects, signed, signedEnd);
        }
        catch (PdfException)
        {
            return [new(0, PdfModificationKinds.Other, null, false)];
        }
    }

    /// <summary>Marks each change as permitted or not, under the certification and locks signed at or before a signature.</summary>
    /// <param name="changes">The changes after the signature.</param>
    /// <param name="details">Every signature.</param>
    /// <param name="ranges">Every signature's byte range check.</param>
    /// <param name="signedEnd">The position after this signature's signed bytes.</param>
    private static void Evaluate(PdfObjectChange[] changes, IReadOnlyList<PdfSignatureDetails> details, PdfByteRangeCheck[] ranges, long signedEnd)
    {
        if (changes.Length == 0)
        {
            return;
        }

        var certification = PdfMdpPermission.None;
        var locks = new List<PdfFieldLock>();
        for (var i = 0; i < details.Count; i++)
        {
            if (!ranges[i].IsValid || ranges[i].SignedEnd > signedEnd)
            {
                continue;
            }

            certification = details[i].IsCertification ? CertificationPermission(details[i]) : certification;
            AddLock(locks, details[i].Lock);
            AddLock(locks, details[i].FieldMdp);
        }

        var permission = PdfMdpEvaluator.Effective(certification, locks);
        for (var i = 0; i < changes.Length; i++)
        {
            changes[i] = changes[i] with
            {
                IsPermitted = PdfMdpEvaluator.IsPermitted(changes[i], permission, locks)
            };
        }
    }

    /// <summary>Gets a certification signature's permission; a certification without a DocMDP /P allows form filling and signing.</summary>
    /// <param name="details">The certification signature.</param>
    /// <returns>The permission.</returns>
    private static PdfMdpPermission CertificationPermission(PdfSignatureDetails details) => details.DocMdp == PdfMdpPermission.None ? PdfMdpPermission.FormFillAndSign : details.DocMdp;

    /// <summary>Adds a lock when there is one.</summary>
    /// <param name="locks">The locks.</param>
    /// <param name="fieldLock">The lock, or <see langword="null"/>.</param>
    private static void AddLock(List<PdfFieldLock> locks, PdfFieldLock? fieldLock)
    {
        if (fieldLock is not null)
        {
            locks.Add(fieldLock);
        }
    }
}
