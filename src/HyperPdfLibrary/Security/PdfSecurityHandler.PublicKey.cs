// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Objects;
using static HyperPdfLibrary.Security.PdfPasswordAlgorithms;

namespace HyperPdfLibrary.Security;

/// <content>
/// The public-key security handler (<c>/Filter /Adobe.PubSec</c>, ISO 32000-2 7.6.5): the file key comes from a seed
/// that each recipient's CMS EnvelopedData carries, decrypted with the recipient's certificate and private key.
/// </content>
public sealed partial class PdfSecurityHandler
{
    /// <summary>The most recipients read.</summary>
    private const int MaxRecipients = 256;

    /// <summary>The longest recipient (CMS EnvelopedData) accepted, in bytes.</summary>
    private const int MaxRecipientLength = 1 << 20;

    /// <summary>Bit 2 of the public-key permission word: permits change of encryption (ISO 32000-2 Table 24).</summary>
    private const int ChangeEncryptionBit = 2;

    /// <summary>
    /// Creates the handler for any supported security handler: the standard handler with a password, or the public-key
    /// handler with a certificate.
    /// </summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="firstId">The first part of the trailer /ID.</param>
    /// <param name="password">The password for the standard handler, or <see langword="null"/>.</param>
    /// <param name="certificate">A recipient's certificate with its private key for the public-key handler, or <see langword="null"/>.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="encrypt"/> is <see langword="null"/>.</exception>
    public static PdfSecurityResult TryCreate(PdfDictionary encrypt, ReadOnlySpan<byte> firstId, string? password, X509Certificate2? certificate, out PdfSecurityHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        return IsPublicKey(encrypt) ? TryCreatePublicKey(encrypt, certificate, out handler) : TryCreate(encrypt, firstId, password, out handler);
    }

    /// <summary>
    /// Determines whether an /Encrypt dictionary uses the public-key handler: a handler other than the standard one whose
    /// dictionary or default crypt filter lists /Recipients, as <c>Adobe.PubSec</c> and compatible handlers do.
    /// </summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <returns><see langword="true"/> for a public-key handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="encrypt"/> is <see langword="null"/>.</exception>
    public static bool IsPublicKey(PdfDictionary encrypt)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        if (encrypt.IsName(KnownName.Filter, KnownName.Standard))
        {
            return false;
        }

