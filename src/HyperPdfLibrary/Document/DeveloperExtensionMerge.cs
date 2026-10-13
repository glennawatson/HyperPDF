// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Extensions;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>Builds the catalog that results from declaring a developer extension (ISO 32000-2 §7.12, Table 1).</summary>
internal static class DeveloperExtensionMerge
{
    /// <summary>The bytes of a URL compared on the stack; a longer URL is encoded on the heap.</summary>
    private const int UrlStackLength = 256;

    /// <summary>Merges an extension into a copy of the catalog.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="extension">The extension to declare.</param>
    /// <param name="baseVersion">The extension's base version.</param>
    /// <param name="arrayForm">Whether the prefix is written as an array of dictionaries.</param>
    /// <returns>The new catalog, or <see langword="null"/> when the catalog already declares the extension and needs no change.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfDictionary? Merge(PdfObjectStore store, PdfDeveloperExtension extension, PdfVersion baseVersion, bool arrayForm) =>
        Merge(store, store.Catalog, extension, baseVersion, arrayForm);

    /// <summary>Merges an extension into a copy of a catalog, which may already be a copy a save repair has changed.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="source">The catalog to copy.</param>
    /// <param name="extension">The extension to declare.</param>
    /// <param name="baseVersion">The extension's base version; the unset version leaves the document's version alone.</param>
    /// <param name="arrayForm">Whether the prefix is written as an array of dictionaries.</param>
    /// <returns>The new catalog, or <see langword="null"/> when the catalog already declares the extension and needs no change.</returns>
    internal static PdfDictionary? Merge(PdfObjectStore store, PdfDictionary source, PdfDeveloperExtension extension, PdfVersion baseVersion, bool arrayForm)
    {
        var prefix = store.Names.Intern(extension.Prefix);
        var current = source.GetDictionary(KnownName.Extensions);
        var existing = current?.Get(prefix) ?? default;

        // Reading what is declared first means an extension that is already there builds and copies nothing.
        var declare = NeedsEntry(store, existing, extension, arrayForm);
        var raised = baseVersion > PdfVersion.Max(PdfVersionRules.Header(store), PdfVersionRules.Catalog(store));
        if (!declare && !raised)
        {
            return null;
        }

        var catalog = source.Clone();
        if (declare)
        {
            var extensions = current?.Clone() ?? new PdfDictionary(store);
            var built = Build(store, extension);
            extensions.Set(prefix, arrayForm ? MergeArray(store, existing, built) : PdfValue.FromDictionary(built));
            catalog.Set(KnownName.Extensions, PdfValue.FromDictionary(extensions));
        }

        if (raised)
        {
            catalog.Set(KnownName.Version, PdfValue.FromName(store.Names.Intern(baseVersion.ToString())));
        }

        return catalog;
    }

    /// <summary>Counts the dictionaries declared for a prefix.</summary>
    /// <param name="value">The prefix's value: a dictionary or an array of dictionaries.</param>
    /// <returns>The number of entries to look at.</returns>
    private static int CountDeclared(PdfValue value)
    {
        if (value.AsArray() is { } array)
        {
            return array.Count;
        }

        return value.AsDictionary() is null ? 0 : 1;
    }

    /// <summary>Determines whether the declaration must be written, without building it.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="existing">The prefix's value.</param>
    /// <param name="extension">The extension.</param>
    /// <param name="arrayForm">Whether the prefix is written as an array of dictionaries.</param>
    /// <returns><see langword="true"/> when the declaration must be written.</returns>
    private static bool NeedsEntry(PdfObjectStore store, PdfValue existing, PdfDeveloperExtension extension, bool arrayForm) =>
        arrayForm ? NeedsArrayEntry(store, existing, extension) : NeedsSingleEntry(store, existing, extension);

    /// <summary>Gets one dictionary declared for a prefix.</summary>
    /// <param name="value">The prefix's value: a dictionary or an array of dictionaries.</param>
    /// <param name="index">The entry.</param>
    /// <returns>The dictionary, or <see langword="null"/> when the entry is not one.</returns>
    private static PdfDictionary? DeclaredAt(PdfValue value, int index) => value.AsArray() is { } array ? array.GetDictionary(index) : value.AsDictionary();

