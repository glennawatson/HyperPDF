// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// A signature that is prepared and waiting for its signature value, for a hardware token or a remote signing service that
/// signs apart from the library. It holds the finished file with room reserved for the signature, so it is complete as long as
/// the object is kept; <see cref="PdfDocumentSigning.CompleteAsync(PdfPendingSignature, ReadOnlyMemory{byte}, CancellationToken)"/> or
/// <see cref="PdfDocumentSigning.CompleteWithContainerAsync(PdfPendingSignature, ReadOnlyMemory{byte}, CancellationToken)"/> finishes it.
/// It can be finished once: a second completion throws <see cref="InvalidOperationException"/>, and so does a completion that
/// overlaps another. A completion that failed because the value did not fit leaves it open.
/// </summary>
[DebuggerDisplay("PdfPendingSignature: {FieldName} {Digest}")]
public sealed class PdfPendingSignature
{
    /// <summary>The state of a signature no completion has claimed.</summary>
    private const int OpenState = 0;

    /// <summary>The state of a signature a completion has claimed, or finished.</summary>
    private const int ClaimedState = 1;

    /// <summary>Open or claimed.</summary>
    private int _state;

    /// <summary>Initializes a new instance of the <see cref="PdfPendingSignature"/> class.</summary>
    /// <param name="work">The prepared file and plan.</param>
    /// <param name="signedAttributes">The DER signed attributes.</param>
    /// <param name="documentDigest">The digest of the signed bytes.</param>
    /// <param name="spec">What the container is made of.</param>
    /// <param name="attempts">The times the file was built.</param>
    internal PdfPendingSignature(PdfPreparedSignature work, byte[] signedAttributes, byte[] documentDigest, in PdfCmsSpec spec, int attempts)
    {
        Work = work;
        SignedAttributes = signedAttributes;
        DocumentDigest = documentDigest;
        Spec = spec;
        Attempts = attempts;
    }

    /// <summary>Gets the DER-encoded signed attributes: the bytes ("ToBeSigned") a signer hashes with <see cref="Digest"/> and signs.</summary>
    public byte[] SignedAttributes { get; }

    /// <summary>
    /// Gets the digest of the signed bytes of the file, which is the messageDigest attribute inside <see cref="SignedAttributes"/>.
    /// A signing service that builds the whole container signs this.
    /// </summary>
    public byte[] DocumentDigest { get; }

    /// <summary>Gets the digest to hash <see cref="SignedAttributes"/> with.</summary>
    public PdfDigestMethod Digest => Work.Plan.Digest;

    /// <summary>Gets the signature format.</summary>
    public PdfSignatureFormat Format => Work.Plan.Format;

    /// <summary>Gets the fully qualified name of the signature field.</summary>
    public string FieldName => Work.Plan.FieldName;

    /// <summary>Gets the room reserved for the signature value, in bytes.</summary>
    public int ContentsCapacity => Work.File.Capacity;

    /// <summary>Gets the times the file was built.</summary>
    internal int Attempts { get; }

    /// <summary>Gets what the container is made of.</summary>
    internal PdfCmsSpec Spec { get; }

    /// <summary>Gets the prepared file and plan.</summary>
    internal PdfPreparedSignature Work { get; }

    /// <summary>Takes the right to write the signature value into the file, which only one completion may have.</summary>
    /// <exception cref="InvalidOperationException">Another completion has the file or finished it.</exception>
    internal void Claim()
    {
        if (Interlocked.CompareExchange(ref _state, ClaimedState, OpenState) != OpenState)
        {
            throw new InvalidOperationException("This pending signature was already completed, or is being completed. Prepare the signature again to sign again.");
        }
    }

    /// <summary>Gives the right back after a completion wrote nothing, so the signature can be completed again.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release() => Volatile.Write(ref _state, OpenState);
}