        var filter = encrypt.GetDictionary(KnownName.CF)?.GetDictionary(encrypt.GetName(KnownName.StmF));
        return encrypt.ContainsKey(KnownName.Recipients) || filter?.ContainsKey(KnownName.Recipients) == true;
    }

    /// <summary>
    /// Creates the public-key handler (sub-filters <c>adbe.pkcs7.s3</c>, <c>s4</c> and <c>s5</c>). Recipients may use RSA
    /// key transport or ephemeral-static ECDH key agreement; the first recipient list that opens gives the permissions.
    /// </summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="certificate">A recipient's certificate with its RSA or elliptic curve private key, or <see langword="null"/>.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>
    /// Success; <see cref="PdfSecurityResult.CertificateRequired"/> when no certificate is given or none of the recipients is
    /// for it; <see cref="PdfSecurityResult.PrivateKeyRequired"/> when a recipient is for a certificate without its private
    /// key; <see cref="PdfSecurityResult.UnsupportedRecipient"/> when the recipient's key type or algorithms are not
    /// supported; <see cref="PdfSecurityResult.RecipientDamaged"/> when the recipient does not decrypt or no recipient list
    /// can be read; <see cref="PdfSecurityResult.Damaged"/> when /Recipients is missing or too large.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="encrypt"/> is <see langword="null"/>.</exception>
    public static PdfSecurityResult TryCreatePublicKey(PdfDictionary encrypt, X509Certificate2? certificate, out PdfSecurityHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        handler = null;
        var version = encrypt.GetInt32(KnownName.V);
        var methods = ReadMethods(encrypt, version);
        var filter = version >= Revision4 ? encrypt.GetDictionary(KnownName.CF)?.GetDictionary(encrypt.GetName(KnownName.StmF)) : null;
        var keyLength = PublicKeyLength(encrypt, filter, version, methods);
        if (keyLength == 0)
        {
            return PdfSecurityResult.UnsupportedHandler;
        }

        if (ReadRecipients(encrypt, filter) is not { } recipients)
        {
            return PdfSecurityResult.Damaged;
        }

        var opened = certificate is null ? CmsRecipientResult.NotForCertificate : CmsRecipientOpening.Open(recipients, certificate);
        if (opened.Content is not { Length: >= PublicKeyFileKey.SeedLength + PublicKeyFileKey.PermissionsLength } envelope)
        {
            return RecipientFailure(opened.Outcome);
        }

        var encryptMetadata = (filter ?? encrypt).GetBoolean(KnownName.EncryptMetadata, true);
        var key = PublicKeyFileKey.Derive(envelope.AsSpan(0, PublicKeyFileKey.SeedLength), recipients, encryptMetadata || version < Revision4, keyLength);
        var permissions = BinaryPrimitives.ReadInt32BigEndian(envelope.AsSpan(PublicKeyFileKey.SeedLength));
        handler = new(key, encrypt.GetInt32(KnownName.R, version), methods, permissions, encryptMetadata)
        {
            CryptFilters = version >= Revision4 ? encrypt.GetDictionary(KnownName.CF) : null,

            Role = RoleOf(permissions),
        };
        return PdfSecurityResult.Success;
    }

    /// <summary>Gets who a recipient is: Table 24 bit 2 permits change of encryption and enables all other permissions, which makes the owner.</summary>
    /// <param name="permissions">The recipient list's permission word.</param>
    /// <returns>The role.</returns>
    private static PdfSecurityRole RoleOf(int permissions) =>
        (permissions & ChangeEncryptionBit) != 0 ? PdfSecurityRole.Owner : PdfSecurityRole.User;

    /// <summary>Gets the file key length in bytes: 32 for AES-256, else from /Length, checked against the ciphers.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="filter">The default crypt filter, or <see langword="null"/>.</param>
    /// <param name="version">The /V value.</param>
    /// <param name="methods">The string and stream methods.</param>
    /// <returns>The length, or zero when the ciphers do not support it.</returns>
    private static int PublicKeyLength(PdfDictionary encrypt, PdfDictionary? filter, int version, CryptMethods methods)
    {
        var aes256 = methods.Streams == CryptMethod.Aes256 || methods.Strings == CryptMethod.Aes256;
        var keyLength = aes256 ? Sha256Length : ReadPublicKeyLength(encrypt, filter, version);
        return IsValidKeyLength(methods, keyLength, aes256 ? Revision5 : Revision4) ? keyLength : 0;
    }

    /// <summary>Reads the key length in bytes: the crypt filter's /Length from version 4, else /Length, defaulting to 40 bits or 128 for AES.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="filter">The default crypt filter, or <see langword="null"/>.</param>
    /// <param name="version">The /V value.</param>
    /// <returns>The length in bytes, or zero when not valid.</returns>
    private static int ReadPublicKeyLength(PdfDictionary encrypt, PdfDictionary? filter, int version)
    {
        var fallback = version >= Revision4 ? DefaultV4KeyBits : DefaultKeyBits;
        var bits = filter?.GetInt32(KnownName.Length, 0) ?? 0;
        bits = bits > 0 ? bits : encrypt.GetInt32(KnownName.Length, fallback);
        if (bits <= 0)
        {
            return 0;
        }

        // A value under 40 is a length in bytes.
        return (bits < DefaultKeyBits ? bits * ByteBits : bits) / ByteBits;
    }

    /// <summary>
    /// Reads the recipients: the default crypt filter's /Recipients from version 4, else the dictionary's. Each is the
    /// DER of a CMS EnvelopedData.
    /// </summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="filter">The default crypt filter, or <see langword="null"/>.</param>
    /// <returns>The recipients, or <see langword="null"/> when there are none, too many, or one is not a string or is too long.</returns>
    private static byte[][]? ReadRecipients(PdfDictionary encrypt, PdfDictionary? filter)
    {
        var value = filter?.Get(KnownName.Recipients) ?? default;
        value = value.IsNull ? encrypt.Get(KnownName.Recipients) : value;
        if (value.Kind == PdfKind.String)
        {
            return IsRecipient(value) ? [value.AsStringBytes().ToArray()] : null;
        }

        if (value.AsArray() is not { Count: > 0 and <= MaxRecipients } array)
        {
            return null;
        }

        var recipients = new byte[array.Count][];
        for (var i = 0; i < recipients.Length; i++)
        {
            var item = array.Get(i);
            if (!IsRecipient(item))
            {
                return null;
            }

            recipients[i] = item.AsStringBytes().ToArray();
        }

        return recipients;
    }

    /// <summary>Determines whether a value can be a recipient: a non-empty string within the size limit.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when usable.</returns>
    private static bool IsRecipient(PdfValue value) =>
        value.Kind == PdfKind.String && value.AsStringBytes().Length is > 0 and <= MaxRecipientLength;

    /// <summary>Maps a recipient list outcome that gave no usable seed to the handler result.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The result.</returns>
    private static PdfSecurityResult RecipientFailure(CmsRecipientOutcome outcome) => outcome switch
    {
        CmsRecipientOutcome.NotForCertificate => PdfSecurityResult.CertificateRequired,
        CmsRecipientOutcome.PrivateKeyMissing => PdfSecurityResult.PrivateKeyRequired,
        CmsRecipientOutcome.Unsupported => PdfSecurityResult.UnsupportedRecipient,

        // A decrypted envelope too short for the seed and permissions is damaged too.
        _ => PdfSecurityResult.RecipientDamaged,
    };
}
