// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Checks the signatures already in a document to see whether another signature may be added (ISO 32000-2, 12.8.2.2 and 12.8.2.4).</summary>
internal static class PdfSigningRules
{
    /// <summary>Checks whether a signature may be added and adds a plain reason to the problems for each rule it would break.</summary>
    /// <param name="details">The signatures already in the document.</param>
    /// <param name="field">The existing field to sign, or <see langword="null"/> for a new field.</param>
    /// <param name="certification">The certification the new signature would make.</param>
    /// <param name="problems">Receives the reasons.</param>
    internal static void Check(IReadOnlyList<PdfSignatureDetails> details, PdfSigningField? field, PdfMdpPermission certification, List<string> problems)
    {
        // A document with no signature fields has nothing that certifies it, locks fields or forbids a new signature.
        if (details.Count == 0)
        {
            return;
        }

        var certified = PdfMdpPermission.None;
        var anySigned = false;
        var locks = default(LockSummary);
        for (var i = 0; i < details.Count; i++)
        {
            var detail = details[i];
            if (!detail.IsSigned)
            {
                continue;
            }

            anySigned = true;
            certified = detail.IsCertification ? CertificationPermission(detail) : certified;
            locks = Add(Add(locks, detail.Lock, field), detail.FieldMdp, field);
        }

        // The effective permission is the strictest of the certification and the permissions of the locks (PdfMdpEvaluator.Effective).
        var effective = Strictest(certified, locks.Permission);
        if (effective == PdfMdpPermission.NoChanges)
        {
            problems.Add("An earlier signature in this document allows no further changes, so it cannot be signed again without breaking that signature.");
        }

        if (field is not null && locks.LocksField)
        {
            problems.Add($"An earlier signature locked the field \"{field.Name}\", so it cannot be signed now without breaking that signature.");
        }

        if (certification != PdfMdpPermission.None && anySigned)
        {
            problems.Add("A certification signature must be the first signature in a document, and this document already has a signature.");
        }
    }

    /// <summary>Determines whether the document already has a signature of any kind.</summary>
    /// <param name="details">The signatures.</param>
    /// <returns><see langword="true"/> when one holds a value.</returns>
    internal static bool HasSignature(IReadOnlyList<PdfSignatureDetails> details)
    {
        for (var i = 0; i < details.Count; i++)
        {
            if (details[i].IsSigned)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads a field's own signature lock (/Lock) so the new signature can carry it as a FieldMDP reference.</summary>
    /// <param name="field">The field, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The lock, or <see langword="null"/> when the field has none.</returns>
    internal static PdfFieldLock? FieldLockOf(PdfSigningField? field, PdfNameTable names) =>
        field is null ? null : PdfSignatureDictionaries.ReadLock(field.Dictionary.GetDictionary(KnownName.Lock), names);

    /// <summary>Gets the permission a certification signature sets; a certification without a DocMDP /P allows form filling and signing.</summary>
    /// <param name="detail">The certification signature.</param>
    /// <returns>The permission.</returns>
    private static PdfMdpPermission CertificationPermission(PdfSignatureDetails detail) =>
        detail.DocMdp == PdfMdpPermission.None ? PdfMdpPermission.FormFillAndSign : detail.DocMdp;

    /// <summary>Takes a lock into account when there is one.</summary>
    /// <param name="summary">What the earlier locks come to.</param>
    /// <param name="fieldLock">The lock, or <see langword="null"/>.</param>
    /// <param name="field">The field to sign, or <see langword="null"/> for a new field.</param>
    /// <returns>The summary with the lock added.</returns>
    private static LockSummary Add(LockSummary summary, PdfFieldLock? fieldLock, PdfSigningField? field) =>
        fieldLock is null
            ? summary
            : new(Strictest(summary.Permission, fieldLock.Permission), summary.LocksField || (field is not null && fieldLock.Locks(field.Name)));

    /// <summary>Gets the stricter of two permissions, where no permission imposes nothing.</summary>
    /// <param name="first">The first permission.</param>
    /// <param name="second">The second permission.</param>
    /// <returns>The stricter one.</returns>
    private static PdfMdpPermission Strictest(PdfMdpPermission first, PdfMdpPermission second)
    {
        if (first == PdfMdpPermission.None)
        {
            return second;
        }

        return second == PdfMdpPermission.None ? first : (PdfMdpPermission)Math.Min((int)first, (int)second);
    }

    /// <summary>What the locks of the earlier signatures come to.</summary>
    /// <param name="Permission">The strictest permission any lock sets, or none.</param>
    /// <param name="LocksField">Whether a lock covers the field about to be signed.</param>
    private readonly record struct LockSummary(PdfMdpPermission Permission, bool LocksField);
}
