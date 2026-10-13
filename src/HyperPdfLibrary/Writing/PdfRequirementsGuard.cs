// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Requirements;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Keeps the catalog's <c>/Requirements</c> true when an edited document is saved (ISO 32000-2 §12.11.3). A requirement
/// says the document uses or needs a feature, so one whose feature the edits removed (the last signature, attachment or
/// portfolio) is removed before writing and listed in <see cref="PdfSaveReport.Requirements"/>. Every other requirement is
/// written as it was read, including ones this library does not know. A save with no edits changes nothing.
/// </summary>
internal static class PdfRequirementsGuard
{
    /// <summary>The undo label of the change.</summary>
    private const string Label = "Update document requirements";

    /// <summary>Removes the requirements whose feature the edits took out of the file.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="report">Receives what was removed, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled; nothing was changed.</exception>
    internal static void Apply(PdfObjectStore store, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        report?.SetRequirements([]);
        if (!StoreEditing.HasChanges(store) || store.Catalog.GetArray(store.Names.Intern("Requirements"u8)) is not { } array)
        {
            return;
        }

        var kept = new List<PdfValue>(array.Count);
        var removed = new List<PdfRequirementChange>();
        for (var i = 0; i < array.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (RemovalOf(store, array.Get(i).AsDictionary()) is { } change)
            {
                removed.Add(change);
            }
            else
            {
                kept.Add(array.GetRaw(i));
            }
        }

        if (removed.Count > 0 && Replace(store, kept))
        {
            report?.SetRequirements([.. removed]);
        }
    }

    /// <summary>Works out whether a requirement dictionary names a feature that is gone.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="requirement">The entry, or <see langword="null"/> when it is not a dictionary.</param>
    /// <returns>The change to report, or <see langword="null"/> when the requirement stays.</returns>
    private static PdfRequirementChange? RemovalOf(PdfObjectStore store, PdfDictionary? requirement)
    {
        if (requirement?.Get(store.Names.Intern("S"u8)).SpellName(store) is not { } type || !PdfRequirementFeatures.IsAbsent(store, type))
        {
            return null;
        }

        var title = PdfRequirementCapabilities.HyperPdf.Find(type)?.Title ?? type;
        return new(type, $"The file listed \"{title}\" as something it needs. The edits left nothing in the file that uses it, so the requirement was removed.");
    }

    /// <summary>Writes the catalog with the requirements that stay; the entry goes when none do.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="kept">The requirements that stay, as they were stored.</param>
    /// <returns><see langword="true"/> when the catalog was changed.</returns>
    private static bool Replace(PdfObjectStore store, List<PdfValue> kept)
    {
        var root = store.Trailer.GetRaw(KnownName.Root);
        if (!root.IsReference)
        {
            return false;
        }

        var key = store.Names.Intern("Requirements"u8);
        var catalog = store.Catalog.Clone();
        if (kept.Count == 0)
        {
            _ = catalog.Remove(key);
        }
        else
        {
            PdfArray array = new(store, [.. kept]);
            catalog.Set(key, PdfValue.FromArray(array));
        }

        var transaction = PdfGuardTransactions.JoinOrBegin(store, Label, out var owned);
        try
        {
            transaction.Replace(root.AsReference(), PdfValue.FromDictionary(catalog));
        }
        catch
        {
            if (owned)
            {
                transaction.Rollback();
            }

            throw;
        }

        if (owned)
        {
            transaction.Commit();
        }

        return true;
    }
}
