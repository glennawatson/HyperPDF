// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Signatures.Signing;
using HyperPdfLibrary.Signing;

namespace HyperPdf.Engine;

/// <summary>A pending signature held by the engine: the library's prepared file with room reserved for the signature.</summary>
internal sealed class EnginePendingSignature : IPendingSignature
{
    /// <summary>1 once a completion has started.</summary>
    private int _claimed;

    /// <summary>Initializes a new instance of the <see cref="EnginePendingSignature"/> class.</summary>
    /// <param name="pending">The library's pending signature.</param>
    internal EnginePendingSignature(PdfPendingSignature pending) => Pending = pending;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> SignedAttributes => Pending.SignedAttributes;

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> DocumentDigest => Pending.DocumentDigest;

    /// <inheritdoc/>
    public SigningDigest Digest => SigningMapping.FromDigest(Pending.Digest);

    /// <inheritdoc/>
    public SigningFormat Format => SigningMapping.FromFormat(Pending.Format);

    /// <inheritdoc/>
    public string FieldName => Pending.FieldName;

    /// <inheritdoc/>
    public int ContentsCapacity => Pending.ContentsCapacity;

    /// <summary>Gets the library's pending signature.</summary>
    internal PdfPendingSignature Pending { get; }

    /// <summary>
    /// Takes the one completion this signature allows. The library writes the signature value into the prepared file in place,
    /// so a second or concurrent completion would overwrite the first one's file.
    /// </summary>
    /// <returns><see langword="true"/> for the first caller; <see langword="false"/> once the signature was claimed.</returns>
    internal bool TryClaim() => Interlocked.Exchange(ref _claimed, 1) == 0;
}
