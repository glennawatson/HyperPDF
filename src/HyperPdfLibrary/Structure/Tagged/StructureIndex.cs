// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Finds a structure tree's elements by object number, by dictionary and by element id. Filled while the tree is built,
/// then only read. Elements are found by object number as they are added, which costs an integer hash; the map by
/// dictionary needs an identity hash for every new dictionary, so it is built from the elements when first asked for.
/// </summary>
[DebuggerDisplay("StructureIndex: {Count} elements")]
internal sealed class StructureIndex
{
    /// <summary>The elements in the order they were added.</summary>
    private readonly List<PdfStructureElement> _elements = [];

    /// <summary>The elements written as their own objects, by object number; the first element with a number keeps it.</summary>
    private readonly Dictionary<int, PdfStructureElement> _byNumber = [];

    /// <summary>The elements by <c>/ID</c>; the first element with an id keeps it.</summary>
    private readonly Dictionary<string, PdfStructureElement> _byId = [with(StringComparer.Ordinal)];

    /// <summary>The elements by dictionary, built on first use; the first element with a dictionary keeps it.</summary>
    private Dictionary<PdfDictionary, PdfStructureElement>? _byDictionary;

    /// <summary>Gets the number of elements.</summary>
    internal int Count => _elements.Count;

    /// <summary>Adds an element.</summary>
    /// <param name="element">The element.</param>
    internal void Add(PdfStructureElement element)
    {
        _elements.Add(element);
        _ = Volatile.Read(ref _byDictionary)?.TryAdd(element.Dictionary, element);
        if (element.Id.IsValid)
        {
            _ = _byNumber.TryAdd(element.Id.Number, element);
        }

        if (element.ElementId is { } id)
        {
            _ = _byId.TryAdd(id, element);
        }
    }

    /// <summary>Finds the element built from a dictionary.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfStructureElement? Find(PdfDictionary dictionary) => (Volatile.Read(ref _byDictionary) ?? BuildByDictionary()).GetValueOrDefault(dictionary);

    /// <summary>Finds the element written as the object a reference names.</summary>
    /// <param name="id">The object id.</param>
    /// <returns>The element, or <see langword="null"/> when no element is that object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfStructureElement? Find(PdfObjectId id) => id.IsValid ? _byNumber.GetValueOrDefault(id.Number) : null;

    /// <summary>Finds an element by its <c>/ID</c>.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfStructureElement? FindById(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Builds the map by dictionary from the elements added so far.</summary>
    /// <returns>The map.</returns>
    private Dictionary<PdfDictionary, PdfStructureElement> BuildByDictionary()
    {
        var map = new Dictionary<PdfDictionary, PdfStructureElement>(_elements.Count, ReferenceEqualityComparer.Instance);
        foreach (var element in _elements)
        {
            _ = map.TryAdd(element.Dictionary, element);
        }

        return Interlocked.CompareExchange(ref _byDictionary, map, null) ?? map;
    }
}
