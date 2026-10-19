// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Changes the elements of an existing structure tree (ISO 32000-2 14.7.2, Table 355): an element's type, its text
/// entries and its place among its siblings. Every edit keeps the tree readable by a conforming reader, so a request that
/// would break the tree is refused with a plain sentence and nothing changes.
/// </summary>
internal static class PdfStructureEditor
{
    /// <summary>The room for a type name's UTF-8 bytes; the longest standard names are 16 characters.</summary>
    private const int MaxTypeBytes = 64;

    /// <summary>The most UTF-8 bytes one UTF-16 character takes.</summary>
    private const int MaxUtf8BytesPerChar = 3;

    /// <summary>Sets an element's standard structure type, its <c>/S</c>.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element, as the reader reports it.</param>
    /// <param name="type">The standard type name, such as <c>P</c> or <c>H2</c>.</param>
    /// <exception cref="ArgumentException">The type is not standard in the element's namespace, or the element is written directly in its parent.</exception>
    internal static void SetType(PdfObjectEdits edits, PdfStructureElement element, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        var editable = Require(edits, element);
        Span<byte> buffer = stackalloc byte[MaxTypeBytes];

        // A name too long for the buffer cannot be a standard type, so it is left empty and refused below.
        var spelling = buffer[..(type.Length <= MaxTypeBytes / MaxUtf8BytesPerChar ? Encoding.UTF8.GetBytes(type, buffer) : 0)];
        var found = PdfStructureTypes.Find(spelling);
        if (found == PdfStructureType.Unknown || !PdfStructureTypes.IsStandardIn(found, PdfStructureTypes.GetNamespaceFlags(NamespaceFor(edits.Store.Version, element))))
        {
            throw new ArgumentException($"'{type}' is not a standard tag type here. Choose one from the list.", nameof(type));
        }

        RequireFit(edits, element, StructureContainment.NameOf(type), element.Parent, true);
        editable.Set(KnownName.S, PdfValue.FromName(edits.Store.Names.Intern(spelling)));
    }

    /// <summary>Gets the standard types an element may take: those its namespace defines that also fit inside its parent and around its children.</summary>
    /// <param name="version">The file's version, such as <c>1.7</c> or <c>2.0</c>.</param>
    /// <param name="element">The element, as the reader reports it.</param>
    /// <returns>The type names. A file that uses the PDF 1.7 namespace only lists every standard type, because the hierarchy rules apply to PDF 2.0 trees (ISO/TS 32005 clause 5.2).</returns>
    internal static ImmutableArray<string> GetChoices(string version, PdfStructureElement element)
    {
        var names = PdfStructureTypes.GetStandardNameArray(NamespaceFor(version, element));
        if (!UsesContainment(version, element))
        {
            return ImmutableCollectionsMarshal.AsImmutableArray(names);
        }

        var fitting = new List<string>(names.Length);
        foreach (var name in names)
        {
            if (string.Equals(name, element.RawType, StringComparison.Ordinal) || FitProblem(element, StructureContainment.NameOf(name), element.Parent, true) is null)
            {
                fitting.Add(name);
            }
        }

        return [.. fitting];
    }

    /// <summary>Sets or clears one of an element's text entries.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element, as the reader reports it.</param>
    /// <param name="field">The entry.</param>
    /// <param name="text">The new text; <see langword="null"/> or empty clears the entry.</param>
    /// <exception cref="ArgumentException">A language is not a valid tag, or the element is written directly in its parent.</exception>
    internal static void SetText(PdfObjectEdits edits, PdfStructureElement element, PdfStructureTextField field, string? text)
    {
        var editable = Require(edits, element);
        var key = KeyOf(field);
        if (string.IsNullOrEmpty(text))
        {
            _ = editable.Remove(key);
            return;
        }

        if (field == PdfStructureTextField.Language && !AccessibilityLanguageTags.IsValid(text))
        {
            throw new ArgumentException($"'{text}' is not a valid language tag. Use a tag such as en-AU.", nameof(text));
        }

        editable.Set(key, PdfValue.FromString(PdfText.Encode(text, editable.GetStringBytes(key), edits.Store.Version)));
    }

