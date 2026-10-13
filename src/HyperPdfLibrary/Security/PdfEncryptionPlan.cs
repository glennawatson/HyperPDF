// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Security;

/// <summary>
/// The encryption a save is about to apply: the new security handler that encrypts every string and stream as it is written,
/// the <c>/Encrypt</c> dictionary for the trailer, and the first part of the file id the keys depend on. One plan serves one
/// write, because it holds the keys made for it.
/// </summary>
[DebuggerDisplay("PdfEncryptionPlan: R{Handler.Revision}")]
internal sealed class PdfEncryptionPlan : IDisposable
{
    /// <summary>The length of a file id part made when the document has none.</summary>
    private const int IdLength = 16;

    /// <summary>The oldest version AES-128 crypt filters (AESV2) need.</summary>
    private const int Pdf16Minor = 6;

    /// <summary>Initializes a new instance of the <see cref="PdfEncryptionPlan"/> class.</summary>
    /// <param name="handler">The new security handler.</param>
    /// <param name="encrypt">The <c>/Encrypt</c> dictionary.</param>
    /// <param name="fileId">The trailer used for <c>/ID</c>.</param>
    /// <param name="minimumVersion">The oldest version the file may declare.</param>
    private PdfEncryptionPlan(PdfSecurityHandler handler, PdfDictionary encrypt, PdfDictionary fileId, PdfVersion minimumVersion)
    {
        Handler = handler;
        Encrypt = encrypt;
        FileId = fileId;
        MinimumVersion = minimumVersion;
    }

    /// <summary>Gets the handler that encrypts what is written.</summary>
    internal PdfSecurityHandler Handler { get; }

    /// <summary>Gets the <c>/Encrypt</c> dictionary for the trailer. Its strings are never encrypted.</summary>
    internal PdfDictionary Encrypt { get; }

    /// <summary>Gets a dictionary holding only the <c>/ID</c> whose first part the keys were made from.</summary>
    internal PdfDictionary FileId { get; }

    /// <summary>Gets the oldest version the file may declare: 1.6 for AES-128 and 2.0 for AES-256 and AES-GCM.</summary>
    internal PdfVersion MinimumVersion { get; }

    /// <summary>Gets a value indicating whether the catalog shall declare the ISO/TS 32003 extension.</summary>
    internal bool UsesAesGcm => Handler.UsesAesGcm;

    /// <summary>Releases the handler.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Handler.Dispose();

    /// <summary>Makes the plan for a save, if the save is to encrypt.</summary>
    /// <param name="store">The document being saved.</param>
    /// <param name="options">The save options.</param>
    /// <returns>The plan, or <see langword="null"/> when <see cref="PdfCompactOptions.Encryption"/> is not set.</returns>
    /// <exception cref="ArgumentException">The options contradict each other.</exception>
    /// <exception cref="PdfException">The document cannot be encrypted, with the reason in plain words.</exception>
    internal static PdfEncryptionPlan? Create(PdfObjectStore store, in PdfCompactOptions options)
    {
        if (options.Encryption is not { } encryption)
        {
            return null;
        }

        PdfEncryptionRules.Check(store, options, encryption);
        var fill = encryption.RandomFill ?? RandomNumberGenerator.Fill;
        var firstId = FirstId(store, fill);
        var idTrailer = new PdfDictionary(null);
        var id = PdfValue.FromString(firstId);
        idTrailer.Set(KnownName.ID, PdfValue.FromArray(new(null, [id, id])));
        return encryption.Recipients is null
            ? CreateStandard(store.Names, encryption, firstId, idTrailer, fill)
            : CreatePublicKey(store.Names, encryption, idTrailer, fill);
    }

