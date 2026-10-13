// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using HyperPdf.Cms;
using HyperPdfLibrary.Compat;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Checks a PDF MAC token: the CMS AuthenticatedData of ISO/TS 32004:2024 6.3 and the MAC validation of RFC 5652 9.3 with
/// the key derivation of 6.4. Every read is bounded by the token's length, and damaged input is reported, not thrown.
/// </summary>
internal static class PdfMacTokenReader
{
    /// <summary>The most bytes a wrapped MAC key may have: a 64-byte key and the 8-byte check value.</summary>
    private const int MaxWrappedKey = 72;

    /// <summary>The fewest bytes a wrapped key has: two blocks and the check value.</summary>
    private const int MinWrappedKey = 24;

    /// <summary>The length of an HMAC-SHA-256 output.</summary>
    private const int MacLength = 32;

    /// <summary>The context-specific tag number of the PasswordRecipientInfo.</summary>
    private const int RecipientTagNumber = 3;

    /// <summary>The DER tag byte of a universal SET OF, which replaces the <c>[2]</c> tag when the MAC input is formed (RFC 5652 9.2).</summary>
    private const byte SetOfTag = 0x31;

    /// <summary>Gets the tag of the <c>[3]</c> PasswordRecipientInfo (RFC 5652 6.2.4).</summary>
    private static Asn1Tag RecipientTag { get; } = new(TagClass.ContextSpecific, RecipientTagNumber, true);

    /// <summary>Checks a token against the document's file key and salt.</summary>
    /// <param name="token">The DER token.</param>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="salt">The 32-byte <c>/KDFSalt</c>.</param>
    /// <returns>The result; its ranges lie in <paramref name="token"/>.</returns>
    internal static PdfMacTokenResult Verify(ReadOnlySpan<byte> token, ReadOnlySpan<byte> fileKey, ReadOnlySpan<byte> salt)
    {
        if (token.IsEmpty || token.Length > PdfLimits.MaxMacTokenLength || salt.Length != PdfMacKeys.SaltLength)
        {
            return PdfMacTokenResult.Failed(PdfMacTokenStatus.Malformed, CmsDigestAlgorithm.Unknown);
        }

        try
        {
            return VerifyStructure(token, fileKey, salt);
        }
        catch (AsnContentException)
        {
            return PdfMacTokenResult.Failed(PdfMacTokenStatus.Malformed, CmsDigestAlgorithm.Unknown);
        }
    }

    /// <summary>Reads the structure, then checks the digest, the key, the attributes and the MAC.</summary>
    /// <param name="token">The DER token.</param>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="salt">The 32-byte salt.</param>
    /// <returns>The result.</returns>
    /// <exception cref="AsnContentException">The token is damaged.</exception>
    private static PdfMacTokenResult VerifyStructure(ReadOnlySpan<byte> token, ReadOnlySpan<byte> fileKey, ReadOnlySpan<byte> salt)
    {
        var parts = default(PdfMacParts);
        var status = Parse(token, ref parts);
        if (status == PdfMacTokenStatus.Valid)
        {
            status = CheckDigest(parts.Digest);
        }

        if (status != PdfMacTokenStatus.Valid)
        {
            return PdfMacTokenResult.Failed(status, parts.Digest);
        }

        Span<byte> macKey = stackalloc byte[MaxWrappedKey];
        macKey = macKey[..(parts.EncryptedKey.Length - AesKeyWrapping.BlockLength)];
        try
        {
            status = TryUnwrap(parts.EncryptedKey, fileKey, salt, macKey) ? CheckAttributes(ref parts, macKey) : PdfMacTokenStatus.KeyUnwrapFailed;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(macKey);
        }

        return status == PdfMacTokenStatus.Valid ? ReadContent(token, ref parts) : PdfMacTokenResult.Failed(status, parts.Digest);
    }

