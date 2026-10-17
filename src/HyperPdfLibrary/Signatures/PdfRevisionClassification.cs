// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Classifies changed values without trusting an unsigned replacement's declared role.</summary>
internal static class PdfRevisionClassification
{
    /// <summary>Classifies a changed stream: file structure, metadata, or not recognised.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="stream">The stream.</param>
    /// <returns>The change, or <see langword="null"/>.</returns>
    internal static PdfObjectChange? ClassifyStream(int number, PdfStream stream)
    {
        var type = stream.Dictionary.GetName(KnownName.Type);
        if (type.Is(KnownName.XRef) || type.Is(KnownName.ObjStm))
        {
            return new(number, PdfModificationKinds.None, null, false);
        }

        return type.Is(KnownName.Metadata) ? new(number, PdfModificationKinds.Metadata, null, false) : null;
    }

    /// <summary>Classifies a changed object by what it is.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="number">The object number.</param>
    /// <param name="now">The current value, or null when freed.</param>
    /// <param name="before">The signed value, or null when new.</param>
    /// <returns>The change, a change of kind None for file structure, or <see langword="null"/> when the object is not recognised.</returns>
    internal static PdfObjectChange? Classify(in PdfRevisionComparison state, int number, PdfValue now, PdfValue before)
    {
        if (before.AsDictionary() is { } signedPage && PdfRevisionKinds.KindOfTarget(signedPage, state.Tables.Right) == PdfModificationKinds.Pages)
        {
            // An unsigned replacement cannot change a signed page's role to gain annotation or metadata permissions.
            return new(number, PdfRevisionClassification.SignedPageKind(state, now.AsDictionary(), signedPage), null, false);
        }

        if (number == state.Root)
        {
            return new(number, PdfRevisionContainers.ClassifyCatalog(state, now.AsDictionary(), before.AsDictionary()), null, false);
        }

        if (number == state.Info)
        {
            return new(number, PdfModificationKinds.Metadata, null, false);
        }

        if (PdfRevisionComparer.TryGetOwner(state, number, out var owner) && owner.Kind == PdfModificationKinds.SecurityStore)
        {
            return owner with
            {
                ObjectNumber = number
            };
        }

        var removed = now.IsNull;
        var value = removed ? before : now;
        var names = removed ? state.Tables.Right : state.Tables.Left;
        return value.Kind switch
        {
            PdfKind.Stream => PdfRevisionClassification.ClassifyStream(number, value.AsStream()!),
            PdfKind.Dictionary => PdfRevisionClassification.ClassifyDictionary(state, number, value.AsDictionary()!, names, now.AsDictionary(), before.AsDictionary()),
            PdfKind.Array => PdfRevisionClassification.ClassifyArray(state, number, now.AsArray(), before.AsArray()),
            _ => null,
        };
    }

    /// <summary>Classifies a signed page without trusting the replacement's object type.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="now">The replacement dictionary, or null when removed or replaced with a scalar.</param>
    /// <param name="before">The signed page or page-tree dictionary.</param>
    /// <returns>Page changes, or the annotation changes of an otherwise unchanged page.</returns>
    internal static PdfModificationKinds SignedPageKind(in PdfRevisionComparison state, PdfDictionary? now, PdfDictionary before) =>
        now is not null && now.IsName(KnownName.Type, KnownName.Page) && before.IsName(KnownName.Type, KnownName.Page)
            ? PdfRevisionContainers.ClassifyPage(state, now, before)
            : PdfModificationKinds.Pages;

    /// <summary>Classifies a changed dictionary.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="number">The object number.</param>
    /// <param name="dictionary">The current dictionary, or the signed one when it was removed.</param>
    /// <param name="names">The name table of <paramref name="dictionary"/>.</param>
    /// <param name="now">The current dictionary, or <see langword="null"/>.</param>
    /// <param name="before">The signed dictionary, or <see langword="null"/>.</param>
    /// <returns>The change, or <see langword="null"/>.</returns>
    internal static PdfObjectChange? ClassifyDictionary(in PdfRevisionComparison state, int number, PdfDictionary dictionary, PdfNameTable names, PdfDictionary? now, PdfDictionary? before)
    {
        if (PdfSignatureDictionaries.IsSignatureValue(dictionary, names))
        {
            var kind = PdfSignatureDictionaries.IsDocumentTimestamp(dictionary, names) ? PdfModificationKinds.SecurityStore : PdfModificationKinds.Signature;
            return new(number, kind, null, false);
        }

        if (dictionary.IsName(KnownName.Type, KnownName.Page))
        {
            return new(number, now is null || before is null ? PdfModificationKinds.Pages : PdfRevisionContainers.ClassifyPage(state, now, before), null, false);
        }

        if (dictionary.IsName(KnownName.Type, KnownName.Pages))
        {
            return new(number, PdfModificationKinds.Pages, null, false);
        }

        if (number == state.AcroForm)
        {
            return new(number, PdfRevisionContainers.ClassifyAcroForm(state, now, before), null, false);
        }

        return PdfRevisionKinds.IsAnnotation(dictionary) || PdfRevisionKinds.IsField(dictionary) ? PdfRevisionKinds.ClassifyWidgetOrField(number, dictionary, names) : null;
    }

    /// <summary>Classifies a changed array by the objects added to or removed from it.</summary>
    /// <param name="state">The revisions and operation-local ownership index.</param>
    /// <param name="number">The object number.</param>
    /// <param name="now">The current array, or <see langword="null"/>.</param>
    /// <param name="before">The signed array, or <see langword="null"/>.</param>
    /// <returns>The change, or <see langword="null"/> when no recognised object was added or removed.</returns>
    internal static PdfObjectChange? ClassifyArray(in PdfRevisionComparison state, int number, PdfArray? now, PdfArray? before)
    {
        var kind = PdfRevisionContainers.ReferenceDifference(state, now, before);
        return kind == PdfModificationKinds.None ? null : new(number, kind, null, false);
    }
}
