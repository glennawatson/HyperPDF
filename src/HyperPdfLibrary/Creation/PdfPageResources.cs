// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
[DebuggerDisplay("PdfPageResources: {_categories.Length} categories")]
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

    /// <summary>The categories that make up the dictionary, in the order they are written.</summary>
    private readonly Category[] _categories;

    /// <summary>The document the resources belong to.</summary>
    private readonly IPdfObjectSink _sink;

    /// <summary>Initializes a new instance of the <see cref="PdfPageResources"/> class.</summary>
    /// <param name="sink">The document the resources' objects belong to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    public PdfPageResources(IPdfObjectSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _categories =
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
    }

    /// <summary>Gets a value indicating whether nothing has been added.</summary>
    public bool IsEmpty
    {
        get
        {
            foreach (var category in _categories)
            {
                if (category.Names.Count > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

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
        foreach (var category in _categories)
        {
            if (category.Names.Count == 0)
            {
                continue;
            }

            var target = GetOrAddGroup(resources, category.Key);
            foreach (var (id, name) in category.Names)
            {
                target.Set(_sink.Names.Intern(name.Value!), PdfValue.FromReference(id));
            }
        }

        return resources;
    }

    /// <summary>Adds an object to a category.</summary>
    /// <param name="index">The category's index.</param>
    /// <param name="id">The object.</param>
    /// <returns>The name.</returns>
    private PdfResourceName Add(int index, PdfObjectId id)
    {
        var category = _categories[index];
        if (category.Names.TryGetValue(id, out var existing))
        {
            return existing;
        }

        var name = new PdfResourceName(string.Create(CultureInfo.InvariantCulture, $"{category.Prefix}{category.Names.Count + 1}"));
        category.Names.Add(id, name);
        return name;
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

    /// <summary>A group of resources written under one key.</summary>
    /// <param name="Key">The key in the resource dictionary.</param>
    /// <param name="Prefix">The prefix of the names given.</param>
    private sealed record Category(PdfName Key, string Prefix)
    {
        /// <summary>Gets the names given, by object.</summary>
        internal Dictionary<PdfObjectId, PdfResourceName> Names { get; } = [];
    }
}
