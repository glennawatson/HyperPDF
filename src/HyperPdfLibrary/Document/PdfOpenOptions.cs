// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Options for <see cref="PdfDocumentReader.OpenWith(byte[], PdfOpenOptions)"/> and its overloads.</summary>
[DebuggerDisplay("PdfOpenOptions: {Source} sink {Diagnostics != null}")]
public sealed record PdfOpenOptions
{
    /// <summary>Gets the shared options with no password, no cancellation and no diagnostics.</summary>
    public static PdfOpenOptions Default { get; } = new();

    /// <summary>Gets the font loader for this document; null leaves text and glyphs without a font.</summary>
    public Func<PdfDictionary, PdfFont?>? FontFactory { get; init; } = PdfFontLoader.Load;

    /// <summary>Gets drawing, font and image services for this document, or null for managed defaults.</summary>
    public PdfDrawingServices? DrawingServices { get; init; }

    /// <summary>Gets or sets the password, or <see langword="null"/>.</summary>
    public string? Password { get; init; }

    /// <summary>Gets or sets a recipient's certificate with its private key, for documents encrypted for certificates, or <see langword="null"/>.</summary>
    public X509Certificate2? Certificate { get; init; }

    /// <summary>
    /// Gets or sets the token that stops long loops (repair scan, page tree walk, object stream indexing, stream
    /// decoding). It stays attached to the document, so cancelling it also stops later reads, which then throw
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets or sets the callback that receives each repair or limit hit, or <see langword="null"/> to report none. It may
    /// be called from any thread, sometimes while the document holds an internal lock, so it must be quick, thread safe
    /// and must not throw or call back into the document.
    /// </summary>
    public Action<PdfDiagnostic>? Diagnostics { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether a damaged cross-reference table, trailer or catalog may be rebuilt by
    /// scanning the file; <see langword="true"/> by default. When <see langword="false"/>, opening throws a
    /// <see cref="PdfException"/> instead, which is what a strict check wants.
    /// </summary>
    public bool Recovery { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether cross-reference streams (and the /XRefStm of hybrid files) are ignored, so
    /// only classic tables are read; <see langword="false"/> by default. A file that needs them is then treated as damaged.
    /// </summary>
    public bool IgnoreXrefStreams { get; init; }

    /// <summary>Gets or sets how a document opened from a path reads its file; mapped by default where the platform allows.</summary>
    public PdfSourceKind Source { get; init; }

    /// <summary>Gets or sets the page cache budget in bytes when the file is read through a stream or file handle.</summary>
    public long CacheBytes { get; init; } = StreamPdfByteSource.DefaultCacheBytes;
}
