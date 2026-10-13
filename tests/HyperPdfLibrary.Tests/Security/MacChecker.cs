// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.RegularExpressions;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>
/// Checks the PDF MAC of every revision of a file as ISO/TS 32004:2024 describes, with the framework's ASN.1 reader and its own
/// HKDF and key unwrap, so a file the library saved is judged without the library's reader.
/// </summary>
internal static partial class MacChecker
{
    /// <summary>The numbers in a byte range.</summary>
    private const int RangeNumbers = 4;

    /// <summary>The hexadecimal digits per byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The length of a key wrap half block.</summary>
    private const int HalfBlock = 8;

    /// <summary>The number of rounds of RFC 3394 key wrap.</summary>
    private const int WrapRounds = 6;

    /// <summary>The byte every half block of the RFC 3394 initial value repeats.</summary>
    private const byte InitialByte = 0xA6;

    /// <summary>The DER tag byte of a SET OF, which the MAC input uses for the attributes.</summary>
    private const byte SetOfTag = 0x31;

    /// <summary>The identifier octet of the constructed <c>[2]</c> authenticated attributes.</summary>
    private const byte StoredAttributesTag = 0xA2;

    /// <summary>The context tag number of the explicit content and the KDF identifier.</summary>
    private const int ZeroContext = 0;

    /// <summary>The context tag number of the digest algorithm.</summary>
    private const int DigestContext = 1;

    /// <summary>The context tag number of the MAC algorithm in the protection attribute.</summary>
    private const int MacContext = 2;

    /// <summary>The context tag number of the password recipient.</summary>
    private const int RecipientContext = 3;

    /// <summary>The length of the wrapped HMAC-SHA-256 key.</summary>
    private const int WrappedLength = 40;

    /// <summary>The digests of Table 8, by name.</summary>
    private static readonly string[] Digests = ["SHA-256", "SHA-384", "SHA-512", "SHA3-256", "SHA3-384", "SHA3-512"];

    /// <summary>Checks every standalone token of a file, and every token attached to a signature.</summary>
    /// <param name="file">The file.</param>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="salt">The 32-byte salt.</param>
    /// <returns>One result for each token found.</returns>
    internal static IReadOnlyList<MacCheck> Check(byte[] file, byte[] fileKey, byte[] salt)
    {
        var text = Encoding.Latin1.GetString(file);
        var checks = new List<MacCheck>();
        var standalone = StandaloneAuthCode().Matches(text);
        for (var i = 0; i < standalone.Count; i++)
        {
            checks.Add(new(standalone[i].Index, "Standalone", CheckStandalone(file, text, standalone[i], fileKey, salt)));
        }

        var attached = AttachedAuthCode().Matches(text);
        for (var i = 0; i < attached.Count; i++)
        {
            checks.Add(new(attached[i].Index, "AttachedToSig", CheckAttached(file, text, attached[i], fileKey, salt)));
        }

        return checks;
    }

