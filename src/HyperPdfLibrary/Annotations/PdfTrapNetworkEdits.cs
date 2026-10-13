// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// The objects a document's edits changed, added or deleted since it was opened, for telling whether an edit touched
/// what a trap network was made from. An object counts as changed only when its value differs from the file's, so an
/// edit that was undone does not count.
/// </summary>
[DebuggerDisplay("PdfTrapNetworkEdits: {_edited.Count} objects")]
internal sealed class PdfTrapNetworkEdits
{
    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The numbers of the objects that were changed, added or deleted.</summary>
    private readonly HashSet<int> _edited;

    /// <summary>Initializes a new instance of the <see cref="PdfTrapNetworkEdits"/> class from the store's edits now.</summary>
    /// <param name="store">The document's objects.</param>
    internal PdfTrapNetworkEdits(PdfObjectStore store)
    {
        _store = store;
        _edited = [.. StoreEditing.GetEditedNumbers(store)];
    }

    /// <summary>Determines whether an object was edited at all, even back to its first value.</summary>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="true"/> when it was.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Touches(int number) => _edited.Contains(number);

    /// <summary>Determines whether an object holds a value other than the file's: changed, added or deleted.</summary>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal bool Differs(int number) =>
        _edited.Contains(number) && !PdfValueEquality.Equal(StoreTransactions.GetOriginal(_store, number), StoreReading.GetObject(_store, new(number, 0)));

    /// <summary>Determines whether an object that the file holds now holds another value or was deleted.</summary>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="true"/> when it does; <see langword="false"/> for an object added since opening.</returns>
    internal bool Changed(int number)
    {
        if (!_edited.Contains(number))
        {
            return false;
        }

        var original = StoreTransactions.GetOriginal(_store, number);
        return !original.IsNull && !PdfValueEquality.Equal(original, StoreReading.GetObject(_store, new(number, 0)));
    }

    /// <summary>Gets an object as the file holds it.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The value in the file; null for an object added since opening.</returns>
    internal PdfValue Original(int number) => _edited.Contains(number) ? StoreTransactions.GetOriginal(_store, number) : StoreReading.GetObject(_store, new(number, 0));

    /// <summary>Follows a reference to the value the file holds.</summary>
    /// <param name="value">The value as written.</param>
    /// <returns>The value in the file, or the value itself when it is not a reference.</returns>
    internal PdfValue ResolveOriginal(PdfValue value) => value.IsReference ? Original(value.AsReference().Number) : value;
}