    /// <summary>Moves an element among its siblings, which changes the reading order but not the page.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element, as the reader reports it.</param>
    /// <param name="offset">How many places to move it; negative moves it earlier.</param>
    /// <returns><see langword="true"/> when the element moved; <see langword="false"/> when it is already at that end.</returns>
    /// <exception cref="ArgumentException">The element is written directly in its parent.</exception>
    internal static bool Move(PdfObjectEdits edits, PdfStructureElement element, int offset)
    {
        _ = Require(edits, element);
        var parent = EditableParent(edits, element.Parent);
        if (parent.Get(KnownName.K).AsArray() is not { } kids)
        {
            return false;
        }

        var copy = kids.Clone();
        var index = IndexOfElement(copy, element.Id.Number);
        var target = Math.Clamp(index + offset, 0, copy.Count - 1);
        if (index < 0 || target == index)
        {
            return false;
        }

        var value = copy.GetRaw(index);
        copy.RemoveAt(index);
        copy.Insert(target, value);
        parent.Set(KnownName.K, PdfValue.FromArray(copy));
        return true;
    }

    /// <summary>Removes an element that holds no content and lifts its child elements into its parent.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element, as the reader reports it.</param>
    /// <exception cref="InvalidOperationException">The element holds content, an annotation, or an ID other elements may refer to.</exception>
    /// <exception cref="ArgumentException">The element is written directly in its parent.</exception>
    internal static void Remove(PdfObjectEdits edits, PdfStructureElement element)
    {
        _ = Require(edits, element);
        foreach (var kid in element.Kids)
        {
            if (kid.Element is null)
            {
                throw new InvalidOperationException("This tag holds page content. Change its type instead, or mark the content as decoration first.");
            }
        }

        if (element.ElementId is not null)
        {
            throw new InvalidOperationException("Other tags may refer to this tag by its ID, so it was not removed.");
        }

        var parent = EditableParent(edits, element.Parent);
        var parentValue = ParentReference(edits, element.Parent);
        foreach (var kid in element.Kids)
        {
            RequireFit(edits, kid.Element!, StructureContainment.NameOf(kid.Element!) ?? kid.Element!.RawType, element.Parent, false);
            if (edits.Edit(kid.Element!.Id) is not { } editableKid)
            {
                throw new InvalidOperationException("A child tag is written directly inside this one, so it was not removed.");
            }

            editableKid.Set(KnownName.P, parentValue);
        }

        ReplaceKid(edits, parent, element.Id.Number, element.Kids);
        edits.Delete(element.Id);
    }

    /// <summary>Makes an element the last child of the element above it in its group.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element, as the reader reports it.</param>
    /// <param name="newParent">The sibling above it, which takes it in.</param>
    /// <exception cref="ArgumentException">A tag involved is written directly inside its parent.</exception>
    /// <exception cref="InvalidOperationException">The move would put a tag inside itself.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Nest(PdfObjectEdits edits, PdfStructureElement element, PdfStructureElement newParent) =>
        Reparent(edits, element, newParent, -1);

    /// <summary>Moves an element out of its parent, to just after the parent.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element, as the reader reports it.</param>
    /// <exception cref="ArgumentException">A tag involved is written directly inside its parent.</exception>
    /// <exception cref="InvalidOperationException">The element is already at the top.</exception>
    internal static void Unnest(PdfObjectEdits edits, PdfStructureElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.Parent is not { } parent)
        {
            throw new InvalidOperationException("This tag is already at the top, so it cannot move out.");
        }