    /// <summary>Reads the ContentInfo and AuthenticatedData of ISO/TS 32004 6.3.</summary>
    /// <param name="token">The DER token.</param>
    /// <param name="parts">Receives the parts.</param>
    /// <returns>Valid when the structure is right.</returns>
    /// <exception cref="AsnContentException">The token is damaged.</exception>
    private static PdfMacTokenStatus Parse(ReadOnlySpan<byte> token, ref PdfMacParts parts)
    {
        var source = token;
        var contentInfo = CmsDer.ReadContents(ref source, Asn1Tag.Sequence);
        if (!source.IsEmpty || !PdfMacOids.Is(CmsDer.ReadContents(ref contentInfo, Asn1Tag.ObjectIdentifier), PdfMacOids.AuthData))
        {
            return PdfMacTokenStatus.Malformed;
        }

        var wrapper = CmsDer.ReadContents(ref contentInfo, CmsDer.Constructed0);
        var data = CmsDer.ReadContents(ref wrapper, Asn1Tag.Sequence);
        return ParseAuthenticatedData(data, ref parts);
    }

    /// <summary>Reads the fields of the AuthenticatedData in order (RFC 5652 9.1).</summary>
    /// <param name="data">The AuthenticatedData's contents.</param>
    /// <param name="parts">Receives the parts.</param>
    /// <returns>Valid when the structure is right.</returns>
    /// <exception cref="AsnContentException">The data is damaged.</exception>
    private static PdfMacTokenStatus ParseAuthenticatedData(ReadOnlySpan<byte> data, ref PdfMacParts parts)
    {
        if (!IsZero(CmsDer.ReadContents(ref data, Asn1Tag.Integer)) || CmsDer.StartsWith(data, CmsDer.Constructed0))
        {
            return PdfMacTokenStatus.Malformed;
        }

        var recipient = ParseRecipient(ref data, ref parts);
        if (recipient != PdfMacTokenStatus.Valid)
        {
            return recipient;
        }

        var algorithm = CmsDer.ReadAlgorithm(ref data, out var algorithmParameters);
        if (!PdfMacOids.Is(algorithm, PdfMacOids.HmacSha256) || !IsAbsentOrNull(algorithmParameters))
        {
            return PdfMacTokenStatus.UnsupportedAlgorithm;
        }

        var digest = ParseDigest(ref data, ref parts);
        return digest != PdfMacTokenStatus.Valid ? digest : ParseContentAndMac(ref data, ref parts);
    }

    /// <summary>Reads the one PasswordRecipientInfo with the pdfMacWrapKdf and AES-256 key wrap (ISO/TS 32004 6.3.3).</summary>
    /// <param name="data">The AuthenticatedData's contents, advanced past the recipients.</param>
    /// <param name="parts">Receives the wrapped key.</param>
    /// <returns>Valid when the structure is right.</returns>
    /// <exception cref="AsnContentException">The recipients are damaged.</exception>
    private static PdfMacTokenStatus ParseRecipient(ref ReadOnlySpan<byte> data, ref PdfMacParts parts)
    {
        var recipients = CmsDer.ReadContents(ref data, Asn1Tag.SetOf);
        var recipient = CmsDer.ReadContents(ref recipients, RecipientTag);
        if (!recipients.IsEmpty || !IsZero(CmsDer.ReadContents(ref recipient, Asn1Tag.Integer)))
        {
            return PdfMacTokenStatus.Malformed;
        }

        var derivation = CmsDer.ReadContents(ref recipient, CmsDer.Constructed0);
        var derivationOid = CmsDer.ReadContents(ref derivation, Asn1Tag.ObjectIdentifier);
        var wrap = CmsDer.ReadAlgorithm(ref recipient, out var wrapParameters);
        var key = CmsDer.ReadContents(ref recipient, Asn1Tag.PrimitiveOctetString);
        if (!recipient.IsEmpty || !IsWrappedKeyLength(key.Length))
        {
            return PdfMacTokenStatus.Malformed;
        }

        parts.EncryptedKey = key;
        var isExpected = PdfMacOids.Is(derivationOid, PdfMacOids.WrapKdf) && derivation.IsEmpty && PdfMacOids.Is(wrap, PdfMacOids.Aes256Wrap) && wrapParameters.IsEmpty;
        return isExpected ? PdfMacTokenStatus.Valid : PdfMacTokenStatus.UnsupportedAlgorithm;
    }