    /// <summary>Makes the plan of a password file.</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="encryption">The encryption options.</param>
    /// <param name="firstId">The first part of the file id.</param>
    /// <param name="idTrailer">The dictionary holding the file id.</param>
    /// <param name="fill">The random source.</param>
    /// <returns>The plan.</returns>
    private static PdfEncryptionPlan CreateStandard(PdfNameTable names, PdfEncryptionOptions encryption, byte[] firstId, PdfDictionary idTrailer, FillBytes fill)
    {
        var algorithm = encryption.Algorithm;
        var revision = RevisionOf(algorithm);

        // PDF 2.0 says bit 10 is always set; an AES-128 file follows ISO 32000-1 and may clear it.
        var permissions = PdfPermissionBits.ToStandard(encryption.Permissions, algorithm == PdfEncryptionAlgorithm.Aes128);
        var entries = StandardEncryptionWriter.Create(revision, encryption.UserPassword, encryption.OwnerPassword, permissions, encryption.EncryptMetadata, firstId, fill);
        var dictionary = PdfEncryptionDictionaries.Standard(names, algorithm, entries, encryption.EncryptMetadata);
        var method = MethodOf(algorithm);
        var handler = PdfSecurityHandler.CreateNew(entries.FileKey, revision, new(method, method), permissions, encryption.EncryptMetadata, dictionary.GetDictionary(KnownName.CF)!);
        return new(handler, dictionary, idTrailer, MinimumVersionOf(algorithm));
    }

    /// <summary>Makes the plan of a public-key file. The file key comes from a random seed, not from the file id.</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="encryption">The encryption options.</param>
    /// <param name="idTrailer">The dictionary holding the file id.</param>
    /// <param name="fill">The random source.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="PdfException">A recipient's certificate cannot receive an encrypted file.</exception>
    private static PdfEncryptionPlan CreatePublicKey(PdfNameTable names, PdfEncryptionOptions encryption, PdfDictionary idTrailer, FillBytes fill)
    {
        var algorithm = encryption.Algorithm;
        var entries = PublicKeyEncryptionWriter.Create(encryption.Recipients!, algorithm, encryption.EncryptMetadata, fill);
        var dictionary = PdfEncryptionDictionaries.PublicKey(names, algorithm, entries, encryption.EncryptMetadata);
        var method = MethodOf(algorithm);
        var filters = dictionary.GetDictionary(KnownName.CF)!;
        var version = PdfEncryptionDictionaries.VersionOf(algorithm);
        var handler = PdfSecurityHandler.CreateNew(entries.FileKey, version, new(method, method), entries.Permissions, encryption.EncryptMetadata, filters);
        return new(handler, dictionary, idTrailer, MinimumVersionOf(algorithm));
    }

    /// <summary>Gets the first part of the file id: the document's own, which the keys may already depend on, else a new random one.</summary>
    /// <param name="store">The document.</param>
    /// <param name="fill">The random source.</param>
    /// <returns>The bytes.</returns>
    private static byte[] FirstId(PdfObjectStore store, FillBytes fill)
    {
        var first = store.Trailer.GetArray(KnownName.ID)?.Get(0) ?? default;
        if (first.Kind == PdfKind.String && !first.AsStringBytes().IsEmpty)
        {
            return first.AsStringBytes().ToArray();
        }

        var created = new byte[IdLength];
        fill(created);
        return created;
    }

    /// <summary>Gets the standard handler revision of a cipher.</summary>
    /// <param name="algorithm">The cipher.</param>
    /// <returns>4, 6 or 7.</returns>
    private static int RevisionOf(PdfEncryptionAlgorithm algorithm) => algorithm switch
    {
        PdfEncryptionAlgorithm.Aes128 => StandardEncryptionWriter.Revision4,
        PdfEncryptionAlgorithm.Aes256 => StandardEncryptionWriter.Revision6,
        _ => StandardEncryptionWriter.Revision7,
    };

    /// <summary>Gets the cipher the handler uses for strings and streams.</summary>
    /// <param name="algorithm">The cipher.</param>
    /// <returns>The method.</returns>
    private static CryptMethod MethodOf(PdfEncryptionAlgorithm algorithm) => algorithm switch
    {
        PdfEncryptionAlgorithm.Aes128 => CryptMethod.Aes128,
        PdfEncryptionAlgorithm.Aes256 => CryptMethod.Aes256,
        _ => CryptMethod.AesGcm,
    };

    /// <summary>Gets the oldest version a cipher is allowed in: AESV2 is PDF 1.6; AESV3 and AESV4 are PDF 2.0.</summary>
    /// <param name="algorithm">The cipher.</param>
    /// <returns>The version.</returns>
    private static PdfVersion MinimumVersionOf(PdfEncryptionAlgorithm algorithm) =>
        algorithm == PdfEncryptionAlgorithm.Aes128 ? new(1, Pdf16Minor) : PdfVersion.Pdf20;
}
