// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Signatures.Signing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;

namespace HyperPdf.Engine;

/// <summary>Maps the app's signing types to the library's and back.</summary>
internal static class SigningMapping
{
    /// <summary>Maps a request to library options.</summary>
    /// <param name="request">The request.</param>
    /// <param name="timestamper">The timestamp authority, or <see langword="null"/>.</param>
    /// <returns>The options.</returns>
    internal static PdfSigningOptions ToOptions(in SigningRequest request, ISignatureTimestamper? timestamper) => new()
    {
        Format = ToFormat(request.Format),
        Digest = ToDigestOrAutomatic(request.Digest),
        FieldName = string.IsNullOrEmpty(request.FieldName) ? null : request.FieldName,
        Appearance = ToAppearance(request),
        Reason = string.IsNullOrEmpty(request.Reason) ? null : request.Reason,
        Location = string.IsNullOrEmpty(request.Location) ? null : request.Location,
        ContactInfo = string.IsNullOrEmpty(request.ContactInfo) ? null : request.ContactInfo,
        SigningTime = request.Time,
        Certification = (PdfMdpPermission)(int)request.Certification,
        Timestamp = timestamper is null ? null : new TimestamperAdapter(timestamper),
        TimestampDigest = ToDigest(request.TimestampDigest),
    };

    /// <summary>Maps a library preview to the app's.</summary>
    /// <param name="preview">The preview.</param>
    /// <returns>The app's preview.</returns>
    internal static SigningPreview ToPreview(PdfSigningPreview preview) => new(
        preview.FieldName,
        preview.IsNewField,
        preview.PageNumber,
        preview.IsVisible,
        FromFormat(preview.Format),
        FromDigest(preview.Digest),
        preview.Reason,
        preview.Location,
        (CertificationLevel)(int)preview.Certification,
        preview.LocksDocument,
        preview.LaterChanges,
        preview.TimestampRequired,
        preview.Problems,
        preview.Notes);

    /// <summary>Maps a digest of the app to the library's.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The library's digest.</returns>
    internal static PdfDigestMethod ToDigest(SigningDigest digest) => digest switch
    {
        SigningDigest.Sha3_256 => PdfDigestMethod.Sha3_256,
        SigningDigest.Sha3_384 => PdfDigestMethod.Sha3_384,
        SigningDigest.Sha3_512 => PdfDigestMethod.Sha3_512,
        SigningDigest.Shake256 => PdfDigestMethod.Shake256,
        SigningDigest.Sha384 => PdfDigestMethod.Sha384,
        SigningDigest.Sha512 => PdfDigestMethod.Sha512,
        _ => PdfDigestMethod.Sha256,
    };

    /// <summary>Maps a digest of the library to the app's.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The app's digest.</returns>
    internal static SigningDigest FromDigest(PdfDigestMethod digest) => digest switch
    {
        PdfDigestMethod.Sha3_256 => SigningDigest.Sha3_256,
        PdfDigestMethod.Sha3_384 => SigningDigest.Sha3_384,
        PdfDigestMethod.Sha3_512 => SigningDigest.Sha3_512,
        PdfDigestMethod.Shake256 => SigningDigest.Shake256,
        PdfDigestMethod.Sha384 => SigningDigest.Sha384,
        PdfDigestMethod.Sha512 => SigningDigest.Sha512,
        _ => SigningDigest.Sha256,
    };

    /// <summary>Maps the kind of key of the app to the library's signature algorithm.</summary>
    /// <param name="kind">The kind of key.</param>
    /// <returns>The algorithm.</returns>
    internal static PdfSignatureAlgorithm ToAlgorithm(SigningKeyKind kind) => kind switch
    {
        SigningKeyKind.RsaPss => PdfSignatureAlgorithm.RsaPss,
        SigningKeyKind.Ecdsa => PdfSignatureAlgorithm.Ecdsa,
        _ => PdfSignatureAlgorithm.RsaPkcs1,
    };

    /// <summary>Maps the app's signer identity to the library's.</summary>
    /// <param name="identity">The identity.</param>
    /// <returns>The library's identity.</returns>
    internal static PdfSignerIdentity ToIdentity(SignerIdentity identity) => new(identity.Certificates, ToAlgorithm(identity.KeyKind));

    /// <summary>Maps a format of the library to the app's.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The app's format.</returns>
    internal static SigningFormat FromFormat(PdfSignatureFormat format) => format switch
    {
        PdfSignatureFormat.CadesDetached => SigningFormat.CadesDetached,
        PdfSignatureFormat.Pkcs7Detached => SigningFormat.Pkcs7Detached,
        _ => SigningFormat.Automatic,
    };

    /// <summary>Maps a digest choice; the default SHA-256 lets the library pick the digest the key suits.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The library's digest, or <see langword="null"/> for the default.</returns>
    private static PdfDigestMethod? ToDigestOrAutomatic(SigningDigest digest) => digest == SigningDigest.Sha256 ? null : ToDigest(digest);

    /// <summary>Maps a format of the app to the library's.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The library's format, or <see langword="null"/> for automatic.</returns>
    private static PdfSignatureFormat? ToFormat(SigningFormat format) => format switch
    {
        SigningFormat.Pkcs7Detached => PdfSignatureFormat.Pkcs7Detached,
        SigningFormat.CadesDetached => PdfSignatureFormat.CadesDetached,
        _ => null,
    };

    /// <summary>Maps where a signature goes; an existing field keeps its own look unless an area is given.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The appearance, or <see langword="null"/> to leave the field's own.</returns>
    private static PdfSignatureAppearance? ToAppearance(in SigningRequest request)
    {
        if (!request.Area.IsEmpty)
        {
            return new() { PageIndex = request.PageIndex, Rectangle = ToRectangle(request.Area) };
        }

        return string.IsNullOrEmpty(request.FieldName) ? new() { PageIndex = request.PageIndex } : null;
    }

    /// <summary>Maps an area to a rectangle.</summary>
    /// <param name="area">The area.</param>
    /// <returns>The rectangle.</returns>
    private static PdfRectangle ToRectangle(in SigningArea area) => new((float)area.Left, (float)area.Bottom, (float)area.Right, (float)area.Top);
}
