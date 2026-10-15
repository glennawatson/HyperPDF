// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// The resource dictionary of a page or form being created (ISO 32000-2 7.8.3). Each font, image, form, shading, pattern,
/// graphics state, colour space and property list is added once by object id and is given a name that is unique in its
/// category; adding the same object again returns the same name. Not thread-safe.
/// </summary>
[DebuggerDisplay("PdfPageResources: empty {IsEmpty}")]
public sealed class PdfPageResources
{
    /// <summary>The index of the font category.</summary>
    private const int FontCategory = 0;

    /// <summary>The index of the image category.</summary>
    private const int ImageCategory = 1;

    /// <summary>The index of the form category.</summary>
    private const int FormCategory = 2;

    /// <summary>The index of the shading category.</summary>
    private const int ShadingCategory = 3;

    /// <summary>The index of the pattern category.</summary>
    private const int PatternCategory = 4;

    /// <summary>The index of the graphics state category.</summary>
    private const int StateCategory = 5;

    /// <summary>The index of the colour space category.</summary>
    private const int ColorSpaceCategory = 6;

    /// <summary>The index of the property list category.</summary>
    private const int PropertiesCategory = 7;

    /// <summary>The number of categories.</summary>
    private const int CategoryCount = 8;

    /// <summary>The most entries searched one by one; past this an index by object is kept.</summary>
    private const int LinearLimit = 16;

    /// <summary>The first size of the entry array.</summary>
    private const int InitialEntries = 4;

    /// <summary>The factor the entry array and the index grow by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The most names of each category kept for use on later pages.</summary>
    private const int CachedNameLimit = 64;

    /// <summary>The common resource names shared without mutable process state.</summary>
    private const int CommonNames = 4;

    /// <summary>The categories that make up the dictionary, in the order they are written. They only name the groups, so every page shares them.</summary>
    private static readonly Category[] Categories =
    [
        new(KnownName.Font, "F"),
        new(KnownName.XObject, "Im"),
        new(KnownName.XObject, "Fm"),
        new(KnownName.Shading, "Sh"),
        new(KnownName.Pattern, "P"),
        new(KnownName.ExtGState, "GS"),
        new(KnownName.ColorSpace, "CS"),
        new(KnownName.Properties, "MC"),
    ];

    /// <summary>The first names of every category, built once and never changed.</summary>
    private static readonly ImmutableArray<PdfResourceName> CommonResourceNames = BuildCommonNames();

    /// <summary>The document the resources belong to.</summary>
    private readonly IPdfObjectSink _sink;

    /// <summary>The names used by this resource set across pages, by category.</summary>
    private PdfResourceName[]?[]? _nameCache;

    /// <summary>The resources added, in the order they were added. The array is made when the first resource is added.</summary>
    private Entry[]? _entries;

    /// <summary>The number of entries used.</summary>
    private int _count;

    /// <summary>The entries by category and object, made once there are more entries than are worth searching one by one.</summary>
    private Dictionary<Key, int>? _index;

