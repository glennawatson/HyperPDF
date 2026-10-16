// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Recognises annotation, field and page objects.</summary>
internal static class PdfRevisionKinds
{
    /// <summary>Classifies an annotation or form field.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="names">The name table of <paramref name="dictionary"/>.</param>
    /// <returns>The change.</returns>
    internal static PdfObjectChange ClassifyWidgetOrField(int number, PdfDictionary dictionary, PdfNameTable names)
    {
        var name = PdfFieldNames.FullName(dictionary);
        return new(number, PdfRevisionKinds.KindOfAnnotationOrField(dictionary, names), name.Length == 0 ? null : name, false);
    }

    /// <summary>Gets the kind of change an annotation or field makes.</summary>
    /// <param name="dictionary">The annotation or field.</param>
    /// <param name="names">The name table of <paramref name="dictionary"/>.</param>
    /// <returns>Annotation for markup; Signature or SecurityStore for signature fields; FormFill for other fields.</returns>
    internal static PdfModificationKinds KindOfAnnotationOrField(PdfDictionary dictionary, PdfNameTable names)
    {
        if (PdfRevisionKinds.IsAnnotation(dictionary) && !dictionary.IsName(KnownName.Subtype, KnownName.Widget))
        {
            return PdfModificationKinds.Annotation;
        }

        if (!PdfFieldNames.FieldType(dictionary).Is(KnownName.Sig))
        {
            return PdfModificationKinds.FormFill;
        }

        var value = dictionary.GetDictionary(KnownName.V);
        return value is not null && PdfSignatureDictionaries.IsDocumentTimestamp(value, names) ? PdfModificationKinds.SecurityStore : PdfModificationKinds.Signature;
    }

    /// <summary>Gets the kind of change adding or removing an object makes.</summary>
    /// <param name="target">The object.</param>
    /// <param name="names">The object's name table.</param>
    /// <returns>The kind, or None when it is not recognised.</returns>
    internal static PdfModificationKinds KindOfTarget(PdfDictionary? target, PdfNameTable names)
    {
        if (target is null)
        {
            return PdfModificationKinds.None;
        }

        if (target.IsName(KnownName.Type, KnownName.Page) || target.IsName(KnownName.Type, KnownName.Pages))
        {
            return PdfModificationKinds.Pages;
        }

        return PdfRevisionKinds.IsAnnotation(target) || PdfRevisionKinds.IsField(target) ? PdfRevisionKinds.KindOfAnnotationOrField(target, names) : PdfModificationKinds.None;
    }

    /// <summary>Determines whether a dictionary is an annotation.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> for /Type /Annot, or a /Subtype with a /Rect.</returns>
    internal static bool IsAnnotation(PdfDictionary dictionary) =>
        dictionary.IsName(KnownName.Type, KnownName.Annot)
        || (dictionary.ContainsKey(KnownName.Subtype) && dictionary.ContainsKey(KnownName.Rect) && !dictionary.IsName(KnownName.Type, KnownName.XObject));

    /// <summary>Determines whether a dictionary is a form field.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> for a field type, or a partial name with kids, a value or a parent.</returns>
    internal static bool IsField(PdfDictionary dictionary) =>
        dictionary.ContainsKey(KnownName.FT)
        || (dictionary.ContainsKey(KnownName.T) && (dictionary.ContainsKey(KnownName.Kids) || dictionary.ContainsKey(KnownName.V) || dictionary.ContainsKey(KnownName.Parent)));

    /// <summary>Determines whether an array references an object.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="true"/> when referenced.</returns>
    internal static bool Contains(PdfArray? array, int number)
    {
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            var item = array.GetRaw(i);
            if (item.IsReference && item.AsReference().Number == number)
            {
                return true;
            }
        }

        return false;
    }
}