    /// <summary>Determines whether the array form must change: no entry of the same level declares the same base version and URL.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="existing">The prefix's value.</param>
    /// <param name="extension">The extension.</param>
    /// <returns><see langword="true"/> when the declaration must be written.</returns>
    private static bool NeedsArrayEntry(PdfObjectStore store, PdfValue existing, PdfDeveloperExtension extension)
    {
        var count = CountDeclared(existing);
        for (var i = 0; i < count; i++)
        {
            if (DeclaredAt(existing, i) is { } item && LevelOf(store, item) == extension.ExtensionLevel && Same(store, item, extension))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether the one dictionary an older document keeps for a prefix must change: none holds as high a level.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="existing">The prefix's value.</param>
    /// <param name="extension">The extension.</param>
    /// <returns><see langword="true"/> when the declaration must be written.</returns>
    private static bool NeedsSingleEntry(PdfObjectStore store, PdfValue existing, PdfDeveloperExtension extension)
    {
        var count = CountDeclared(existing);
        for (var i = 0; i < count; i++)
        {
            if (DeclaredAt(existing, i) is { } item && LevelOf(store, item) >= extension.ExtensionLevel)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds an array of the dictionaries with the new one replacing the one of the same level.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="existing">The prefix's value.</param>
    /// <param name="built">The new dictionary.</param>
    /// <returns>The array.</returns>
    private static PdfValue MergeArray(PdfObjectStore store, PdfValue existing, PdfDictionary built)
    {
        var level = LevelOf(store, built);
        var count = CountDeclared(existing);
        var array = new PdfArray(store, count + 1);
        var replaced = false;
        for (var i = 0; i < count; i++)
        {
            if (DeclaredAt(existing, i) is not { } item)
            {
                continue;
            }

            if (LevelOf(store, item) != level)
            {
                array.Add(PdfValue.FromDictionary(item));
                continue;
            }

            array.Add(PdfValue.FromDictionary(built));
            replaced = true;
        }

        if (!replaced)
        {
            array.Add(PdfValue.FromDictionary(built));
        }

        return PdfValue.FromArray(array);
    }

    /// <summary>Builds the extension dictionary.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="extension">The extension.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Build(PdfObjectStore store, PdfDeveloperExtension extension)
    {
        var names = store.Names;
        var dictionary = new PdfDictionary(store);
        dictionary.Set(KnownName.Type, PdfValue.FromName(names.Intern("DeveloperExtensions"u8)));
        dictionary.Set(KnownName.BaseVersion, PdfValue.FromName(names.Intern(extension.BaseVersion)));
        dictionary.Set(names.Intern("ExtensionLevel"u8), PdfValue.FromInteger(extension.ExtensionLevel));
        if (extension.Url is { } url)
        {
            dictionary.Set(names.Intern("URL"u8), PdfValue.FromString(Encoding.UTF8.GetBytes(url)));
        }

        if (extension.Revision is { } revision)
        {
            dictionary.Set(names.Intern("ExtensionRevision"u8), PdfValue.FromString(Encoding.UTF8.GetBytes(revision)));
        }

        return dictionary;
    }

    /// <summary>Gets the level of an extension dictionary.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The level.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long LevelOf(PdfObjectStore store, PdfDictionary dictionary) => dictionary.GetInteger(store.Names.Intern("ExtensionLevel"u8));

    /// <summary>Determines whether an extension dictionary declares the same base version and URL as an extension.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="declared">The dictionary.</param>
    /// <param name="extension">The extension.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    private static bool Same(PdfObjectStore store, PdfDictionary declared, PdfDeveloperExtension extension) =>
        declared.GetName(KnownName.BaseVersion) == store.Names.Intern(extension.BaseVersion)
        && UrlEquals(declared.GetStringBytes(store.Names.Intern("URL"u8)), extension.Url);

    /// <summary>Determines whether stored URL bytes are the UTF-8 encoding of a URL, with no URL matching empty bytes.</summary>
    /// <param name="stored">The bytes in the dictionary.</param>
    /// <param name="url">The URL, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool UrlEquals(ReadOnlySpan<byte> stored, string? url)
    {
        if (url is null)
        {
            return stored.IsEmpty;
        }

        if (Encoding.UTF8.GetMaxByteCount(url.Length) > UrlStackLength)
        {
            return stored.SequenceEqual(Encoding.UTF8.GetBytes(url));
        }

        Span<byte> encoded = stackalloc byte[UrlStackLength];
        return stored.SequenceEqual(encoded[..Encoding.UTF8.GetBytes(url, encoded)]);
    }
}
