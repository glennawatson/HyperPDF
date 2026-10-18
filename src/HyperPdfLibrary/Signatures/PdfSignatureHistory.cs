// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Retains signatures and their signed permissions across bounded cross-reference revisions.</summary>
internal static class PdfSignatureHistory
{
    /// <summary>The largest signature scratch list retained after a read.</summary>
    private const int MaxKeptSignatures = 4096;

    /// <summary>Reads signed snapshots before unsigned catalog or field replacements can hide their authority.</summary>
    /// <param name="document">The current document.</param>
    /// <returns>Historical signed fields followed by fields that are only present in the current revision.</returns>
    internal static PdfSignatureDetails[] Read(PdfDocument document)
    {
        var store = document.Objects;
        var revisions = PdfDocumentSignatureValidation.GetRevisionArray(document);
        var result = store.ScratchPools.Slots<List<PdfSignatureDetails>>().Take() ?? [];
        var details = store.ScratchPools.Slots<List<PdfSignatureDetails>>().Take() ?? [];
        try
        {
            if (revisions.Length <= 1)
            {
                PdfSignatureDetailsReader.Read(store, result);
                return [.. result];
            }

            foreach (var revision in revisions)
            {
                PdfOpenContext.ThrowIfCancelled(store.Context);
                if (revision.XrefOffset < 0 || revision.EndOffset >= store.Source.Length)
                {
                    continue;
                }

                using var signed = StoreRevisions.OpenRevision(store, revision.EndOffset);
                PdfSignatureDetailsReader.Read(signed, details, CollectionsMarshal.AsSpan(result));
                AddSigned(result, CollectionsMarshal.AsSpan(details), store, revisions, revision);
            }

            PdfSignatureDetailsReader.Read(store, details, CollectionsMarshal.AsSpan(result));
            AddCurrent(result, CollectionsMarshal.AsSpan(details));
            return [.. result];
        }
        finally
        {
            ReturnScratch(store, details);
            ReturnScratch(store, result);
        }
    }

    /// <summary>Clears signature references before returning bounded scratch to the document.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="details">The reusable list.</param>
    private static void ReturnScratch(PdfObjectStore store, List<PdfSignatureDetails> details)
    {
        details.Clear();
        if (details.Capacity <= MaxKeptSignatures)
        {
            _ = store.ScratchPools.Slots<List<PdfSignatureDetails>>().Offer(details);
        }
    }

    /// <summary>Retains signatures only once their byte ranges close a genuine revision already present in the snapshot.</summary>
    /// <param name="result">The signatures retained so far.</param>
    /// <param name="details">Fields in one historical snapshot.</param>
    /// <param name="store">The complete document's objects.</param>
    /// <param name="revisions">The genuine cross-reference revisions.</param>
    /// <param name="snapshot">The revision whose permissions and fields are available.</param>
    private static void AddSigned(
        List<PdfSignatureDetails> result,
        ReadOnlySpan<PdfSignatureDetails> details,
        PdfObjectStore store,
        PdfRevision[] revisions,
        in PdfRevision snapshot)
    {
        foreach (var detail in details)
        {
            PdfOpenContext.ThrowIfCancelled(store.Context);
            if (!detail.IsSigned)
            {
                continue;
            }

            var field = detail.Field;
            var range = PdfByteRangeChecker.Check(field.ByteRange, field.Contents.Length, store.Source, revisions);
            if (range.IsValid && range.RevisionIndex == snapshot.Index && !Contains(result, field))
            {
                Append(result, detail);
            }
        }
    }

    /// <summary>Adds current fields without replacing the permissions or locks of an already retained signed snapshot.</summary>
    /// <param name="result">Historical signed fields.</param>
    /// <param name="details">The current fields.</param>
    private static void AddCurrent(List<PdfSignatureDetails> result, ReadOnlySpan<PdfSignatureDetails> details)
    {
        foreach (var detail in details)
        {
            if ((detail.IsSigned && Contains(result, detail.Field)) || (!detail.IsSigned && HasSignedField(result, detail.FieldName)))
            {
                continue;
            }

            Append(result, detail);
        }
    }

    /// <summary>Assigns each retained signature its position in the historical result, including reused fields.</summary>
    /// <param name="result">The retained signatures.</param>
    /// <param name="detail">The signature to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Append(List<PdfSignatureDetails> result, PdfSignatureDetails detail) =>
        result.Add(detail.Field.Index == result.Count ? detail : detail with { Field = detail.Field with { Index = result.Count } });

    /// <summary>Checks a signature's stored signed ranges and contents, not its mutable field name.</summary>
    /// <param name="details">Previously retained signatures.</param>
    /// <param name="field">The signature to find.</param>
    /// <returns>Whether the same signature is already retained.</returns>
    private static bool Contains(List<PdfSignatureDetails> details, PdfSignatureField field)
    {
        foreach (var detail in details)
        {
            if (detail.Field.ByteRange.AsSpan().SequenceEqual(field.ByteRange) && detail.Field.Contents.AsSpan().SequenceEqual(field.Contents))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an unsigned current field replaces an already retained signed field.</summary>
    /// <param name="details">The retained signatures.</param>
    /// <param name="name">The current field's name.</param>
    /// <returns>Whether a historical signed field has this name.</returns>
    private static bool HasSignedField(List<PdfSignatureDetails> details, string name)
    {
        foreach (var detail in details)
        {
            if (detail.IsSigned && string.Equals(detail.FieldName, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
