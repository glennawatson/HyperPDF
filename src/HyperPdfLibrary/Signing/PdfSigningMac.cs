// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using HyperPdf.Cms;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// Gives a signature in an encrypted document the PDF MAC it needs (ISO/TS 32004:2024 6.5.2). A revision that holds a
/// signature cannot carry a standalone MAC: the MAC would cover the signature's reserved space, which is filled in after.
/// The MAC is attached to the signature as an unsigned attribute instead, which leaves the signed bytes alone.
/// </summary>
internal static class PdfSigningMac
{
    /// <summary>Determines whether a revision signed in this document needs a PDF MAC.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns><see langword="true"/> when the document is encrypted and requires one, or an earlier revision has one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNeeded(PdfObjectStore store) => PdfMacSeal.IsNeeded(store);

    /// <summary>Checks that the document can take a PDF MAC, and takes the keys that make the token.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The keys.</returns>
    /// <exception cref="PdfException">The document needs a PDF MAC and its encryption has no valid /KDFSalt.</exception>
    internal static PdfMacKeyMaterial Keys(PdfObjectStore store)
    {
        _ = PdfMacSeal.Create(store);
        var names = PdfMacNames.From(store.Names);
        var salt = store.Trailer.GetDictionary(KnownName.Encrypt)!.GetStringBytes(names.KdfSalt).ToArray();
        return new(store.Security!.FileKey.ToArray(), salt);
    }

    /// <summary>Builds the trailer entry that points at the signature that carries the MAC.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="signature">The signature dictionary's object id.</param>
    /// <returns>The <c>/AuthCode</c> value.</returns>
    internal static PdfDictionary AuthCode(PdfObjectStore store, PdfObjectId signature)
    {
        var names = PdfMacNames.From(store.Names);
        var authCode = new PdfDictionary(store);
        authCode.Set(names.MacLocation, PdfValue.FromName(names.AttachedToSig));
        authCode.Set(names.SigObjRef, PdfValue.FromReference(signature));
        return authCode;
    }

    /// <summary>Attaches the MAC token to a signature container.</summary>
    /// <param name="keys">The keys that make the token.</param>
    /// <param name="file">The file, with the byte range patched in.</param>
    /// <param name="container">The DER signature container.</param>
    /// <param name="scratchPools">The caller's native-context owner.</param>
    /// <param name="cancellationToken">Cancels hashing and attachment.</param>
    /// <returns>The container with the token as an unsigned attribute of its signer.</returns>
    internal static byte[] Attach(PdfMacKeyMaterial keys, PdfSigningFile file, byte[] container, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cms = new SignedCms();
        cms.Decode(container);
        Span<byte> data = stackalloc byte[CmsDigests.MaxLength];
        var dataLength = PdfSigningFileWriter.HashSigned(file, CmsDigestAlgorithm.Sha256, data, scratchPools, cancellationToken);
        var signatureDigest = SHA256.HashData(cms.SignerInfos[0].GetSignature());
        var token = PdfMacTokenWriter.Create(keys.FileKey, keys.Salt, CmsDigestAlgorithm.Sha256, data[..dataLength], signatureDigest);
        return PdfMacSignatureAttachment.Attach(container, token);
    }

    /// <summary>Gets the size the attached token adds to a container.</summary>
    /// <returns>The size in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AttachedSize() => PdfMacTokenWriter.Length(CmsDigestAlgorithm.Sha256, true);
}