    /// <summary>Initializes a new instance of the <see cref="PdfPageResources"/> class.</summary>
    /// <param name="sink">The document the resources' objects belong to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    public PdfPageResources(IPdfObjectSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
    }

    /// <summary>Gets a value indicating whether nothing has been added.</summary>
    public bool IsEmpty => _count == 0;

    /// <summary>Adds a font dictionary.</summary>
    /// <param name="font">The font dictionary's object.</param>
    /// <returns>The name to select it with.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfResourceName AddFont(PdfObjectId font) => Add(FontCategory, font);

    /// <summary>Adds an image XObject.</summary>
    /// <param name="image">The image's object.</param>
    /// <returns>The name to draw it with.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfResourceName AddImage(PdfObjectId image) => Add(ImageCategory, image);

    /// <summary>Adds a form XObject.</summary>
    /// <param name="form">The form's object.</param>
    /// <returns>The name to draw it with.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfResourceName AddForm(PdfObjectId form) => Add(FormCategory, form);

    /// <summary>Adds a shading dictionary.</summary>
    /// <param name="shading">The shading's object.</param>
    /// <returns>The name to paint it with.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfResourceName AddShading(PdfObjectId shading) => Add(ShadingCategory, shading);

    /// <summary>Adds a pattern.</summary>
    /// <param name="pattern">The pattern's object.</param>
    /// <returns>The name to select it with.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfResourceName AddPattern(PdfObjectId pattern) => Add(PatternCategory, pattern);

    /// <summary>Adds an extended graphics state dictionary.</summary>
    /// <param name="state">The graphics state's object.</param>
    /// <returns>The name to select it with.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfResourceName AddGraphicsState(PdfObjectId state) => Add(StateCategory, state);

    /// <summary>Adds a colour space that is an array, such as an ICC based space.</summary>
    /// <param name="colorSpace">The object holding the colour space array.</param>
    /// <returns>The name to select it with.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfResourceName AddColorSpace(PdfObjectId colorSpace) => Add(ColorSpaceCategory, colorSpace);

    /// <summary>Adds a property list that marked content refers to, such as an optional content group.</summary>
    /// <param name="properties">The property list's object.</param>
    /// <returns>The name to refer to it with.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfResourceName AddProperties(PdfObjectId properties) => Add(PropertiesCategory, properties);

    /// <summary>Builds the resource dictionary.</summary>
    /// <returns>A new dictionary that holds one subdictionary for each category that has an entry.</returns>
    public PdfDictionary ToDictionary()
    {
        var resources = new PdfDictionary(_sink.Owner);
        if (_entries is not { } entries)
        {
            return resources;
        }

        for (var i = 0; i < CategoryCount; i++)
        {
            PdfDictionary? target = null;
            for (var e = 0; e < _count; e++)
            {
                if (entries[e].Category != i)
                {
                    continue;
                }

                target ??= GetOrAddGroup(resources, Categories[i].Key);
                target.Set(_sink.Names.Intern(entries[e].Name.Utf8), PdfValue.FromReference(entries[e].Id));
            }
        }

        return resources;
    }

    /// <summary>Forgets every resource so the object can be used for another page. Names given before are given again.</summary>
    internal void Clear()
    {
        _count = 0;
        _index?.Clear();
    }

    /// <summary>Builds the common resource names for every category.</summary>
    /// <returns>The names in category order.</returns>
    private static ImmutableArray<PdfResourceName> BuildCommonNames()
    {
        var names = ImmutableArray.CreateBuilder<PdfResourceName>(CategoryCount * CommonNames);
        for (var category = 0; category < CategoryCount; category++)
        {
            for (var number = 1; number <= CommonNames; number++)
            {
                names.Add(new(string.Create(CultureInfo.InvariantCulture, $"{Categories[category].Prefix}{number}")));
            }
        }

        return names.MoveToImmutable();
    }

    /// <summary>Gets the name of a resource, making it when it is new.</summary>
    /// <param name="category">The category.</param>
    /// <param name="number">The number within the category, from one.</param>
    /// <returns>The name, such as <c>F1</c>.</returns>
    private PdfResourceName GetName(int category, int number)
    {
        if (number <= CommonNames)
        {
            return CommonResourceNames[(category * CommonNames) + number - 1];
        }

        if (number > CachedNameLimit)
        {
            return new(string.Create(CultureInfo.InvariantCulture, $"{Categories[category].Prefix}{number}"));
        }

        var index = number - CommonNames - 1;
        var cache = _nameCache ??= new PdfResourceName[]?[CategoryCount];
        var names = cache[category];
        if (names is null)
        {
            names = new PdfResourceName[Math.Max(index + 1, InitialEntries)];
            cache[category] = names;
        }
        else if (index >= names.Length)
        {
            Array.Resize(ref names, Math.Min(CachedNameLimit - CommonNames, Math.Max(index + 1, names.Length * GrowthFactor)));
            cache[category] = names;
        }

        ref var name = ref names[index];
        if (name.Value is null)
        {
            name = new(string.Create(CultureInfo.InvariantCulture, $"{Categories[category].Prefix}{number}"));
        }

        return name;
    }

    /// <summary>Adds an object to a category.</summary>
    /// <param name="category">The category's index.</param>
    /// <param name="id">The object.</param>
    /// <returns>The name.</returns>
    private PdfResourceName Add(int category, PdfObjectId id)
    {
        if (Find(category, id) is var found and >= 0)
        {
            return _entries![found].Name;
        }

        var number = 1;
        for (var e = 0; e < _count; e++)
        {
            number += _entries![e].Category == category ? 1 : 0;
        }

        var name = GetName(category, number);
        Append(new(id, name, category));
        return name;
    }

    /// <summary>Finds the entry of an object in a category.</summary>
    /// <param name="category">The category's index.</param>
    /// <param name="id">The object.</param>
    /// <returns>The index of the entry, or -1.</returns>
    private int Find(int category, PdfObjectId id)
    {
        if (_index is { } index)
        {
            return index.TryGetValue(new(category, id), out var at) ? at : -1;
        }

        for (var e = 0; e < _count; e++)
        {
            if (_entries![e].Category == category && _entries[e].Id == id)
            {
                return e;
            }
        }

        return -1;
    }

    /// <summary>Adds an entry, making the entry array and the index when they are needed.</summary>
    /// <param name="entry">The entry.</param>
    private void Append(in Entry entry)
    {
        if (_entries is null)
        {
            _entries = new Entry[InitialEntries];
        }
        else if (_count == _entries.Length)
        {
            Array.Resize(ref _entries, _count * GrowthFactor);
        }

        _entries[_count] = entry;
        _index?.Add(new(entry.Category, entry.Id), _count);
        _count++;
        if (_index is null && _count > LinearLimit)
        {
            BuildIndex();
        }
    }

    /// <summary>Makes the index of the entries added so far.</summary>
    private void BuildIndex()
    {
        _index = [with(_count * GrowthFactor)];
        for (var e = 0; e < _count; e++)
        {
            _index.Add(new(_entries![e].Category, _entries[e].Id), e);
        }
    }

    /// <summary>Gets the subdictionary under a key, adding it when it is missing.</summary>
    /// <param name="resources">The resource dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The subdictionary.</returns>
    private PdfDictionary GetOrAddGroup(PdfDictionary resources, PdfName key)
    {
        if (resources.GetRaw(key).AsDictionary() is { } existing)
        {
            return existing;
        }

        var group = new PdfDictionary(_sink.Owner);
        resources.Set(key, PdfValue.FromDictionary(group));
        return group;
    }

    /// <summary>One resource added to the page.</summary>
    /// <param name="Id">The resource's object.</param>
    /// <param name="Name">The name it was given.</param>
    /// <param name="Category">The index of its category.</param>
    private readonly record struct Entry(PdfObjectId Id, PdfResourceName Name, int Category);

    /// <summary>The place of a resource in the index.</summary>
    /// <param name="Category">The index of its category.</param>
    /// <param name="Id">The resource's object.</param>
    private readonly record struct Key(int Category, PdfObjectId Id);

    /// <summary>A group of resources written under one key.</summary>
    /// <param name="Key">The key in the resource dictionary.</param>
    /// <param name="Prefix">The prefix of the names given.</param>
    private sealed record Category(PdfName Key, string Prefix);
}