    /// <summary>Checks a token: its structure, the key, the attributes, the MAC and the digests of the bytes it covers.</summary>
    /// <param name="token">The DER token.</param>
    /// <param name="fileKey">The file key.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="covered">The bytes the data digest covers.</param>
    /// <param name="signature">The signature value for a token attached to a signature, or <see langword="null"/>.</param>
    /// <returns>Empty text when it holds, otherwise the reason.</returns>
    internal static string VerifyToken(byte[] token, byte[] fileKey, byte[] salt, byte[] covered, byte[]? signature)
    {
        try
        {
            var parts = Read(token);
            var macKey = parts.Failure.Length > 0 ? null : Unwrap(MacToken.Hkdf(fileKey, salt, "PDFMAC"u8.ToArray(), MacToken.KeyLength), parts.Wrapped);
            if (parts.Failure.Length > 0 || macKey is null)
            {
                return parts.Failure.Length > 0 ? parts.Failure : "The wrapped MAC key does not unwrap.";
            }

            var input = parts.Attributes.ToArray();
            input[0] = SetOfTag;
            return CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(macKey, input), parts.Mac)
                ? CheckContent(parts, covered, signature)
                : "The MAC does not match the attributes.";
        }
        catch (Exception exception) when (exception is AsnContentException or CryptographicException or InvalidOperationException)
        {
            return $"The token cannot be read: {exception.Message}";
        }
    }

    /// <summary>Matches a standalone /AuthCode dictionary.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"/AuthCode\s*<<\s*/MAC\s*<(?<mac>[^>]*)>\s*/ByteRange\s*\[\s*(?<range>[\d\s]+)\]\s*/MACLocation\s*/Standalone\s*>>")]
    private static partial Regex StandaloneAuthCode();

    /// <summary>Matches an /AuthCode dictionary attached to a signature.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"/AuthCode\s*<<\s*/MACLocation\s*/AttachedToSig\s*/SigObjRef\s*(?<number>\d+)\s+0\s+R\s*>>")]
    private static partial Regex AttachedAuthCode();

    /// <summary>Checks a standalone token against its revision.</summary>
    /// <param name="file">The file.</param>
    /// <param name="text">The file as Latin-1 text.</param>
    /// <param name="match">The /AuthCode match.</param>
    /// <param name="fileKey">The file key.</param>
    /// <param name="salt">The salt.</param>
    /// <returns>Empty text when it holds, otherwise the reason.</returns>
    private static string CheckStandalone(byte[] file, string text, Match match, byte[] fileKey, byte[] salt)
    {
        var numbers = match.Groups["range"].Value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(static number => long.Parse(number, CultureInfo.InvariantCulture))
            .ToArray();
        var revisionEnd = RevisionEnd(text, match.Index);
        if (numbers.Length != RangeNumbers || numbers[0] != 0 || numbers[2] <= numbers[1] || numbers[2] + numbers[3] != revisionEnd)
        {
            return $"The byte range {string.Join(' ', numbers)} does not cover the revision, which ends at {revisionEnd}.";
        }

        var hex = match.Groups["mac"].Value;
        var gap = Encoding.Latin1.GetString(file, (int)numbers[1], (int)(numbers[2] - numbers[1]));
        if (gap != $"<{hex}>" || hex.Length % DigitsPerByte != 0 || !hex.All(char.IsAsciiHexDigit))
        {
            return "The /MAC entry is not the exact hexadecimal string in the gap of the byte range.";
        }

        byte[] covered = [.. file.AsSpan(0, (int)numbers[1]), .. file.AsSpan((int)numbers[2], (int)numbers[3])];
        return VerifyToken(Convert.FromHexString(hex), fileKey, salt, covered, null);
    }

    /// <summary>Checks a token attached to a signature.</summary>
    /// <param name="file">The file.</param>
    /// <param name="text">The file as Latin-1 text.</param>
    /// <param name="match">The /AuthCode match.</param>
    /// <param name="fileKey">The file key.</param>
    /// <param name="salt">The salt.</param>
    /// <returns>Empty text when it holds, otherwise the reason.</returns>
    private static string CheckAttached(byte[] file, string text, Match match, byte[] fileKey, byte[] salt)
    {
        var start = text.IndexOf(string.Create(CultureInfo.InvariantCulture, $"\n{match.Groups["number"].Value} 0 obj"), StringComparison.Ordinal);
        var contents = text.IndexOf(MacPdf.ContentsMarker, start, StringComparison.Ordinal) + MacPdf.ContentsMarker.Length;
        var digits = text.IndexOf('>', contents) - contents;
        var container = Convert.FromHexString(text.AsSpan(contents, digits)).AsSpan().TrimEnd((byte)0).ToArray();
        var after = contents + digits + 1;
        byte[] covered = [.. file.AsSpan(0, contents - 1), .. file.AsSpan(after, (int)RevisionEnd(text, contents) - after)];
        var cms = new SignedCms(new(covered), true);
        cms.Decode(container);
        var attribute = cms.SignerInfos[0].UnsignedAttributes.Cast<CryptographicAttributeObject>().FirstOrDefault(static a => a.Oid.Value == MacToken.MacDataOid);
        return attribute is null
            ? "The signature has no PDF MAC attribute."
            : VerifyToken(attribute.Values[0].RawData, fileKey, salt, covered, cms.SignerInfos[0].GetSignature());
    }

    /// <summary>Finds where the revision that holds a position ends: after the next end-of-file marker and its line end.</summary>
    /// <param name="text">The file as Latin-1 text.</param>
    /// <param name="position">A position in the revision.</param>
    /// <returns>The position after the marker's line end.</returns>
    private static long RevisionEnd(string text, int position)
    {
        var end = text.IndexOf("%%EOF", position, StringComparison.Ordinal) + "%%EOF".Length;
        end += text[end] == '\r' ? 1 : 0;
        return end + (text[end] == '\n' ? 1 : 0);
    }

    /// <summary>Checks the content of a token whose MAC holds: the message digest and the digests of the document and signature.</summary>
    /// <param name="parts">The token's parts.</param>
    /// <param name="covered">The bytes the data digest covers.</param>
    /// <param name="signature">The signature value, or <see langword="null"/>.</param>
    /// <returns>Empty text when it holds, otherwise the reason.</returns>
    private static string CheckContent(MacParts parts, byte[] covered, byte[]? signature)
    {
        if (!CryptographicOperations.FixedTimeEquals(parts.MessageDigest, MacToken.Hash(parts.Digest, parts.Content)))
        {
            return "The message digest attribute does not match the content.";
        }

        var info = new AsnReader(parts.Content, AsnEncodingRules.BER).ReadSequence();
        var versionIsZero = info.ReadInteger() == BigInteger.Zero;
        var data = info.ReadOctetString();
        var signed = info.HasData ? info.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, ZeroContext)) : null;
        if (!versionIsZero || !CryptographicOperations.FixedTimeEquals(data, MacToken.Hash(parts.Digest, covered)))
        {
            return "The data digest does not match the bytes the byte range covers.";
        }

        var signatureOk = signature is null
            ? signed is null
            : signed is not null && CryptographicOperations.FixedTimeEquals(signed, MacToken.Hash(parts.Digest, signature));
        return signatureOk ? string.Empty : "The signature digest is missing, unexpected or wrong.";
    }

    /// <summary>Reads the structure of a token as ISO/TS 32004 6.3 requires, one field at a time.</summary>
    /// <param name="token">The DER token.</param>
    /// <returns>The parts, or a failure sentence.</returns>
    private static MacParts Read(byte[] token)
    {
        var outer = new AsnReader(token, AsnEncodingRules.BER);
        var info = outer.ReadSequence();
        outer.ThrowIfNotEmpty();
        if (info.ReadObjectIdentifier() != MacToken.AuthDataOid)
        {
            return MacParts.Fail("The token is not authenticated data.");
        }

        var data = info.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, ZeroContext)).ReadSequence();
        if (data.ReadInteger() != BigInteger.Zero)
        {
            return MacParts.Fail("The version is not 0.");
        }

        var recipients = data.ReadSetOf();
        var recipient = recipients.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, RecipientContext, true));
        recipients.ThrowIfNotEmpty();
        return ReadRecipient(recipient) is { } wrapped
            ? ReadRest(data, wrapped)
            : MacParts.Fail("The recipient is not one PasswordRecipientInfo with pdfMacWrapKdf and AES-256 key wrap.");
    }

    /// <summary>Reads the PasswordRecipientInfo.</summary>
    /// <param name="recipient">The recipient's fields.</param>
    /// <returns>The wrapped key, or <see langword="null"/> when the recipient does not follow the standard.</returns>
    private static byte[]? ReadRecipient(AsnReader recipient)
    {
        _ = recipient.ReadInteger();
        var kdf = recipient.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, ZeroContext, true));
        var wrap = recipient.ReadSequence();
        var key = recipient.ReadOctetString();
        recipient.ThrowIfNotEmpty();
        var ok = kdf.ReadObjectIdentifier() == MacToken.WrapKdfOid && !kdf.HasData && wrap.ReadObjectIdentifier() == MacToken.Aes256WrapOid && !wrap.HasData;
        return ok && key.Length == WrappedLength ? key : null;
    }

    /// <summary>Reads the MAC algorithm, digest, content, attributes and MAC, and checks the attributes.</summary>
    /// <param name="data">The AuthenticatedData's remaining fields.</param>
    /// <param name="wrapped">The wrapped key.</param>
    /// <returns>The parts, or a failure sentence.</returns>
    private static MacParts ReadRest(AsnReader data, byte[] wrapped)
    {
        var hmac = data.ReadSequence();
        var algorithmId = data.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, DigestContext, true)).ReadObjectIdentifier();
        var digest = Array.Find(Digests, name => string.Equals(MacToken.AlgorithmIdentifier(name), algorithmId, StringComparison.Ordinal));
        var encapsulated = data.ReadSequence();
        var contentOk = encapsulated.ReadObjectIdentifier() == MacToken.IntegrityInfoOid;
        var content = encapsulated.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, ZeroContext)).ReadOctetString();
        var attributes = data.ReadEncodedValue().ToArray();
        var mac = data.ReadOctetString();
        data.ThrowIfNotEmpty();
        if (hmac.ReadObjectIdentifier() != MacToken.HmacSha256Oid || digest is null || !contentOk || attributes[0] != StoredAttributesTag)
        {
            return MacParts.Fail("The MAC, digest, content type or attributes tag is not as the standard requires.");
        }

        var sorted = attributes.ToArray();
        sorted[0] = SetOfTag;
        return ReadAttributes(sorted, algorithmId) is { } found
            ? new(string.Empty, wrapped, attributes, mac, digest, content, found)
            : MacParts.Fail("The attributes are not exactly a content type, a message digest and a matching algorithm protection.");
    }

    /// <summary>Reads the three authenticated attributes.</summary>
    /// <param name="attributes">The attributes with a SET OF tag.</param>
    /// <param name="algorithmId">The digest identifier of the AuthenticatedData.</param>
    /// <returns>The message digest, or <see langword="null"/> when the attributes are not as the standard requires.</returns>
    private static byte[]? ReadAttributes(byte[] attributes, string algorithmId)
    {
        var set = new AsnReader(attributes, AsnEncodingRules.BER).ReadSetOf();
        byte[]? messageDigest = null;
        var contentType = false;
        var protection = false;
        while (set.HasData)
        {
            var attribute = set.ReadSequence();
            var oid = attribute.ReadObjectIdentifier();
            var values = attribute.ReadSetOf();
            if (oid == MacToken.ContentTypeOid)
            {
                contentType = values.ReadObjectIdentifier() == MacToken.IntegrityInfoOid;
            }
            else if (oid == MacToken.MessageDigestOid)
            {
                messageDigest = values.ReadOctetString();
            }
            else if (oid == MacToken.AlgorithmProtectionOid)
            {
                var value = values.ReadSequence();
                protection = value.ReadSequence().ReadObjectIdentifier() == algorithmId
                    && value.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, MacContext, true)).ReadObjectIdentifier() == MacToken.HmacSha256Oid;
            }
        }

        return contentType && protection ? messageDigest : null;
    }

    /// <summary>Unwraps a key as RFC 3394 2.2.2 describes.</summary>
    /// <param name="kek">The key-encryption key.</param>
    /// <param name="wrapped">The wrapped key.</param>
    /// <returns>The key, or <see langword="null"/> when the check value is wrong.</returns>
    private static byte[]? Unwrap(byte[] kek, byte[] wrapped)
    {
        using var aes = Aes.Create();
        aes.Key = kek;
        var blocks = (wrapped.Length / HalfBlock) - 1;
        var a = wrapped[..HalfBlock];
        var r = Enumerable.Range(0, blocks).Select(i => wrapped.AsSpan((i + 1) * HalfBlock, HalfBlock).ToArray()).ToArray();
        for (var j = WrapRounds - 1; j >= 0; j--)
        {
            for (var i = blocks; i >= 1; i--)
            {
                var t = new byte[HalfBlock];
                BinaryPrimitives.WriteUInt64BigEndian(t, (ulong)((blocks * j) + i));
                var b = aes.DecryptEcb([.. a.Select((x, k) => (byte)(x ^ t[k])), .. r[i - 1]], PaddingMode.None);
                a = b[..HalfBlock];
                r[i - 1] = b[HalfBlock..];
            }
        }

        return Array.TrueForAll(a, static x => x == InitialByte) ? [.. r.SelectMany(static block => block)] : null;
    }

    /// <summary>The parts of a token that checking needs.</summary>
    /// <param name="Failure">Why the token cannot be read, or empty text.</param>
    /// <param name="Wrapped">The wrapped MAC key.</param>
    /// <param name="Attributes">The authenticated attributes as stored, with the <c>[2]</c> tag.</param>
    /// <param name="Mac">The MAC.</param>
    /// <param name="Digest">The digest name.</param>
    /// <param name="Content">The encapsulated PdfMacIntegrityInfo.</param>
    /// <param name="MessageDigest">The message digest attribute.</param>
    private sealed record MacParts(string Failure, byte[] Wrapped, byte[] Attributes, byte[] Mac, string Digest, byte[] Content, byte[] MessageDigest)
    {
        /// <summary>Creates the parts of a token that could not be read.</summary>
        /// <param name="failure">Why.</param>
        /// <returns>The parts.</returns>
        internal static MacParts Fail(string failure) => new(failure, [], [], [], string.Empty, [], []);
    }
}
