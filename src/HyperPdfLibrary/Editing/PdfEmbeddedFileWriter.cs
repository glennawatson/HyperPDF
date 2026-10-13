// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.AssociatedFiles;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Builds embedded files (ISO 32000-2 7.11.4): the <c>/EmbeddedFile</c> stream with its MIME type and <c>/Params</c>
/// (size, dates and MD5 checksum), and the <c>/Filespec</c> dictionary that names it and points at it from <c>/EF</c>.
/// </summary>
internal static class PdfEmbeddedFileWriter
{
    /// <summary>The MIME type used for a file of unknown type when the PDF/A level needs one.</summary>
    internal const string UnknownMimeType = "application/octet-stream";

    /// <summary>The bytes reserved for a formatted PDF date.</summary>
    private const int DateBufferLength = 32;

    /// <summary>The characters other than letters and digits that a MIME type or subtype may hold (RFC 6838).</summary>
    private const string MimeSymbols = "!#$&-^_.+";

    /// <summary>The longest MIME type accepted.</summary>
    private const int MaxMimeLength = 127;

    /// <summary>Makes the file specification and embedded file stream for a file, as new objects.</summary>
    /// <param name="edits">The edits the objects belong to.</param>
    /// <param name="document">The document.</param>
    /// <param name="spec">The file.</param>
    /// <param name="needsAssociation">Whether the document's PDF/A level needs a MIME type and a relationship on every file.</param>
    /// <returns>The file specification's object.</returns>
    /// <exception cref="ArgumentException">The name is empty or the MIME type is not valid.</exception>
    internal static PdfObjectId Create(PdfObjectEdits edits, PdfDocument document, PdfAttachmentSpec spec, bool needsAssociation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Name);
        var store = document.Objects;
        var mime = spec.MimeType ?? (needsAssociation ? UnknownMimeType : null);
        var stream = CreateStream(store, spec.Data.Span, mime, spec.Created, spec.Modified, spec.Compress);
        var streamId = edits.Transaction.Add(PdfValue.FromStream(stream));
        var files = new PdfDictionary(store);
        files.Set(KnownName.F, PdfValue.FromReference(streamId));
        files.Set(KnownName.UF, PdfValue.FromReference(streamId));
        var filespec = new PdfDictionary(store);
        filespec.Set(KnownName.Type, PdfValue.FromName(KnownName.Filespec));
        filespec.Set(KnownName.F, PdfValue.FromString(Encoding.ASCII.GetBytes(AsciiName(spec.Name))));
        filespec.Set(KnownName.UF, PdfValue.FromString(PdfText.Encode(spec.Name, store.Version)));
        filespec.Set(KnownName.EF, PdfValue.FromDictionary(files));
        SetDescription(filespec, spec.Description, store.Version);
        var relationship = spec.Relationship ?? (needsAssociation ? PdfFileRelationship.Unspecified : null);
        SetRelationship(store, filespec, relationship);
        return edits.Add(filespec);
    }

    /// <summary>Builds an <c>/EmbeddedFile</c> stream.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="data">The file's bytes.</param>
    /// <param name="mime">The MIME type, or <see langword="null"/>.</param>
    /// <param name="created">The creation date, or <see langword="null"/>.</param>
    /// <param name="modified">The modification date, or <see langword="null"/>.</param>
    /// <param name="compress">Whether to compress the bytes with Flate.</param>
    /// <returns>The stream.</returns>
    /// <exception cref="ArgumentException">The MIME type is not valid.</exception>
    internal static PdfStream CreateStream(PdfObjectStore store, ReadOnlySpan<byte> data, string? mime, DateTimeOffset? created, DateTimeOffset? modified, bool compress)
    {
        var dictionary = new PdfDictionary(store);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.EmbeddedFile));
        SetMimeType(store, dictionary, mime);
        dictionary.Set(KnownName.Params, PdfValue.FromDictionary(CreateParameters(store, data, created, modified)));
        if (compress && !data.IsEmpty)
        {
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return new(dictionary, Compress(data));
        }

        return new(dictionary, data.ToArray());
    }

    /// <summary>Sets or removes a stream's <c>/Subtype</c> from a MIME type, escaping the slash as <c>#2F</c> when written.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="mime">The MIME type, <see langword="null"/> to leave, or an empty string to remove.</param>
    /// <exception cref="ArgumentException">The MIME type is not valid.</exception>
    internal static void SetMimeType(PdfObjectStore store, PdfDictionary dictionary, string? mime)
    {
        if (mime is null)
        {
            return;
        }

        if (mime.Length == 0)
        {
            _ = dictionary.Remove(KnownName.Subtype);
            return;
        }

        RequireMimeType(mime);
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(store.Names.Intern(Encoding.ASCII.GetBytes(mime))));
    }

    /// <summary>Builds the <c>/Params</c> dictionary: size, dates and MD5 checksum of the uncompressed bytes.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="data">The file's bytes.</param>
    /// <param name="created">The creation date, or <see langword="null"/>.</param>
    /// <param name="modified">The modification date, or <see langword="null"/>.</param>
    /// <returns>The parameters.</returns>
    internal static PdfDictionary CreateParameters(PdfObjectStore store, ReadOnlySpan<byte> data, DateTimeOffset? created, DateTimeOffset? modified)
    {
        var parameters = new PdfDictionary(store);
        parameters.Set(KnownName.Size, PdfValue.FromInteger(data.Length));
        SetDate(parameters, KnownName.CreationDate, created);
        SetDate(parameters, KnownName.ModDate, modified);
        Span<byte> hash = stackalloc byte[Md5.HashLength];
        _ = Md5.HashData(data, hash);
        parameters.Set(KnownName.CheckSum, PdfValue.FromString(hash.ToArray()));
        return parameters;
    }

    /// <summary>Sets a date entry from a date, or leaves the entry alone for <see langword="null"/>.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The date.</param>
    internal static void SetDate(PdfDictionary dictionary, KnownName key, DateTimeOffset? value)
    {
        if (value is not { } date)
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[DateBufferLength];
        var length = PdfDate.Format(date, buffer);
        dictionary.Set(key, PdfValue.FromString(buffer[..length].ToArray()));
    }

    /// <summary>Sets or removes a file specification's description.</summary>
    /// <param name="filespec">The file specification.</param>
    /// <param name="description">The text, <see langword="null"/> to leave, or an empty string to remove.</param>
    /// <param name="version">The document's header version.</param>
    internal static void SetDescription(PdfDictionary filespec, string? description, string version)
    {
        if (description is null)
        {
            return;
        }

        if (description.Length == 0)
        {
            _ = filespec.Remove(KnownName.Desc);
            return;
        }

        filespec.Set(KnownName.Desc, PdfValue.FromString(PdfText.Encode(description, version)));
    }

    /// <summary>Sets a file specification's <c>/AFRelationship</c>.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="filespec">The file specification.</param>
    /// <param name="relationship">The relationship, or <see langword="null"/> to leave it.</param>
    internal static void SetRelationship(PdfObjectStore store, PdfDictionary filespec, PdfFileRelationship? relationship)
    {
        if (relationship is { } value)
        {
            filespec.Set(store.Names.Intern("AFRelationship"u8), PdfValue.FromName(store.Names.Intern(value.ToString())));
        }
    }

    /// <summary>Makes a file name safe for the <c>/F</c> string, which older readers read as bytes: characters outside ASCII become <c>_</c>.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The ASCII name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string AsciiName(string name) => string.Create(
        name.Length,
        name,
        static (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = source[i] is >= ' ' and <= '~' ? source[i] : '_';
            }
        });

    /// <summary>Checks that a string is a MIME type: a type and a subtype separated by a slash, each made of RFC 6838 characters.</summary>
    /// <param name="mime">The type.</param>
    /// <exception cref="ArgumentException">The type is not valid.</exception>
    private static void RequireMimeType(string mime)
    {
        var slash = mime.IndexOf('/', StringComparison.Ordinal);
        var valid = mime.Length <= MaxMimeLength && slash > 0 && slash < mime.Length - 1 && mime.IndexOf('/', slash + 1) < 0;
        foreach (var character in mime)
        {
            valid &= character == '/' || char.IsAsciiLetterOrDigit(character) || MimeSymbols.Contains(character, StringComparison.Ordinal);
        }

        if (!valid)
        {
            throw new ArgumentException($"'{mime}' is not a MIME type such as application/pdf.", nameof(mime));
        }
    }

    /// <summary>Compresses bytes with Flate.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The zlib stream.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] Compress(ReadOnlySpan<byte> data)
    {
        var output = default(PooledBuffer);
        try
        {
            ZLibCodec.Compress(data, ref output);
            return output.WrittenSpan.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }
}