        Reparent(edits, element, parent.Parent, parent.Id.Number);
    }

    /// <summary>Determines whether the hierarchy rules of ISO/TS 32005 clause 7.2 apply to an element: its types come from the PDF 2.0 namespace.</summary>
    /// <param name="version">The file's version.</param>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when they apply.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool UsesContainment(string version, PdfStructureElement element) =>
        string.Equals(NamespaceFor(version, element), PdfStructureTypes.Pdf20Namespace, StringComparison.Ordinal);

    /// <summary>Gets the namespace whose types an element may take: its own, else the one its file version implies.</summary>
    /// <param name="version">The file's version, such as <c>1.7</c> or <c>2.0</c>.</param>
    /// <param name="element">The element.</param>
    /// <returns>The namespace; <see langword="null"/> never, because a file below PDF 2.0 gets the PDF 1.7 namespace.</returns>
    internal static string NamespaceFor(string version, PdfStructureElement element) =>
        element.Namespace ?? (version.StartsWith('2') ? PdfStructureTypes.Pdf20Namespace : PdfStructureTypes.Pdf17Namespace);

    /// <summary>Gets the key that holds a text entry.</summary>
    /// <param name="field">The entry.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The field is not a text entry.</exception>
    private static PdfName KeyOf(PdfStructureTextField field) => field switch
    {
        PdfStructureTextField.AlternateText => KnownName.Alt,
        PdfStructureTextField.ActualText => KnownName.ActualText,
        PdfStructureTextField.Expansion => KnownName.E,
        PdfStructureTextField.Title => KnownName.T,
        PdfStructureTextField.Language => KnownName.Lang,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Not a text entry."),
    };

    /// <summary>Gets the editable copy of an element's dictionary.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException">The element is written inside its parent.</exception>
    private static PdfDictionary Require(PdfObjectEdits edits, PdfStructureElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return edits.Edit(element.Id) ?? throw new ArgumentException("This tag is written inside its parent, so it cannot be edited on its own.", nameof(element));
    }

    /// <summary>Gets the editable dictionary that holds a parent's <c>/K</c>: the parent, or the structure tree root.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="holder">The parent element, or <see langword="null"/> for the structure tree root.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException">The parent is written inside its own parent.</exception>
    private static PdfDictionary EditableParent(PdfObjectEdits edits, PdfStructureElement? holder) =>
        holder is not null
            ? edits.Edit(holder.Id) ?? throw new ArgumentException("The parent tag is written inside its own parent, so it cannot be edited.", nameof(holder))
            : PdfEditDictionaries.Child(edits, edits.Catalog(), KnownName.StructTreeRoot);

    /// <summary>Gets the value a parent's kids carry in <c>/P</c>.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="holder">The parent element, or <see langword="null"/> for the structure tree root.</param>
    /// <returns>A reference to the parent element or to the structure tree root.</returns>
    /// <exception cref="InvalidOperationException">The structure tree root is written inside the catalog.</exception>
    private static PdfValue ParentReference(PdfObjectEdits edits, PdfStructureElement? holder)
    {
        if (holder is not null)
        {
            return PdfValue.FromReference(holder.Id);
        }

        var root = edits.CatalogForRead.GetRaw(KnownName.StructTreeRoot);
        return root.IsReference ? root : throw new InvalidOperationException("The structure tree root is written inside the catalog, so its children cannot be lifted.");
    }

    /// <summary>Refuses a change that would put a tag where the hierarchy rules do not allow it, in a PDF 2.0 tree.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element the change is about.</param>
    /// <param name="name">The hierarchy table's spelling of the type the element will have.</param>
    /// <param name="parent">The parent it will have, or <see langword="null"/> for the top.</param>
    /// <param name="checkKids">Whether the element's own children must fit inside the new type.</param>
    /// <exception cref="InvalidOperationException">The rules do not allow it.</exception>
    private static void RequireFit(PdfObjectEdits edits, PdfStructureElement element, string name, PdfStructureElement? parent, bool checkKids)
    {
        if (UsesContainment(edits.Store.Version, element) && FitProblem(element, name, parent, checkKids) is { } problem)
        {
            throw new InvalidOperationException(problem);
        }
    }

    /// <summary>Finds why a tag does not fit.</summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The hierarchy table's spelling of its type.</param>
    /// <param name="parent">The parent it would have.</param>
    /// <param name="checkKids">Whether its children must fit inside the type.</param>
    /// <returns>A plain sentence, or <see langword="null"/> when it fits.</returns>
    private static string? FitProblem(PdfStructureElement element, string name, PdfStructureElement? parent, bool checkKids)
    {
        if (parent is not null && StructureContainment.NameOf(parent) is { } parentName && !StructureContainment.Allows(parentName, name))
        {
            return $"A {name} tag cannot go inside a {parentName} tag, so nothing changed.";
        }

        return OccurrenceProblem(element, name, parent) ?? (checkKids ? KidProblem(element, name) : null);
    }

    /// <summary>Finds a child of an element that would not fit inside a type.</summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The hierarchy table's spelling of the type it will have.</param>
    /// <returns>A plain sentence, or <see langword="null"/> when its children fit.</returns>
    private static string? KidProblem(PdfStructureElement element, string name)
    {
        if (!StructureContainment.Knows(name))
        {
            return null;
        }

        foreach (var kid in element.Kids)
        {
            if (kid.Element is { } child && StructureContainment.NameOf(child) is { } childName && !StructureContainment.Allows(name, childName))
            {
                return $"A {name} tag cannot hold a {childName} tag, which is inside this one, so nothing changed.";
            }
        }

        return null;
    }

    /// <summary>Finds whether putting an element in a parent would give the parent a second child of a type the table allows once.</summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The hierarchy table's spelling of the type the element will have.</param>
    /// <param name="parent">The parent it will have, or <see langword="null"/> for the top.</param>
    /// <returns>A plain sentence, or <see langword="null"/> when it fits.</returns>
    private static string? OccurrenceProblem(PdfStructureElement element, string name, PdfStructureElement? parent)
    {
        var parentName = parent is null ? null : StructureContainment.NameOf(parent);
        var flag = StructureContainment.AtMostOnce(parentName, name);
        if (parent is null || flag == OnceChild.None)
        {
            return null;
        }

        foreach (var kid in parent.Kids)
        {
            if (kid.Element is { } other && other.Id.Number != element.Id.Number && StructureContainment.AtMostOnce(parentName, StructureContainment.NameOf(other)) == flag)
            {
                return $"A {parentName} tag can hold only one {name} tag, and this one already has one, so nothing changed.";
            }
        }

        return null;
    }

    /// <summary>Takes an element out of its parent and puts it in another, which keeps its content and its page.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="element">The element.</param>
    /// <param name="newParent">The new parent, or <see langword="null"/> for the structure tree root.</param>
    /// <param name="afterNumber">The object number of the kid to go after, or -1 to go last.</param>
    /// <exception cref="ArgumentException">A tag involved is written directly inside its parent.</exception>
    /// <exception cref="InvalidOperationException">The move would put a tag inside itself.</exception>
    private static void Reparent(PdfObjectEdits edits, PdfStructureElement element, PdfStructureElement? newParent, int afterNumber)
    {
        _ = Require(edits, element);
        for (var up = newParent; up is not null; up = up.Parent)
        {
            if (up.Id.Number == element.Id.Number)
            {
                throw new InvalidOperationException("A tag cannot go inside itself.");
            }
        }

        RequireFit(edits, element, StructureContainment.NameOf(element) ?? element.RawType, newParent, false);

        var parentValue = ParentReference(edits, newParent);
        ReplaceKid(edits, EditableParent(edits, element.Parent), element.Id.Number, []);
        var holder = EditableParent(edits, newParent);
        var kids = holder.Get(KnownName.K).AsArray()?.Clone() ?? new PdfArray(edits.Store);
        if (holder.Get(KnownName.K).AsArray() is null && !holder.GetRaw(KnownName.K).IsNull)
        {
            kids.Add(holder.GetRaw(KnownName.K));
        }

        var after = afterNumber < 0 ? -1 : IndexOfElement(kids, afterNumber);
        if (after < 0)
        {
            kids.Add(PdfValue.FromReference(element.Id));
        }
        else
        {
            kids.Insert(after + 1, PdfValue.FromReference(element.Id));
        }

        holder.Set(KnownName.K, PdfValue.FromArray(kids));
        edits.Edit(element.Id)!.Set(KnownName.P, parentValue);
    }

    /// <summary>Replaces one kid of a parent with the given kids, in place.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="parent">The editable parent.</param>
    /// <param name="number">The object number of the kid to replace.</param>
    /// <param name="replacement">The element kids that take its place.</param>
    /// <exception cref="InvalidOperationException">The parent does not hold the kid.</exception>
    private static void ReplaceKid(PdfObjectEdits edits, PdfDictionary parent, int number, IReadOnlyList<PdfStructureKid> replacement)
    {
        var raw = parent.GetRaw(KnownName.K);
        PdfArray kids;
        var index = 0;
        if (raw.IsReference && raw.AsReference().Number == number)
        {
            kids = new(edits.Store);
        }
        else
        {
            kids = parent.Get(KnownName.K).AsArray()?.Clone() ?? new PdfArray(edits.Store);
            index = IndexOfElement(kids, number);
            if (index < 0)
            {
                throw new InvalidOperationException("The tag was not found in its parent, so it was not removed.");
            }

            kids.RemoveAt(index);
        }

        for (var i = 0; i < replacement.Count; i++)
        {
            kids.Insert(index + i, PdfValue.FromReference(replacement[i].Element!.Id));
        }

        parent.Set(KnownName.K, PdfValue.FromArray(kids));
    }

    /// <summary>Finds the index of a reference to an object in a kid array.</summary>
    /// <param name="kids">The array.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOfElement(PdfArray kids, int number)
    {
        var items = kids.Items;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].IsReference && items[i].AsReference().Number == number)
            {
                return i;
            }
        }

        return -1;
    }
}
