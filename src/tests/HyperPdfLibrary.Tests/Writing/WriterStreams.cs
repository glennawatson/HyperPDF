// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Reads the streams and identifiers of a written file, for the writer control tests.</summary>
internal static class WriterStreams
{
    /// <summary>The number of parts in a file identifier.</summary>
    private const int PartCount = 2;

    /// <summary>The byte filter named in a stream, or an empty string for none.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="stream">The stream.</param>
    /// <returns>The filter names joined by commas.</returns>
    internal static string FilterOf(PdfObjectStore store, PdfStream stream)
    {
        var filter = stream.Dictionary.Get(KnownName.Filter);
        if (filter.AsArray() is not { } array)
        {
            return filter.AsName() is { IsNone: false } name ? store.Names.GetString(name) : string.Empty;
        }

        var names = new List<string>();
        for (var i = 0; i < array.Count; i++)
        {
            names.Add(store.Names.GetString(array.GetName(i)));
        }

        return string.Join(',', names);
    }

    /// <summary>Lists every stream object in a file.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The streams, in object order.</returns>
    internal static List<PdfStream> All(PdfObjectStore store)
    {
        var streams = new List<PdfStream>();
        for (var number = 1; number < store.Size; number++)
        {
            if (StoreReading.GetObject(store, new(number, 0)).AsStream() is { } stream)
            {
                streams.Add(stream);
            }
        }

        return streams;
    }

    /// <summary>Finds the first stream that matches a test.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="match">The test.</param>
    /// <returns>The stream, or <see langword="null"/>.</returns>
    internal static PdfStream? Find(PdfObjectStore store, Func<PdfStream, bool> match)
    {
        foreach (var stream in All(store))
        {
            if (match(stream))
            {
                return stream;
            }
        }

        return null;
    }

    /// <summary>Reads the file identifier of a file.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The two parts, or <see langword="null"/> when the trailer has no /ID.</returns>
    internal static FileId? ReadId(byte[] file)
    {
        using var store = StoreOpening.Open(file, null);
        return store.Trailer.GetArray(KnownName.ID) is { Count: PartCount } id ? new FileId(id.Get(0).AsStringBytes().ToArray(), id.Get(1).AsStringBytes().ToArray()) : null;
    }

    /// <summary>Gets the header line of a file.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The header, such as <c>%PDF-1.7</c>.</returns>
    internal static string Header(byte[] file) => Encoding.ASCII.GetString(file, 0, "%PDF-1.7".Length);

    /// <summary>The two parts of a file identifier (ISO 32000-2 §14.4).</summary>
    /// <param name="First">The permanent part, made when the file was first written.</param>
    /// <param name="Second">The part that changes when the file is updated.</param>
    internal readonly record struct FileId(byte[] First, byte[] Second);
}