    /// <summary>Reads the <c>[1]</c> digest algorithm (ISO/TS 32004 6.3.4).</summary>
    /// <param name="data">The AuthenticatedData's contents, advanced past the digest algorithm.</param>
    /// <param name="parts">Receives the digest.</param>
    /// <returns>Valid when the structure is right.</returns>
    /// <exception cref="AsnContentException">The algorithm is damaged.</exception>
    private static PdfMacTokenStatus ParseDigest(ref ReadOnlySpan<byte> data, ref PdfMacParts parts)
    {
        var algorithm = CmsDer.ReadContents(ref data, CmsDer.Constructed1);
        var oid = CmsDer.ReadContents(ref algorithm, Asn1Tag.ObjectIdentifier);
        parts.DigestOid = oid;
        parts.Digest = CmsDigests.FromEncoded(oid);
        return IsAbsentOrNull(algorithm) ? PdfMacTokenStatus.Valid : PdfMacTokenStatus.Malformed;
    }

    /// <summary>Reads the encapsulated content, the authenticated attributes and the MAC, and checks that nothing follows (ISO/TS 32004 6.3.2, 6.3.6 and 6.3.7).</summary>
    /// <param name="data">The AuthenticatedData's contents, advanced past the digest algorithm.</param>
    /// <param name="parts">Receives the parts.</param>
    /// <returns>Valid when the structure is right.</returns>
    /// <exception cref="AsnContentException">The data is damaged.</exception>
    private static PdfMacTokenStatus ParseContentAndMac(ref ReadOnlySpan<byte> data, ref PdfMacParts parts)
    {
        var encapsulated = CmsDer.ReadContents(ref data, Asn1Tag.Sequence);
        if (!PdfMacOids.Is(CmsDer.ReadContents(ref encapsulated, Asn1Tag.ObjectIdentifier), PdfMacOids.IntegrityInfo))
        {
            return PdfMacTokenStatus.Malformed;
        }

        var wrapper = CmsDer.ReadContents(ref encapsulated, CmsDer.Constructed0);
        parts.Content = CmsDer.ReadContents(ref wrapper, Asn1Tag.PrimitiveOctetString);
        if (!CmsDer.StartsWith(data, CmsDer.Constructed2))
        {
            return PdfMacTokenStatus.Malformed;
        }

        parts.AuthenticatedAttributes = CmsDer.ReadEncoded(ref data);
        parts.Mac = CmsDer.ReadContents(ref data, Asn1Tag.PrimitiveOctetString);

        // Anything left is an unauthenticated attribute or trailing data, which the standard does not allow.
        return data.IsEmpty && encapsulated.IsEmpty && wrapper.IsEmpty ? PdfMacTokenStatus.Valid : PdfMacTokenStatus.Malformed;
    }

    /// <summary>Checks that the digest is one of Table 8 and that this computer provides it.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>Valid, <see cref="PdfMacTokenStatus.DigestNotAllowed"/> or <see cref="PdfMacTokenStatus.DigestUnavailable"/>.</returns>
    private static PdfMacTokenStatus CheckDigest(CmsDigestAlgorithm digest)
    {
        if (digest is not (CmsDigestAlgorithm.Sha256 or CmsDigestAlgorithm.Sha384 or CmsDigestAlgorithm.Sha512
            or CmsDigestAlgorithm.Sha3_256 or CmsDigestAlgorithm.Sha3_384 or CmsDigestAlgorithm.Sha3_512))
        {
            return PdfMacTokenStatus.DigestNotAllowed;
        }

        return CmsDigests.Status(digest) == CmsDigestStatus.Available ? PdfMacTokenStatus.Valid : PdfMacTokenStatus.DigestUnavailable;
    }

