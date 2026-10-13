// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Decrypts a CMS EncryptedContentInfo (RFC 5652 6.1) encrypted with AES-CBC (RFC 3565), the content ciphers the
/// managed key agreement path supports. ISO 32000-2 7.6.5.3 deprecates the others.
/// </summary>
internal static class CmsContentDecryption
{
    /// <summary>Gets the key length the content cipher needs.</summary>
    /// <param name="encryptedContentInfo">The EncryptedContentInfo's contents.</param>
    /// <returns>The key length in bytes, or zero when the cipher is not supported.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    internal static int KeyLength(ReadOnlySpan<byte> encryptedContentInfo)
    {
        _ = CmsDer.ReadContents(ref encryptedContentInfo, Asn1Tag.ObjectIdentifier);
        return CmsAlgorithms.AesCbcKeyLength(CmsDer.ReadAlgorithm(ref encryptedContentInfo, out _));
    }

    /// <summary>Decrypts the content with the content-encryption key.</summary>
    /// <param name="encryptedContentInfo">The EncryptedContentInfo's contents.</param>
    /// <param name="key">The content-encryption key.</param>
    /// <returns>The content, or a damaged result when the key, IV or padding is wrong.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    internal static CmsRecipientResult Decrypt(ReadOnlySpan<byte> encryptedContentInfo, ReadOnlySpan<byte> key)
    {
        _ = CmsDer.ReadContents(ref encryptedContentInfo, Asn1Tag.ObjectIdentifier);
        var oid = CmsDer.ReadAlgorithm(ref encryptedContentInfo, out var parameters);
        var iv = CmsDer.ReadContents(ref parameters, Asn1Tag.PrimitiveOctetString);
        var ciphertext = ReadCiphertext(encryptedContentInfo);
        if (CmsAlgorithms.AesCbcKeyLength(oid) != key.Length || iv.Length != PdfSecurityHandler.AesBlock
            || ciphertext.IsEmpty || ciphertext.Length % PdfSecurityHandler.AesBlock != 0)
        {
            return CmsRecipientResult.Damaged;
        }

        using var aes = Aes.Create();
        aes.SetKey(key);
        try
        {
            return CmsRecipientResult.Opened(aes.DecryptCbc(ciphertext, iv));
        }
        catch (CryptographicException)
        {
            // Bad padding: the content key is wrong or the content is damaged.
            return CmsRecipientResult.Damaged;
        }
    }

    /// <summary>Reads the <c>[0] IMPLICIT</c> encrypted content, which BER producers may split into a constructed string.</summary>
    /// <param name="source">What follows the content encryption algorithm.</param>
    /// <returns>The ciphertext, or an empty span when the content is absent.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    private static ReadOnlySpan<byte> ReadCiphertext(ReadOnlySpan<byte> source)
    {
        if (CmsDer.StartsWith(source, CmsDer.Primitive0))
        {
            return CmsDer.ReadContents(ref source, CmsDer.Primitive0);
        }

        return CmsDer.StartsWith(source, CmsDer.Constructed0)
            ? AsnDecoder.ReadOctetString(source, AsnEncodingRules.BER, out _, CmsDer.Primitive0)
            : default;
    }
}
