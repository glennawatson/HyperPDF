// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// A page's resources with image XObjects added to them. The page's own dictionary and any shared resource dictionary are
/// never changed: the additions go into a copy that the page then names directly.
/// </summary>
internal sealed class PageResourceEdit
{
    /// <summary>The start of the names given to externalised inline images.</summary>
    private const string NamePrefix = "HyperInlineImage";

    /// <summary>The page's resources as they were, or <see langword="null"/> when it has none.</summary>
    private readonly PdfDictionary? _original;

    /// <summary>The objects the resources belong to.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The copy of the resources, made when the first image is added.</summary>
    private PdfDictionary? _resources;

    /// <summary>The copy of the image XObject dictionary, made when the first image is added.</summary>
    private PdfDictionary? _images;

    /// <summary>The number tried first for the next name.</summary>
    private int _nextNumber = 1;

    /// <summary>Initializes a new instance of the <see cref="PageResourceEdit"/> class.</summary>
    /// <param name="store">The objects the resources belong to.</param>
    /// <param name="original">The page's resources, own or inherited, or <see langword="null"/>.</param>
    internal PageResourceEdit(PdfObjectStore store, PdfDictionary? original)
    {
        _store = store;
        _original = original;
    }

    /// <summary>Gets the copy of the resources to write into the page, or <see langword="null"/> when no image was added.</summary>
    internal PdfDictionary? Changed => _resources;

    /// <summary>Gets the value of a named colour space in the resources.</summary>
    /// <param name="name">The resource name.</param>
    /// <returns>The colour space as stored (a name, array or reference), or null when the resources have none of that name.</returns>
    internal PdfValue GetColorSpace(PdfName name) => _original?.GetDictionary(KnownName.ColorSpace)?.GetRaw(name) ?? default;

    /// <summary>Adds an image XObject under a name that no XObject of the page has.</summary>
    /// <param name="image">The image object.</param>
    /// <returns>The name.</returns>
    internal PdfName AddImage(PdfObjectId image)
    {
        _resources ??= _original?.Clone() ?? new PdfDictionary(_store);
        _images ??= _resources.GetDictionary(KnownName.XObject)?.Clone() ?? new PdfDictionary(_store);
        PdfName name;
        do
        {
            name = _store.Names.Intern(string.Create(CultureInfo.InvariantCulture, $"{NamePrefix}{_nextNumber}"));
            _nextNumber++;
        }
        while (_images.ContainsKey(name));

        _images.Set(name, PdfValue.FromReference(image));
        _resources.Set(KnownName.XObject, PdfValue.FromDictionary(_images));
        return name;
    }
}
