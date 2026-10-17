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

/// <summary>Options for opening a document. Decode limits are validated when creating a decoding context.</summary>
/// <param name="FontFactory">Loads fonts, or null to leave glyphs without a font.</param>
/// <param name="DrawingServices">Drawing services, or null for managed defaults.</param>
/// <param name="Password">The document password, or null.</param>
/// <param name="Certificate">A recipient certificate with its private key, or null.</param>
/// <param name="Diagnostics">A quick, thread-safe repair callback that must not throw or reenter the document.</param>
/// <param name="Recovery">Whether damaged objects and cross-reference data may be recovered.</param>
/// <param name="IgnoreXrefStreams">Whether cross-reference streams are ignored, including hybrid references.</param>
/// <param name="Source">How path-based input reads its bytes.</param>
/// <param name="CacheBytes">The stream or file-handle page cache budget.</param>
/// <param name="MaxDecompressedBytes">The positive output limit for each filter, at most 256 MiB.</param>
/// <param name="CancellationToken">Stops opening and later document reads.</param>
[DebuggerDisplay("PdfOpenOptions: {Source} sink {Diagnostics != null}")]
public sealed record PdfOpenOptions(
    Func<PdfDictionary, PdfFont?>? FontFactory,
    PdfDrawingServices? DrawingServices,
    string? Password,
    X509Certificate2? Certificate,
    Action<PdfDiagnostic>? Diagnostics,
    bool Recovery,
    bool IgnoreXrefStreams,
    PdfSourceKind Source,
    long CacheBytes,
    int MaxDecompressedBytes,
    CancellationToken CancellationToken)
{
    /// <summary>Initializes a new instance of the <see cref="PdfOpenOptions"/> class.</summary>
    public PdfOpenOptions()
        : this(PdfFontLoader.Load, null, null, null, null, true, false, default, StreamPdfByteSource.DefaultCacheBytes, PdfLimits.MaxDecodedLength, default)
    {
    }

    /// <summary>Gets the shared options with no password, cancellation or diagnostics.</summary>
    public static PdfOpenOptions Default { get; } = new();
}
