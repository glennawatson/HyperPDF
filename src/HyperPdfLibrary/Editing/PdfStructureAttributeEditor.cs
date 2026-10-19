// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Editing;

/// <summary>Changes the attribute objects of a structure element (ISO 32000-2 14.7.6): the <c>/Scope</c> of a table header cell and the <c>/ListNumbering</c> of a list.</summary>
internal static class PdfStructureAttributeEditor
{
    /// <summary>The attribute owner of table attributes.</summary>
    private const string TableOwner = "Table";

    /// <summary>The placement names ISO 32000-2 Table 380 defines.</summary>
    private static readonly string[] Placements = ["Block", "Inline", "Before", "Start", "End"];

    /// <summary>The list numbering names ISO 32000-2 Table 383 defines.</summary>
    private static readonly string[] ListNumberings =
    [
        "None", "Unordered", "Ordered", "Disc", "Circle", "Square", "Decimal", "UpperRoman", "LowerRoman", "UpperAlpha", "LowerAlpha", "Description",
    ];

    /// <summary>Sets or clears the <c>/Scope</c> of a table header cell, which says which cells the header labels (ISO 32000-2 14.8.5.7, Table 384).</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The header cell, as the reader reports it.</param>
    /// <param name="scope">The new scope; <see cref="PdfTableScope.None"/> clears it.</param>
    /// <exception cref="ArgumentException">The element is not a TH, the scope is not known, or the element is written directly in its parent.</exception>
    internal static void SetTableScope(PdfObjectEdits edits, PdfStructureElement element, PdfTableScope scope)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!string.Equals(element.MappedType, "TH", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only a table header cell (TH) has a scope.", nameof(element));
        }

        var value = scope switch
        {
            PdfTableScope.None => null,
            PdfTableScope.Row => "Row",
            PdfTableScope.Column => "Column",
            PdfTableScope.Both => "Both",
            _ => throw new ArgumentException("That is not a table scope.", nameof(scope)),
        };

