// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>Finds signature fields in the interactive form and picks names for new ones.</summary>
internal static class PdfSigningFields
{
    /// <summary>The most fields visited, so a damaged form cannot make signing run on without end.</summary>
    private const int MaxFields = 1 << 16;

    /// <summary>The prefix of generated field names.</summary>
    private const string NamePrefix = "Signature";

    /// <summary>The name of the first signature field, which a document with no fields gets.</summary>
    private const string FirstSignatureName = "Signature1";

    /// <summary>Finds a signature field by its fully qualified name.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="name">The name.</param>
    /// <returns>The field, or <see langword="null"/> when there is no signature field of that name.</returns>
    internal static PdfSigningField? Find(PdfObjectStore store, string name)
    {
        if (!HasFields(store))
        {
            return null;
        }

        HashSet<string> names = [with(StringComparer.Ordinal)];
        foreach (var field in Read(store, names))
        {
            if (string.Equals(field.Name, name, StringComparison.Ordinal))
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Picks a name for a new signature field that no other field has.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The name, such as <c>Signature1</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string NextName(PdfObjectStore store) => NextName(store, NamePrefix);

    /// <summary>Picks a name for a new field that no other field has.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="prefix">The text the name starts with.</param>
    /// <returns>The name, such as <c>DocTimeStamp1</c>.</returns>
    internal static string NextName(PdfObjectStore store, string prefix)
    {
        if (!HasFields(store))
        {
            return string.Equals(prefix, NamePrefix, StringComparison.Ordinal) ? FirstSignatureName : string.Create(CultureInfo.InvariantCulture, $"{prefix}{1}");
        }

        HashSet<string> names = [with(StringComparer.Ordinal)];
        _ = Read(store, names);

        // With n names taken, at least one of the numbers 1 to n + 1 is free.
        for (var number = 1; number <= names.Count; number++)
        {
            var name = string.Create(CultureInfo.InvariantCulture, $"{prefix}{number}");
            if (!names.Contains(name))
            {
                return name;
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"{prefix}{names.Count + 1}");
    }

    /// <summary>Determines whether a field name is already used.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="name">The fully qualified name.</param>
    /// <returns><see langword="true"/> when a field has it.</returns>
    internal static bool IsUsed(PdfObjectStore store, string name)
    {
        if (!HasFields(store))
        {
            return false;
        }

        HashSet<string> names = [with(StringComparer.Ordinal)];
        _ = Read(store, names);
        return names.Contains(name);
    }

    /// <summary>Gets the page a field's widget is on, from its /P entry or by looking for it in each page's annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="field">The field.</param>
    /// <returns>The zero-based page index, or -1 when the widget is on no page.</returns>
    internal static int PageOf(PdfDocument document, PdfSigningField field)
    {
        var pageReference = field.Widget?.GetRaw(KnownName.P).AsReference() ?? default;
        for (var index = 0; index < document.PageCount; index++)
        {
            var page = PdfDocumentPages.GetPage(document, index);
            if ((pageReference.IsValid && page.Id == pageReference) || (field.WidgetId.IsValid && Lists(page.Dictionary.GetArray(KnownName.Annots), field.WidgetId)))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Determines whether the document has an interactive form with a list of fields.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns><see langword="true"/> when there is a /Fields array to walk.</returns>
    private static bool HasFields(PdfObjectStore store) => store.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields) is not null;

    /// <summary>Walks the form's fields, collecting every name and the signature fields.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="names">Receives every field's fully qualified name.</param>
    /// <returns>The signature fields.</returns>
    private static List<PdfSigningField> Read(PdfObjectStore store, HashSet<string> names)
    {
        var signatures = new List<PdfSigningField>();
        if (store.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields) is { } fields)
        {
            var state = new WalkState(names, signatures);
            Walk(fields, string.Empty, default, state, 0);
        }

        return signatures;
    }

    /// <summary>Visits the fields of an array.</summary>
    /// <param name="fields">The array.</param>
    /// <param name="prefix">The fully qualified name of the parent, or empty at the top.</param>
    /// <param name="inheritedType">The field type the parent passes down.</param>
    /// <param name="state">What the walk collects.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void Walk(PdfArray fields, string prefix, PdfName inheritedType, WalkState state, int depth)
    {
        for (var i = 0; i < fields.Count && state.Names.Count < MaxFields && depth < PdfLimits.MaxNesting; i++)
        {
            if (fields.GetDictionary(i) is not { } field)
            {
                continue;
            }

            Visit(field, fields.GetRaw(i).AsReference(), prefix, inheritedType, state, depth);
        }
    }

    /// <summary>Visits one field: records its name and either descends into its kids or, for a signature field, describes it.</summary>
    /// <param name="field">The field's dictionary.</param>
    /// <param name="id">The field's object id.</param>
    /// <param name="prefix">The fully qualified name of the parent.</param>
    /// <param name="inheritedType">The field type the parent passes down.</param>
    /// <param name="state">What the walk collects.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void Visit(PdfDictionary field, PdfObjectId id, string prefix, PdfName inheritedType, WalkState state, int depth)
    {
        var partial = field.ContainsKey(KnownName.T) ? PdfText.Decode(field.GetStringBytes(KnownName.T)) : string.Empty;
        var name = prefix.Length > 0 && partial.Length > 0 ? $"{prefix}.{partial}" : prefix + partial;
        var type = field.GetName(KnownName.FT).IsNone ? inheritedType : field.GetName(KnownName.FT);
        _ = state.Names.Add(name);
        if (field.GetArray(KnownName.Kids) is { } kids && HasFieldKid(kids))
        {
            Walk(kids, name, type, state, depth + 1);
        }
        else if (type.Is(KnownName.Sig))
        {
            state.Signatures.Add(Describe(id, field, name));
        }
    }

    /// <summary>Determines whether any kid of a field is a field itself rather than a widget.</summary>
    /// <param name="kids">The /Kids array.</param>
    /// <returns><see langword="true"/> when one has a partial name.</returns>
    private static bool HasFieldKid(PdfArray kids)
    {
        for (var i = 0; i < kids.Count; i++)
        {
            if (kids.GetDictionary(i)?.ContainsKey(KnownName.T) == true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Describes a terminal signature field and finds its widget.</summary>
    /// <param name="id">The field's object id.</param>
    /// <param name="field">The field's dictionary.</param>
    /// <param name="name">The fully qualified name.</param>
    /// <returns>The description.</returns>
    private static PdfSigningField Describe(PdfObjectId id, PdfDictionary field, string name) =>
        field.GetArray(KnownName.Kids) is { Count: > 0 } kids && kids.GetDictionary(0) is { } widget
            ? new(id, field, name, kids.GetRaw(0).AsReference(), widget)
            : new(id, field, name, id, field);

    /// <summary>Determines whether an annotation array lists an object.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="id">The object.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Lists(PdfArray? array, PdfObjectId id)
    {
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            if (array.GetRaw(i).AsReference() == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What a walk of the form collects.</summary>
    /// <param name="Names">Every field's fully qualified name.</param>
    /// <param name="Signatures">The signature fields.</param>
    private sealed record WalkState(HashSet<string> Names, List<PdfSigningField> Signatures);
}
