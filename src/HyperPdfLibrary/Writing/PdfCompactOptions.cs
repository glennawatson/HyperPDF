// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Writing;

/// <summary>How <see cref="PdfCompactWriter"/> lays out a document.</summary>
/// <param name="UseObjectStreams">
/// Whether to pack objects into compressed object streams with a cross-reference stream (PDF 1.5 and later); otherwise
/// every object is written plainly with a classic table. A document that claims PDF/A-1 always gets a classic table, as
/// ISO 19005-1 forbids cross-reference streams.
/// </param>
/// <param name="RemoveEncryption">Whether to write an encrypted document decrypted, without /Encrypt.</param>
/// <remarks>
/// The other settings are properties, set with a <c>with</c> expression such as
/// <c>PdfCompactOptions.Default with { StreamMode = PdfStreamMode.Recompress }</c>. The defaults keep the layout the writer
/// has always made.
/// </remarks>
[DebuggerDisplay("PdfCompactOptions: object streams {UseObjectStreams}, remove encryption {RemoveEncryption}, streams {StreamMode}")]
public readonly record struct PdfCompactOptions(bool UseObjectStreams, bool RemoveEncryption)
{
    /// <summary>The inline image size, in bytes, above which <see cref="PdfContentNormalisation.ExternaliseInlineImages"/> moves an image out.</summary>
    internal const int DefaultInlineImageThreshold = 4096;

    /// <summary>Gets the default: object streams, encryption kept.</summary>
    public static PdfCompactOptions Default => new(true, false);

    /// <summary>Gets the layout readable by PDF 1.4 tools: classic table, no object streams, encryption kept.</summary>
    public static PdfCompactOptions Classic => new(false, false);

    /// <summary>Gets what happens to the data of each stream.</summary>
    public PdfStreamMode StreamMode { get; init; }

    /// <summary>Gets how hard Flate compression works, for <see cref="PdfStreamMode.Compress"/>, <see cref="PdfStreamMode.Recompress"/> and object streams.</summary>
    public PdfCompressionLevel CompressionLevel { get; init; }

    /// <summary>Gets the page content changes to make before writing. They are made in the document in memory as one undoable edit.</summary>
    public PdfContentNormalisation Normalisation { get; init; }

    /// <summary>
    /// Gets the inline image size in bytes above which an image is moved out of page content; zero means 4096. Only used
    /// with <see cref="PdfContentNormalisation.ExternaliseInlineImages"/>.
    /// </summary>
    public int InlineImageThreshold { get; init; }

    /// <summary>
    /// Gets a value indicating whether objects no reference reaches are kept. They are renumbered after the reachable
    /// objects, in object number order. The /Encrypt dictionary of a document whose encryption is removed, object streams
    /// and cross-reference streams of the source are never copied.
    /// </summary>
    public bool KeepUnreferenced { get; init; }

    /// <summary>
    /// Gets a value indicating whether the same document always writes the same bytes. Every writer here is
    /// deterministic (objects are numbered breadth first, the second part of /ID is a hash of the written bytes), except
    /// that AES encryption picks a random initialisation vector for each string and stream; a save that would encrypt
    /// with AES fails when this is set, so output is never claimed reproducible when it is not.
    /// </summary>
    public bool Reproducible { get; init; }

    /// <summary>
    /// Gets how to encrypt the file, or <see langword="null"/> to keep the document's own encryption (or none). The new
    /// encryption replaces any the document has: strings and streams are decrypted with the document's handler and encrypted
    /// again with a new key. Encryption applies to the whole file, so only this writer can add it; an incremental save never
    /// adds or changes it. A signed document and one that claims PDF/A are refused. A document opened with only its user
    /// password is refused unless it already allows everything, because only the owner may change the protection.
    /// </summary>
    public PdfEncryptionOptions? Encryption { get; init; }

    /// <summary>
    /// Gets the oldest version the file may declare. The header is raised to it, and never lowered below the document's
    /// own version or what its features need (1.5 for object streams, 2.0 for AES-GCM). The unset version asks for no more
    /// than the features need.
    /// </summary>
    public PdfVersion MinimumVersion { get; init; }

    /// <summary>Gets a value indicating whether the caller has already shown the owner password of the document's current encryption, so the owner rules are met.</summary>
    internal bool OwnerVerified { get; init; }
}
