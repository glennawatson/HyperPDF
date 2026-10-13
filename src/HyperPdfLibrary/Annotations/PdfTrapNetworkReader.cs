// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>Finds trap network annotations (ISO 32000-2 §12.5.6.21) in an annotation array.</summary>
internal static class PdfTrapNetworkReader
{
    /// <summary>Determines whether an annotation is a trap network.</summary>
    /// <param name="annotation">The annotation, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when its <c>/Subtype</c> is <c>/TrapNet</c>.</returns>
    internal static bool IsNetwork(PdfDictionary? annotation) => annotation is not null && annotation.IsName(KnownName.Subtype, KnownName.TrapNet);

    /// <summary>Gets an entry of an annotation array as a dictionary.</summary>
    /// <param name="store">The objects the entries resolve in.</param>
    /// <param name="annotations">The array.</param>
    /// <param name="index">The index.</param>
    /// <returns>The annotation, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfDictionary? Get(PdfObjectStore store, PdfArray annotations, int index) => StoreReading.Resolve(store, annotations.GetRaw(index)).AsDictionary();

    /// <summary>Counts the trap network annotations in an array.</summary>
    /// <param name="store">The objects the entries resolve in.</param>
    /// <param name="annotations">The <c>/Annots</c> array.</param>
    /// <param name="index">The index of the last one, or -1 when there is none.</param>
    /// <returns>The number found.</returns>
    internal static int Find(PdfObjectStore store, PdfArray annotations, out int index)
    {
        index = -1;
        var count = 0;
        for (var i = 0; i < annotations.Count; i++)
        {
            if (!IsNetwork(Get(store, annotations, i)))
            {
                continue;
            }

            index = i;
            count++;
        }

        return count;
    }

    /// <summary>Lists the appearance states of an array's annotations other than trap networks, in array order.</summary>
    /// <param name="store">The objects the entries resolve in.</param>
    /// <param name="annotations">The array.</param>
    /// <param name="states">Receives each annotation's <c>/AS</c>, or no name for one without it.</param>
    internal static void States(PdfObjectStore store, PdfArray annotations, List<PdfName> states)
    {
        foreach (var item in annotations.Items)
        {
            if (StoreReading.Resolve(store, item).AsDictionary() is { } annotation && !IsNetwork(annotation))
            {
                states.Add(annotation.GetName(KnownName.AS));
            }
        }
    }

    /// <summary>Copies an annotation array without its trap network annotations.</summary>
    /// <param name="store">The objects the entries resolve in.</param>
    /// <param name="annotations">The array.</param>
    /// <returns>The copy, which has the same owner.</returns>
    internal static PdfArray Without(PdfObjectStore store, PdfArray annotations)
    {
        var kept = new PdfArray(annotations.Owner, annotations.Count);
        foreach (var item in annotations.Items)
        {
            if (!IsNetwork(StoreReading.Resolve(store, item).AsDictionary()))
            {
                kept.Add(item);
            }
        }

        return kept;
    }

    /// <summary>Copies an annotation array with one entry moved to the end and written as a new value.</summary>
    /// <param name="annotations">The array.</param>
    /// <param name="index">The entry to move.</param>
    /// <param name="entry">The value written at the end in its place.</param>
    /// <returns>The copy, which has the same owner.</returns>
    internal static PdfArray MovedLast(PdfArray annotations, int index, PdfValue entry)
    {
        var moved = new PdfArray(annotations.Owner, annotations.Count);
        for (var i = 0; i < annotations.Count; i++)
        {
            if (i != index)
            {
                moved.Add(annotations.GetRaw(i));
            }
        }

        moved.Add(entry);
        return moved;
    }
}