        SetAttribute(edits, element, TableOwner, "Scope", NameValue(edits, value));
    }

    /// <summary>Sets or clears the <c>/ListNumbering</c> of a list, which says how its items are labelled (ISO 32000-2 14.8.5.6, Table 383).</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The list, as the reader reports it.</param>
    /// <param name="numbering">One of the names of Table 383, or <see langword="null"/> or empty to clear it.</param>
    /// <exception cref="ArgumentException">The element is not an L, the name is not one of Table 383, or the element is written directly in its parent.</exception>
    internal static void SetListNumbering(PdfObjectEdits edits, PdfStructureElement element, string? numbering)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!string.Equals(element.MappedType, "L", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only a list (L) has a numbering.", nameof(element));
        }

        if (!string.IsNullOrEmpty(numbering) && Array.IndexOf(ListNumberings, numbering) < 0)
        {
            throw new ArgumentException($"'{numbering}' is not a list numbering. Choose one of: {string.Join(", ", ListNumberings)}.", nameof(numbering));
        }

        SetAttribute(edits, element, "List", "ListNumbering", NameValue(edits, string.IsNullOrEmpty(numbering) ? null : numbering));
    }

    /// <summary>Sets or clears the <c>/Placement</c> of an element, which says how it sits in the flow of content (ISO 32000-2 14.8.5.4, Table 380).</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element, as the reader reports it.</param>
    /// <param name="placement">One of the names of Table 380, or <see langword="null"/> or empty to clear it.</param>
    /// <exception cref="ArgumentException">The name is not one of Table 380, or the element is written directly in its parent.</exception>
    internal static void SetPlacement(PdfObjectEdits edits, PdfStructureElement element, string? placement)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!string.IsNullOrEmpty(placement) && Array.IndexOf(Placements, placement) < 0)
        {
            throw new ArgumentException($"'{placement}' is not a placement. Choose one of: {string.Join(", ", Placements)}.", nameof(placement));
        }

        SetAttribute(edits, element, "Layout", "Placement", NameValue(edits, string.IsNullOrEmpty(placement) ? null : placement));
    }

    /// <summary>Sets or clears the <c>/RowSpan</c> and <c>/ColSpan</c> of a table cell: how many rows and columns it covers (ISO 32000-2 14.8.5.7, Table 384).</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The cell, as the reader reports it.</param>
    /// <param name="rows">How many rows the cell covers; 1 clears the entry, because 1 is the default.</param>
    /// <param name="columns">How many columns the cell covers; 1 clears the entry.</param>
    /// <exception cref="ArgumentException">The element is not a TH or TD, a span is below 1, or the element is written directly in its parent.</exception>
    internal static void SetCellSpans(PdfObjectEdits edits, PdfStructureElement element, int rows, int columns)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.MappedType is not ("TH" or "TD"))
        {
            throw new ArgumentException("Only a table cell (TH or TD) has row and column spans.", nameof(element));
        }

        if (rows < 1 || columns < 1)
        {
            throw new ArgumentException("A cell covers at least one row and one column.", nameof(rows));
        }

        SetAttribute(edits, element, TableOwner, "RowSpan", rows == 1 ? null : PdfValue.FromInteger(rows));
        SetAttribute(edits, element, TableOwner, "ColSpan", columns == 1 ? null : PdfValue.FromInteger(columns));
    }

    /// <summary>Makes a name value.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="name">The name's text, or <see langword="null"/> for no value.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private static PdfValue? NameValue(PdfObjectEdits edits, string? name) => name is null ? null : PdfValue.FromName(edits.Store.Names.Intern(name));

    /// <summary>Sets or clears an entry in an element's attribute object for an owner, making the object when needed.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element.</param>
    /// <param name="owner">The attribute owner, such as <c>Table</c>.</param>
    /// <param name="key">The entry's key.</param>
    /// <param name="value">The name to set, or <see langword="null"/> to clear the entry.</param>
    /// <exception cref="ArgumentException">The element is written directly in its parent.</exception>
    private static void SetAttribute(PdfObjectEdits edits, PdfStructureElement element, string owner, string key, PdfValue? value)
    {
        var editable = edits.Edit(element.Id) ?? throw new ArgumentException("This tag is written inside its parent, so it cannot be edited on its own.", nameof(element));
        var names = edits.Store.Names;
        var target = new AttributeTarget(names.Intern(owner), names.Intern(key));
        switch (editable.Get(KnownName.A))
        {
            case { Kind: PdfKind.Dictionary } single when single.AsDictionary() is { } dictionary:
            {
                UpdateSingle(edits, editable, dictionary, target, value);
                break;
            }

            case { Kind: PdfKind.Array } list when list.AsArray() is { } array:
            {
                UpdateList(edits, editable, array, target, value);
                break;
            }

            default:
            {
                AddFirst(edits, editable, target, value);
                break;
            }
        }
    }

    /// <summary>Updates an element whose <c>/A</c> is one attribute object.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="editable">The element's editable dictionary.</param>
    /// <param name="attributes">The attribute object.</param>
    /// <param name="target">The owner and key to change.</param>
    /// <param name="value">The new value, or <see langword="null"/> to clear it.</param>
    private static void UpdateSingle(PdfObjectEdits edits, PdfDictionary editable, PdfDictionary attributes, AttributeTarget target, PdfValue? value)
    {
        if (attributes.GetName(KnownName.O) == target.Owner)
        {
            editable.Set(KnownName.A, PdfValue.FromDictionary(WithValue(attributes, target.Key, value)));
            return;
        }

        if (value is null)
        {
            return;
        }

        var array = new PdfArray(edits.Store);
        array.Add(PdfValue.FromDictionary(attributes));
        array.Add(PdfValue.FromDictionary(NewObject(edits, target, value.Value)));
        editable.Set(KnownName.A, PdfValue.FromArray(array));
    }

    /// <summary>Updates an element whose <c>/A</c> is an array of attribute objects.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="editable">The element's editable dictionary.</param>
    /// <param name="array">The attribute objects.</param>
    /// <param name="target">The owner and key to change.</param>
    /// <param name="value">The new value, or <see langword="null"/> to clear it.</param>
    private static void UpdateList(PdfObjectEdits edits, PdfDictionary editable, PdfArray array, AttributeTarget target, PdfValue? value)
    {
        var copy = array.Clone();
        var index = IndexOfOwner(copy, target.Owner);
        if (index >= 0)
        {
            copy.Insert(index, PdfValue.FromDictionary(WithValue(copy.GetDictionary(index)!, target.Key, value)));
            copy.RemoveAt(index + 1);
        }
        else if (value is { } added)
        {
            copy.Add(PdfValue.FromDictionary(NewObject(edits, target, added)));
        }
        else
        {
            return;
        }

        editable.Set(KnownName.A, PdfValue.FromArray(copy));
    }

    /// <summary>Finds the first attribute object of an owner in an array.</summary>
    /// <param name="array">The attribute objects.</param>
    /// <param name="owner">The owner name.</param>
    /// <returns>Its position, or -1 when there is none.</returns>
    private static int IndexOfOwner(PdfArray array, PdfName owner)
    {
        for (var i = 0; i < array.Count; i++)
        {
            if (array.GetDictionary(i)?.GetName(KnownName.O) == owner)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Gives an element with no attributes its first attribute object.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="editable">The element's editable dictionary.</param>
    /// <param name="target">The owner and key to change.</param>
    /// <param name="value">The new value, or <see langword="null"/> when there is nothing to add.</param>
    private static void AddFirst(PdfObjectEdits edits, PdfDictionary editable, AttributeTarget target, PdfValue? value)
    {
        if (value is { } first)
        {
            editable.Set(KnownName.A, PdfValue.FromDictionary(NewObject(edits, target, first)));
        }
    }

    /// <summary>Copies an attribute object with one entry set or removed.</summary>
    /// <param name="attributes">The object.</param>
    /// <param name="key">The entry's key.</param>
    /// <param name="value">The new value, or <see langword="null"/> to remove the entry.</param>
    /// <returns>The copy.</returns>
    private static PdfDictionary WithValue(PdfDictionary attributes, PdfName key, PdfValue? value)
    {
        var copy = attributes.Clone();
        if (value is { } set)
        {
            copy.Set(key, set);
        }
        else
        {
            _ = copy.Remove(key);
        }

        return copy;
    }

    /// <summary>Makes an attribute object with one entry.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="target">The owner and key.</param>
    /// <param name="value">The name to set.</param>
    /// <returns>The object.</returns>
    private static PdfDictionary NewObject(PdfObjectEdits edits, AttributeTarget target, PdfValue value)
    {
        var attributes = new PdfDictionary(edits.Store);
        attributes.Set(KnownName.O, PdfValue.FromName(target.Owner));
        attributes.Set(target.Key, value);
        return attributes;
    }

    /// <summary>The owner and key of an attribute entry.</summary>
    /// <param name="Owner">The attribute owner's name.</param>
    /// <param name="Key">The entry's key.</param>
    private readonly record struct AttributeTarget(PdfName Owner, PdfName Key);
}
