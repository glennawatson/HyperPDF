// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Finds the editable copy of a dictionary or array that hangs off another dictionary during an edit.</summary>
internal static class PdfEditDictionaries
{
    /// <summary>Gets the editable copy of a child dictionary, making it, and linking it from the parent, when needed.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="parent">The editable parent.</param>
    /// <param name="key">The key.</param>
    /// <returns>The child's working copy. An indirect child stays an object of its own; a direct child is copied into the parent.</returns>
    internal static PdfDictionary Child(PdfObjectEdits edits, PdfDictionary parent, PdfName key)
    {
        var raw = parent.GetRaw(key);
        if (raw.IsReference && edits.Edit(raw.AsReference()) is { } indirect)
        {
            return indirect;
        }

        var copy = raw.AsDictionary()?.Clone() ?? new PdfDictionary(edits.Store);
        parent.Set(key, PdfValue.FromDictionary(copy));
        return copy;
    }

    /// <summary>Gets an editable copy of a child array and links it directly from the parent; an absent array starts empty.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="parent">The editable parent.</param>
    /// <param name="key">The key.</param>
    /// <returns>The copy.</returns>
    internal static PdfArray ChildArray(PdfObjectEdits edits, PdfDictionary parent, PdfName key)
    {
        var copy = parent.GetArray(key)?.Clone() ?? new PdfArray(edits.Store);
        parent.Set(key, PdfValue.FromArray(copy));
        return copy;
    }

    /// <summary>Determines whether an array holds a reference to an object.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool Contains(PdfArray? array, int number) => IndexOf(array, number) >= 0;

    /// <summary>Finds the position of a reference in an array.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The index, or -1.</returns>
    internal static int IndexOf(PdfArray? array, int number)
    {
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            if (array.GetRaw(i).AsReference() is { IsValid: true } id && id.Number == number)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Removes every reference to an object from an array.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="true"/> when something was removed.</returns>
    internal static bool RemoveAll(PdfArray? array, int number)
    {
        var removed = false;
        for (var at = IndexOf(array, number); at >= 0; at = IndexOf(array, number))
        {
            array!.RemoveAt(at);
            removed = true;
        }

        return removed;
    }
}
