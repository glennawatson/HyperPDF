// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Finds the metadata streams the PDF/A metadata rules check: the catalog's, and those of every other dictionary.</summary>
internal static class PdfAMetadataStreams
{
    /// <summary>Gets the catalog's metadata stream when it is a proper metadata stream (<c>/Type /Metadata /Subtype /XML</c>).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The stream, or <see langword="null"/>.</returns>
    internal static PdfStream? Main(PdfDocument document) =>
        document.Catalog.GetStream(KnownName.Metadata) is { } stream && IsMetadataStream(document.Objects, stream) ? stream : null;

    /// <summary>Gets the catalog's <c>/Metadata</c> stream whatever its type, which the document information crosswalk reads.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The stream, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfStream? Catalog(PdfDocument document) => document.Catalog.GetStream(KnownName.Metadata);

    /// <summary>Finds the metadata streams of every dictionary other than the catalog, each once.</summary>
    /// <param name="document">The document.</param>
    /// <param name="main">The catalog's metadata stream, which is not listed again.</param>
    /// <param name="cancellationToken">Stops the search between objects.</param>
    /// <returns>The streams, in object order.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static List<PdfAMetadataStream> Others(PdfDocument document, PdfStream? main, CancellationToken cancellationToken)
    {
        var store = document.Objects;
        var found = new List<PdfAMetadataStream>();
        var seen = new HashSet<PdfStream>(ReferenceEqualityComparer.Instance);
        if (main is not null)
        {
            _ = seen.Add(main);
        }

        for (var number = 1; number < store.Size; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Visit(store, StoreReading.GetObject(store, new(number, 0)), number, found, seen, 0);
        }

        return found;
    }

    /// <summary>Looks for metadata in a value and the dictionaries and arrays held directly inside it.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="value">The value.</param>
    /// <param name="number">The number of the object the value belongs to.</param>
    /// <param name="found">The streams found.</param>
    /// <param name="seen">The streams already found.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void Visit(PdfObjectStore store, PdfValue value, int number, List<PdfAMetadataStream> found, HashSet<PdfStream> seen, int depth)
    {
        if (depth >= PdfLimits.MaxNesting)
        {
            return;
        }

        if (value.AsDictionary() is { } dictionary)
        {
            Check(store, dictionary, number, found, seen);
            for (var i = 0; i < dictionary.Count; i++)
            {
                Visit(store, dictionary.GetValueAt(i), number, found, seen, depth + 1);
            }
        }
        else if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                Visit(store, array.GetRaw(i), number, found, seen, depth + 1);
            }
        }
    }

    /// <summary>Records a dictionary's metadata stream, unless the dictionary is the catalog.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="number">The number of the object the dictionary belongs to.</param>
    /// <param name="found">The streams found.</param>
    /// <param name="seen">The streams already found.</param>
    private static void Check(PdfObjectStore store, PdfDictionary dictionary, int number, List<PdfAMetadataStream> found, HashSet<PdfStream> seen)
    {
        if (dictionary.IsName(KnownName.Type, KnownName.Catalog) || dictionary.GetStream(KnownName.Metadata) is not { } stream)
        {
            return;
        }

        if (IsMetadataStream(store, stream) && seen.Add(stream))
        {
            found.Add(new(stream, PdfAContexts.Object(number)));
        }
    }

    /// <summary>Determines whether a stream is marked as an XML metadata stream.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="stream">The stream.</param>
    /// <returns><see langword="true"/> when it has <c>/Type /Metadata</c> and <c>/Subtype /XML</c>.</returns>
    private static bool IsMetadataStream(PdfObjectStore store, PdfStream stream) =>
        stream.Dictionary.IsName(KnownName.Type, KnownName.Metadata) && stream.Dictionary.GetName(KnownName.Subtype) == store.Names.Intern("XML"u8);
}
