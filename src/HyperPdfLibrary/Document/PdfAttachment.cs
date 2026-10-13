// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.AssociatedFiles;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>A file embedded in the document.</summary>
/// <param name="Index">The attachment's position in the /EmbeddedFiles name tree.</param>
/// <param name="Name">The file name, from the file specification.</param>
/// <param name="Data">The embedded file stream, or <see langword="null"/> when the specification has none.</param>
[DebuggerDisplay("PdfAttachment: {Name}")]
public sealed record PdfAttachment(int Index, string Name, PdfStream? Data)
{
    /// <summary>Gets the attachment's name in the name tree, which the attachment editing methods take to name it.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Gets the object of the file specification, or an invalid id when the tree holds it inline.</summary>
    public PdfObjectId Id { get; init; }

    /// <summary>Gets the file's description, or <see langword="null"/> for none.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the file's MIME type, or <see langword="null"/> when unknown.</summary>
    public string? MimeType { get; init; }

    /// <summary>Gets how the file relates to the document, or <see langword="null"/> when it states none or one this library does not define.</summary>
    public PdfFileRelationship? Relationship { get; init; }

    /// <summary>Gets the creation date the file records, or <see langword="null"/> for none.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>Gets the modification date the file records, or <see langword="null"/> for none.</summary>
    public DateTimeOffset? Modified { get; init; }

    /// <summary>Gets the size the file records (<c>/Params /Size</c>), or <see langword="null"/> for none.</summary>
    public long? Size { get; init; }

    /// <summary>Gets the MD5 checksum the file records (<c>/Params /CheckSum</c>), or <see langword="null"/> for none.</summary>
    public byte[]? CheckSum { get; init; }
}
