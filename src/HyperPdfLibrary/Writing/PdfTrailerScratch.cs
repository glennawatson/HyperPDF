// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>A temporary trailer dictionary, cleared before its storage is returned for reuse.</summary>
internal readonly ref struct PdfTrailerScratch : IDisposable
{
    /// <summary>The largest trailer kept between writes.</summary>
    private const int MaxKeptEntries = 64;

    /// <summary>The owner of the leased trailer storage.</summary>
    private readonly ScratchSlots<PdfDictionary> _slots;

    /// <summary>Initializes a new instance of the <see cref="PdfTrailerScratch"/> struct.</summary>
    /// <param name="dictionary">The leased storage.</param>
    /// <param name="slots">The owner.</param>
    private PdfTrailerScratch(PdfDictionary dictionary, ScratchSlots<PdfDictionary> slots)
    {
        Dictionary = dictionary;
        _slots = slots;
    }

    /// <summary>Gets the trailer being built.</summary>
    internal PdfDictionary Dictionary { get; }

    /// <summary>Clears and returns bounded trailer storage.</summary>
    public void Dispose()
    {
        if (Dictionary is not { } dictionary)
        {
            return;
        }

        var keep = dictionary.Count <= MaxKeptEntries;
        dictionary.Clear();
        if (keep)
        {
            _ = _slots.Offer(dictionary);
        }
    }

    /// <summary>Leases a cleared trailer dictionary.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The lease.</returns>
    internal static PdfTrailerScratch Rent(PdfObjectStore store)
    {
        var slots = store.ScratchPools.Slots<PdfDictionary>();
        return new(slots.Take() ?? new(null), slots);
    }
}