    /// <summary>Derives the key-encryption key and unwraps the MAC key.</summary>
    /// <param name="wrapped">The wrapped MAC key.</param>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="salt">The 32-byte salt.</param>
    /// <param name="macKey">Receives the MAC key, 8 bytes shorter than <paramref name="wrapped"/>.</param>
    /// <returns><see langword="true"/> when the key unwrapped and its check value is right.</returns>
    private static bool TryUnwrap(ReadOnlySpan<byte> wrapped, ReadOnlySpan<byte> fileKey, ReadOnlySpan<byte> salt, Span<byte> macKey)
    {
        Span<byte> kek = stackalloc byte[PdfMacKeys.KeyEncryptionKeyLength];
        try
        {
            PdfMacKeys.DeriveKeyEncryptionKey(fileKey, salt, kek);
            using var aes = Aes.Create();
            aes.SetKey(kek);
            return AesKeyWrapping.TryUnwrap(aes, wrapped, macKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <summary>Checks the authenticated attributes (ISO/TS 32004 6.3.6) and the MAC over them (RFC 5652 9.3).</summary>
    /// <param name="parts">The parts.</param>
    /// <param name="macKey">The MAC key.</param>
    /// <returns>Valid or the first failure.</returns>
    /// <exception cref="AsnContentException">The attributes are damaged.</exception>
    private static PdfMacTokenStatus CheckAttributes(ref PdfMacParts parts, scoped ReadOnlySpan<byte> macKey)
    {
        var attributes = ReadAttributes(parts.AuthenticatedAttributes);
        if (!attributes.HasContentType || !attributes.HasMessageDigest || !IsProtectionValid(attributes.Protection, ref parts))
        {
            return PdfMacTokenStatus.Malformed;
        }

        Span<byte> hash = stackalloc byte[CmsDigests.MaxLength];
        var length = CmsDigests.Hash(parts.Digest, parts.Content, hash);
        if (length == 0 || !CryptographicOperations.FixedTimeEquals(hash[..length], attributes.MessageDigest))
        {
            return PdfMacTokenStatus.ContentDigestMismatch;
        }

        // The MAC is over the attributes with a universal SET OF tag, in place of the [2] tag they are stored with.
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, macKey);
        hmac.AppendData([SetOfTag]);
        hmac.AppendData(parts.AuthenticatedAttributes[1..]);
        Span<byte> computed = stackalloc byte[MacLength];
        _ = hmac.GetHashAndReset(computed);
        return CryptographicOperations.FixedTimeEquals(computed, parts.Mac) ? PdfMacTokenStatus.Valid : PdfMacTokenStatus.MacMismatch;
    }

    /// <summary>Reads the content type, message digest and algorithm protection attributes, each at most once.</summary>
    /// <param name="attributes">The whole encoded <c>[2]</c> attributes.</param>
    /// <returns>The attributes found; an attribute that repeats or is damaged leaves the matching flag unset.</returns>
    /// <exception cref="AsnContentException">The attributes are damaged.</exception>
    private static PdfMacAttributes ReadAttributes(ReadOnlySpan<byte> attributes)
    {
        var found = default(PdfMacAttributes);
        var body = CmsDer.ReadContents(ref attributes, CmsDer.Constructed2);
        while (!body.IsEmpty)
        {
            var attribute = CmsDer.ReadContents(ref body, Asn1Tag.Sequence);
            var oid = CmsDer.ReadContents(ref attribute, Asn1Tag.ObjectIdentifier);
            var values = CmsDer.ReadContents(ref attribute, Asn1Tag.SetOf);
            if (!attribute.IsEmpty || !Accept(ref found, oid, values))
            {
                return default;
            }
        }

        return found;
    }

    /// <summary>Records an attribute when it is one of the three that checking uses.</summary>
    /// <param name="found">The attributes so far.</param>
    /// <param name="oid">The attribute type.</param>
    /// <param name="values">The attribute's value set.</param>
    /// <returns><see langword="false"/> when a known attribute repeats or does not have exactly one correct value.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    private static bool Accept(ref PdfMacAttributes found, ReadOnlySpan<byte> oid, ReadOnlySpan<byte> values)
    {
        if (PdfMacOids.Is(oid, PdfMacOids.ContentType))
        {
            var repeated = found.HasContentType;
            found.HasContentType = true;
            return !repeated && IsSingle(values, Asn1Tag.ObjectIdentifier, out var type) && PdfMacOids.Is(type, PdfMacOids.IntegrityInfo);
        }

        if (PdfMacOids.Is(oid, PdfMacOids.MessageDigest))
        {
            var repeated = found.HasMessageDigest;
            found.HasMessageDigest = true;
            var single = IsSingle(values, Asn1Tag.PrimitiveOctetString, out var digest);
            found.MessageDigest = digest;
            return !repeated && single;
        }

        if (PdfMacOids.Is(oid, PdfMacOids.AlgorithmProtection))
        {
            var repeated = found.HasProtection;
            found.HasProtection = true;
            var single = IsSingle(values, Asn1Tag.Sequence, out var protection);
            found.Protection = protection;
            return !repeated && single;
        }

        return true;
    }

    /// <summary>Reads a value set that holds exactly one value with a tag.</summary>
    /// <param name="values">The value set's contents.</param>
    /// <param name="tag">The tag the value must have.</param>
    /// <param name="contents">Receives the value's contents.</param>
    /// <returns><see langword="true"/> when the set holds that one value and nothing else.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    private static bool IsSingle(ReadOnlySpan<byte> values, Asn1Tag tag, out ReadOnlySpan<byte> contents)
    {
        contents = CmsDer.ReadContents(ref values, tag);
        return values.IsEmpty;
    }

    /// <summary>Checks the algorithm protection attribute, when there is one, names the same digest and MAC as the AuthenticatedData (ISO/TS 32004 6.3.6.4).</summary>
    /// <param name="protection">The attribute value's contents, or an empty span when absent.</param>
    /// <param name="parts">The parts.</param>
    /// <returns><see langword="true"/> when the attribute is absent or agrees.</returns>
    /// <exception cref="AsnContentException">The attribute is damaged.</exception>
    private static bool IsProtectionValid(ReadOnlySpan<byte> protection, ref PdfMacParts parts)
    {
        if (protection.IsEmpty)
        {
            return true;
        }

        var digest = CmsDer.ReadAlgorithm(ref protection, out _);
        if (!PdfMacOids.Is(digest, parts.DigestOid) || !CmsDer.StartsWith(protection, CmsDer.Constructed2))
        {
            return false;
        }

        var algorithm = CmsDer.ReadContents(ref protection, CmsDer.Constructed2);
        return protection.IsEmpty && PdfMacOids.Is(CmsDer.ReadContents(ref algorithm, Asn1Tag.ObjectIdentifier), PdfMacOids.HmacSha256) && IsAbsentOrNull(algorithm);
    }

    /// <summary>Reads the PdfMacIntegrityInfo (ISO/TS 32004 6.2) and finds its digests in the token.</summary>
    /// <param name="token">The token.</param>
    /// <param name="parts">The parts.</param>
    /// <returns>The result.</returns>
    /// <exception cref="AsnContentException">The content is damaged.</exception>
    private static PdfMacTokenResult ReadContent(ReadOnlySpan<byte> token, ref PdfMacParts parts)
    {
        var content = parts.Content;
        var info = CmsDer.ReadContents(ref content, Asn1Tag.Sequence);
        var length = CmsDigests.Length(parts.Digest);
        if (!content.IsEmpty || !IsZero(CmsDer.ReadContents(ref info, Asn1Tag.Integer)))
        {
            return PdfMacTokenResult.Failed(PdfMacTokenStatus.Malformed, parts.Digest);
        }

        var data = CmsDer.ReadContents(ref info, Asn1Tag.PrimitiveOctetString);
        var signature = CmsDer.StartsWith(info, CmsDer.Primitive0) ? CmsDer.ReadContents(ref info, CmsDer.Primitive0) : default;
        if (!info.IsEmpty || data.Length != length || (!signature.IsEmpty && signature.Length != length))
        {
            return PdfMacTokenResult.Failed(PdfMacTokenStatus.Malformed, parts.Digest);
        }

        return new(PdfMacTokenStatus.Valid, parts.Digest, Locate(token, data), signature.IsEmpty ? default : Locate(token, signature));
    }

    /// <summary>Gets where a slice lies in the token.</summary>
    /// <param name="token">The token.</param>
    /// <param name="part">A slice of it.</param>
    /// <returns>The range.</returns>
    private static Range Locate(ReadOnlySpan<byte> token, ReadOnlySpan<byte> part) =>
        token.Overlaps(part, out var at) ? new Range(at, at + part.Length) : default;

    /// <summary>Determines whether a wrapped key's length is one the key wrap makes: whole 8-byte blocks, at least two and the check value.</summary>
    /// <param name="length">The length.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsWrappedKeyLength(int length) =>
        length % AesKeyWrapping.BlockLength == 0 && length is >= MinWrappedKey and <= MaxWrappedKey;

    /// <summary>Determines whether an INTEGER's contents are zero.</summary>
    /// <param name="contents">The contents.</param>
    /// <returns><see langword="true"/> for the single byte zero.</returns>
    private static bool IsZero(ReadOnlySpan<byte> contents) => contents is [0];

    /// <summary>Determines whether algorithm parameters are absent or an ASN.1 NULL.</summary>
    /// <param name="parameters">The encoded parameters.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool IsAbsentOrNull(ReadOnlySpan<byte> parameters) =>
        parameters.IsEmpty || parameters is [(byte)UniversalTagNumber.Null, 0];
}
