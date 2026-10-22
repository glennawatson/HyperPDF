// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdfLibrary.Objects;
using static HyperPdfLibrary.Security.PdfPasswordAlgorithms;

namespace HyperPdfLibrary.Security;

/// <summary>
/// The standard security handler: works out the file key from a password and decrypts (and encrypts) strings and
/// streams with RC4, AES-128 or AES-256 (revisions 2 to 6). Hashes use one-shot APIs on stack buffers, and AES instances
/// are pooled and reused for every object.
/// </summary>
[DebuggerDisplay("PdfSecurityHandler: R{Revision} {StreamMethod}")]
public sealed partial class PdfSecurityHandler : IDisposable
{
    /// <summary>The AES block size.</summary>
    internal const int AesBlock = 16;

    /// <summary>The idle AES instances kept; a call that finds every slot full disposes its instance.</summary>
    private const int AesSlotCount = 16;

    /// <summary>The bytes of the object number and generation mixed into an object key.</summary>
    private const int ObjectSaltLength = 5;

    /// <summary>The length of the "sAlT" suffix of AES object keys.</summary>
    private const int AesSaltLength = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The shift of an object number's third byte.</summary>
    private const int ThirdByteShift = 16;

    /// <summary>The position of an object number's third byte in the object salt.</summary>
    private const int ThirdNumberByte = 2;

    /// <summary>The position of the generation in the object salt.</summary>
    private const int GenerationOffset = 3;

    /// <summary>Revision 2.</summary>
    private const int Revision2 = 2;

    /// <summary>Version 2, the first that has a variable key length.</summary>
    private const int Version2 = 2;

    /// <summary>The default key length in bits for version 4.</summary>
    private const int DefaultV4KeyBits = 128;

    /// <summary>Revision 4, the last RC4/AES-128 revision.</summary>
    private const int Revision4 = 4;

    /// <summary>Revision 5.</summary>
    private const int Revision5 = 5;

    /// <summary>Revision 6.</summary>
    private const int Revision6 = 6;

    /// <summary>Revision 7 (ISO/TS 32003), which uses the revision 6 password algorithms with AES-GCM.</summary>
    private const int Revision7 = 7;

    /// <summary>Version 6 (ISO/TS 32003), the version that goes with revision 7.</summary>
    private const int Version6 = 6;

    /// <summary>The file key.</summary>
    private readonly byte[] _fileKey;

    /// <summary>
    /// The AES instances not in use, in slots a call empties with an exchange and fills again with a compare-exchange. An
    /// instance is not thread safe, so each call takes one, keys it, and puts it back; calls on different threads do not wait.
    /// </summary>
    private readonly Aes?[] _aesSlots = new Aes?[AesSlotCount];

    /// <summary>Guards <see cref="_gcm"/> and disposal.</summary>
    private readonly Lock _gate = new();

    /// <summary>The AES-GCM instance, created on first use; guarded by <see cref="_gate"/>.</summary>
    private AesGcm? _gcm;

    /// <summary>The AES-GCM encryptions done with the file key by this handler; counted with <see cref="Interlocked"/>.</summary>
    private long _gcmEncryptions;

    /// <summary>1 once disposed; written under <see cref="_gate"/>.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PdfSecurityHandler"/> class.</summary>
    /// <param name="fileKey">The file key.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="methods">How strings and streams are encrypted.</param>
    /// <param name="permissions">The permission flags.</param>
    /// <param name="encryptMetadata">Whether metadata streams are encrypted.</param>
    private PdfSecurityHandler(byte[] fileKey, int revision, CryptMethods methods, int permissions, bool encryptMetadata)
    {
        _fileKey = fileKey;
        Revision = revision;
        StringMethod = methods.Strings;
        StreamMethod = methods.Streams;
        Permissions = permissions;
        EncryptMetadata = encryptMetadata;
    }

    /// <summary>Gets the security handler revision.</summary>
    public int Revision { get; }

    /// <summary>Gets the permission flags (/P).</summary>
    public int Permissions { get; }

    /// <summary>Gets a value indicating whether metadata streams are encrypted.</summary>
    public bool EncryptMetadata { get; }

    /// <summary>
    /// Gets who the password or certificate that opened the document is: the owner, who may change or remove the protection,
    /// or a user bound by <see cref="Permissions"/>.
    /// </summary>
    public PdfSecurityRole Role { get; init; }

    /// <summary>Gets the file encryption key, the input key material of the PDF MAC key derivation (ISO/TS 32004:2024 6.4).</summary>
    internal ReadOnlySpan<byte> FileKey
    {
        get
        {
            // A disposed handler has zeroed its key, which must not be used as if it were the key.
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return _fileKey;
        }
    }

    /// <summary>Gets how strings are encrypted.</summary>
    internal CryptMethod StringMethod { get; }

    /// <summary>Gets how streams are encrypted.</summary>
    internal CryptMethod StreamMethod { get; }

    /// <summary>Gets the /CF dictionary of named crypt filters, or <see langword="null"/> before revision 4.</summary>
    internal PdfDictionary? CryptFilters { get; init; }

    /// <summary>Gets the name of the crypt filter for embedded file streams (/EFF), or none when the stream filter applies.</summary>
    internal PdfName EmbeddedFileFilter { get; init; }

    /// <summary>Gets a value indicating whether a crypt filter uses AES-GCM, so a saved document shall declare the ISO/TS 32003 extension.</summary>
    internal bool UsesAesGcm { get; init; }

    /// <summary>Gets a value indicating whether writing encrypts with AES, which picks a random initialisation vector each time.</summary>
    internal bool UsesRandomIv => UsesAesGcm || StringMethod >= CryptMethod.Aes128 || StreamMethod >= CryptMethod.Aes128;

    /// <summary>Gets the suffix of AES-128 object key input.</summary>
    private static ReadOnlySpan<byte> AesSalt => "sAlT"u8;

    /// <summary>Authenticates with a password and creates the handler.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="firstId">The first part of the trailer /ID.</param>
    /// <param name="password">The password; tried as the user password and then the owner password.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="encrypt"/> is <see langword="null"/>.</exception>
    public static PdfSecurityResult TryCreate(PdfDictionary encrypt, ReadOnlySpan<byte> firstId, string? password, out PdfSecurityHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        handler = null;
        if (!encrypt.IsName(KnownName.Filter, KnownName.Standard))
        {
            return PdfSecurityResult.UnsupportedHandler;
        }

        var version = encrypt.GetInt32(KnownName.V);
        var revision = encrypt.GetInt32(KnownName.R);
        var methods = ReadMethods(encrypt, version);
        var keyLength = revision >= Revision5 ? Sha256Length : ReadKeyLength(encrypt, revision, version);
        var supported = CheckSupport(encrypt, version, revision, methods, keyLength);
        if (supported != PdfSecurityResult.Success)
        {
            return supported;
        }

        var key = Authenticate(encrypt, new(revision, version, keyLength), firstId, password, out var role);
        if (key is null)
        {
            return PdfSecurityResult.WrongPassword;
        }

        if (revision == Revision7 && !IsPermsValid(encrypt.GetStringBytes(KnownName.Perms), key))
        {
            return PdfSecurityResult.Damaged;
        }

        handler = new(key, revision, methods, ReadPermissions(encrypt), encrypt.GetBoolean(KnownName.EncryptMetadata, true))
        {
            CryptFilters = version >= Revision4 ? encrypt.GetDictionary(KnownName.CF) : null,
            EmbeddedFileFilter = version >= Revision4 ? encrypt.GetName(KnownName.EFF) : default,
            UsesAesGcm = DeclaresGcm(encrypt.GetDictionary(KnownName.CF)),
            Role = role,
        };
        return PdfSecurityResult.Success;
    }

    /// <summary>Decrypts a string, authenticating AES-GCM data first. Nothing is returned for a value that fails authentication.</summary>
    /// <param name="id">The object holding the string.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <param name="plain">The plain bytes when decryption succeeded.</param>
    /// <returns><see langword="false"/> when AES-GCM data is too short or fails authentication.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDecryptString(PdfObjectId id, ReadOnlySpan<byte> data, [NotNullWhen(true)] out byte[]? plain) =>
        TryDecrypt(StringMethod, id, data, out plain);

    /// <summary>Decrypts a stream's data, authenticating AES-GCM data first. Nothing is returned for data that fails authentication.</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <param name="plain">The plain bytes when decryption succeeded.</param>
    /// <returns><see langword="false"/> when AES-GCM data is too short or fails authentication.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDecryptStream(PdfObjectId id, ReadOnlySpan<byte> data, [NotNullWhen(true)] out byte[]? plain) =>
        TryDecrypt(StreamMethod, id, data, out plain);

    /// <summary>Decrypts a string.</summary>
    /// <param name="id">The object holding the string.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <returns>The plain bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] DecryptString(PdfObjectId id, ReadOnlySpan<byte> data) => Decrypt(StringMethod, id, data);

    /// <summary>Decrypts a stream's data.</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <returns>The plain bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] DecryptStream(PdfObjectId id, ReadOnlySpan<byte> data) => Decrypt(StreamMethod, id, data);

    /// <summary>Encrypts a string for writing.</summary>
    /// <param name="id">The object holding the string.</param>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The encrypted bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] EncryptString(PdfObjectId id, ReadOnlySpan<byte> data) => Encrypt(StringMethod, id, data);

    /// <summary>Encrypts a stream's data for writing.</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The encrypted bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] EncryptStream(PdfObjectId id, ReadOnlySpan<byte> data) => Encrypt(StreamMethod, id, data);

    /// <summary>Releases the AES instance once any decryption in flight has finished. Later use throws <see cref="ObjectDisposedException"/>.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            Volatile.Write(ref _disposed, 1);
            DisposeAesPool();
            _gcm?.Dispose();

            // Every user of the key checks the flag under this lock, or after reading the key, so none keeps a zeroed key.
            CryptographicOperations.ZeroMemory(_fileKey);
        }
    }

    /// <summary>Creates the handler of a file about to be written. Its key was just made, so it holds the owner's rights.</summary>
    /// <param name="fileKey">The file key.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="methods">How strings and streams are encrypted.</param>
    /// <param name="permissions">The permission word.</param>
    /// <param name="encryptMetadata">Whether metadata streams are encrypted.</param>
    /// <param name="filters">The /CF dictionary written to the file.</param>
    /// <returns>The handler.</returns>
    internal static PdfSecurityHandler CreateNew(byte[] fileKey, int revision, CryptMethods methods, int permissions, bool encryptMetadata, PdfDictionary filters) =>
        new(fileKey, revision, methods, permissions, encryptMetadata)
        {
            CryptFilters = filters,
            UsesAesGcm = methods.Streams == CryptMethod.AesGcm || methods.Strings == CryptMethod.AesGcm,
            Role = PdfSecurityRole.Owner,
        };

    /// <summary>Decrypts a stream's data with a named crypt filter, authenticating AES-GCM data first.</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <param name="filterName">The crypt filter's name in /CF.</param>
    /// <param name="plain">The plain bytes when decryption succeeded.</param>
    /// <returns><see langword="false"/> when AES-GCM data is too short or fails authentication.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryDecryptStream(PdfObjectId id, ReadOnlySpan<byte> data, PdfName filterName, [NotNullWhen(true)] out byte[]? plain) =>
        TryDecrypt(FilterMethod(CryptFilters, filterName), id, data, out plain);

    /// <summary>Finds the crypt filter an embedded file stream uses when it names none itself: the /EFF filter.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="name">The filter name when the stream is an embedded file and /EFF is present.</param>
    /// <returns><see langword="true"/> when /EFF applies to the stream.</returns>
    internal bool TryGetEmbeddedFileFilter(PdfDictionary dictionary, out PdfName name)
    {
        name = EmbeddedFileFilter;
        return !name.IsNone && dictionary.IsName(KnownName.Type, KnownName.EmbeddedFile);
    }

    /// <summary>Decrypts a stream's data with the method of a named crypt filter (a stream's /Crypt filter with a /Name).</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <param name="filterName">The crypt filter's name in /CF.</param>
    /// <returns>The plain bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] DecryptStream(PdfObjectId id, ReadOnlySpan<byte> data, PdfName filterName) =>
        Decrypt(FilterMethod(CryptFilters, filterName), id, data);

    /// <summary>Encrypts a stream's data with the method of a named crypt filter (a stream's /Crypt filter with a /Name).</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The plain bytes.</param>
    /// <param name="filterName">The crypt filter's name in /CF.</param>
    /// <returns>The encrypted bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] EncryptStream(PdfObjectId id, ReadOnlySpan<byte> data, PdfName filterName) =>
        Encrypt(FilterMethod(CryptFilters, filterName), id, data);

    /// <summary>Gets the method of a named crypt filter.</summary>
    /// <param name="filterName">The crypt filter's name in /CF.</param>
    /// <returns>The method.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CryptMethod MethodOf(PdfName filterName) => FilterMethod(CryptFilters, filterName);

    /// <summary>Reads the string and stream methods from /V and the crypt filters.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="version">The /V value.</param>
    /// <returns>The methods.</returns>
    private static CryptMethods ReadMethods(PdfDictionary encrypt, int version)
    {
        if (version < Revision4)
        {
            return new(CryptMethod.Rc4, CryptMethod.Rc4);
        }

        var filters = encrypt.GetDictionary(KnownName.CF);
        return new(FilterMethod(filters, encrypt.GetName(KnownName.StrF)), FilterMethod(filters, encrypt.GetName(KnownName.StmF)));
    }

    /// <summary>Drops the last bytes of data when the final byte is a pad length from 1 to 16 (PKCS#5 padding, ISO 32000-2 7.6.3.2); otherwise the data was padded wrongly and is kept whole.</summary>
    /// <param name="raw">The decrypted bytes with their padding.</param>
    /// <returns>The bytes without the pad.</returns>
    private static byte[] StripPlausiblePad(byte[] raw)
    {
        if (raw.Length == 0)
        {
            return raw;
        }

        var pad = raw[^1];
        return pad is >= 1 and <= AesBlock && pad <= raw.Length ? raw.AsSpan(0, raw.Length - pad).ToArray() : raw;
    }

    /// <summary>Reads /P as its low 32 bits, because writers store it as an unsigned number too.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <returns>The permission flags.</returns>
    private static int ReadPermissions(PdfDictionary encrypt) => unchecked((int)encrypt.GetInteger(KnownName.P));

    /// <summary>Reads the file key length in bytes for revisions 2 to 4 from /Length, which is in bits (ISO 32000-2 7.6.2, Table 20).</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="version">The /V value.</param>
    /// <returns>The length in bytes, or zero when it is not valid.</returns>
    private static int ReadKeyLength(PdfDictionary encrypt, int revision, int version)
    {
        if (revision == Revision2 || version < Version2)
        {
            return Revision2KeyLength;
        }

        var bits = version >= Revision4 ? ReadV4KeyBits(encrypt) : encrypt.GetInt32(KnownName.Length, DefaultKeyBits);
        if (bits <= 0)
        {
            return 0;
        }

        // Some writers store /Length in bytes; a value under 40 cannot be a valid bit length, so read it as bytes.
        return (bits < DefaultKeyBits ? bits * ByteBits : bits) / ByteBits;
    }

    /// <summary>Reads the key length of a version 4 file: the stream filter's own /Length, then /Length, then 128.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <returns>The length, in bits or bytes as the writer chose.</returns>
    private static int ReadV4KeyBits(PdfDictionary encrypt)
    {
        var stream = encrypt.GetDictionary(KnownName.CF)?.GetDictionary(encrypt.GetName(KnownName.StmF));
        var bits = stream?.GetInt32(KnownName.Length, 0) ?? 0;
        return bits == 0 ? encrypt.GetInt32(KnownName.Length, DefaultV4KeyBits) : bits;
    }

    /// <summary>Checks that every cipher in use supports the key length.</summary>
    /// <param name="methods">The string and stream methods.</param>
    /// <param name="keyLength">The key length in bytes.</param>
    /// <param name="revision">The revision.</param>
    /// <returns><see langword="true"/> when the combination can be used.</returns>
    private static bool IsValidKeyLength(CryptMethods methods, int keyLength, int revision) => revision switch
    {
        >= Revision7 => IsAes256Family(methods.Strings) && IsAes256Family(methods.Streams),
        >= Revision5 => IsAes256OrNone(methods.Strings) && IsAes256OrNone(methods.Streams),
        _ => SupportsKeyLength(methods.Strings, keyLength) && SupportsKeyLength(methods.Streams, keyLength),
    };

    /// <summary>Checks that a method is allowed with revision 7, which uses AES-256 in either mode.</summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> for AES-256 CBC, AES-GCM and no encryption.</returns>
    private static bool IsAes256Family(CryptMethod method) => method is CryptMethod.Aes256 or CryptMethod.AesGcm or CryptMethod.None;

    /// <summary>
    /// Checks the parts of an /Encrypt dictionary this handler supports before the password is tried: the version and
    /// revision pair (revision 7 goes with version 6), the ciphers, and for AES-GCM the AESV4 crypt filter that revision 7
    /// shall declare (ISO/TS 32003 5.1) and the platform support it needs.
    /// </summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="version">The /V value.</param>
    /// <param name="revision">The /R value.</param>
    /// <param name="methods">The string and stream methods.</param>
    /// <param name="keyLength">The key length in bytes.</param>
    /// <returns>
    /// Success, <see cref="PdfSecurityResult.UnsupportedHandler"/>, <see cref="PdfSecurityResult.Damaged"/> or
    /// <see cref="PdfSecurityResult.UnsupportedCipher"/>.
    /// </returns>
    private static PdfSecurityResult CheckSupport(PdfDictionary encrypt, int version, int revision, CryptMethods methods, int keyLength)
    {
        if (revision is < Revision2 or > Revision7 || (revision == Revision7) != (version == Version6) || !IsValidKeyLength(methods, keyLength, revision))
        {
            return PdfSecurityResult.UnsupportedHandler;
        }

        var declared = DeclaresGcm(encrypt.GetDictionary(KnownName.CF));
        if (revision == Revision7 && !declared)
        {
            return PdfSecurityResult.Damaged;
        }

        return declared && !AesGcmCipher.IsSupported ? PdfSecurityResult.UnsupportedCipher : PdfSecurityResult.Success;
    }

    /// <summary>Determines whether any crypt filter in /CF uses the AESV4 method.</summary>
    /// <param name="filters">The /CF dictionary, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool DeclaresGcm(PdfDictionary? filters)
    {
        for (var i = 0; filters is not null && i < filters.Count; i++)
        {
            if (filters.GetDictionary(filters.GetKeyAt(i))?.GetName(KnownName.CFM).Is(KnownName.AESV4) == true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks one cipher against a key length from revisions 2 to 4, whose key comes from a 16-byte MD5 hash.</summary>
    /// <param name="method">The method.</param>
    /// <param name="keyLength">The key length in bytes.</param>
    /// <returns><see langword="true"/> when the cipher supports the length.</returns>
    private static bool SupportsKeyLength(CryptMethod method, int keyLength) => method switch
    {
        CryptMethod.Rc4 => keyLength is >= Revision2KeyLength and <= Md5.HashLength,
        CryptMethod.Aes128 or CryptMethod.Aes256 => keyLength == AesBlock,
        CryptMethod.AesGcm => false,
        _ => true,
    };

    /// <summary>Checks that a method is allowed with revisions 5 and 6, which use AES-256 only (PDF 2.0 drops RC4 and AES-128).</summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> for AES-256 and for no encryption.</returns>
    private static bool IsAes256OrNone(CryptMethod method) => method is CryptMethod.Aes256 or CryptMethod.None;

    /// <summary>Gets the method of a named crypt filter.</summary>
    /// <param name="filters">The /CF dictionary.</param>
    /// <param name="name">The filter name.</param>
    /// <returns>The method.</returns>
    private static CryptMethod FilterMethod(PdfDictionary? filters, PdfName name)
    {
        if (name.IsNone || name.Is(KnownName.Identity))
        {
            return CryptMethod.None;
        }

        var method = filters?.GetDictionary(name)?.GetName(KnownName.CFM) ?? default;
        return method.ToKnownName() switch
        {
            KnownName.V2 => CryptMethod.Rc4,
            KnownName.AESV2 => CryptMethod.Aes128,
            KnownName.AESV3 => CryptMethod.Aes256,
            KnownName.AESV4 => CryptMethod.AesGcm,
            _ => CryptMethod.None,
        };
    }

    /// <summary>Decrypts data.</summary>
    /// <param name="method">The method.</param>
    /// <param name="id">The object.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <returns>The plain bytes.</returns>
    /// <exception cref="PdfException">The method is AES-GCM and the data is too short or fails authentication.</exception>
    private byte[] Decrypt(CryptMethod method, PdfObjectId id, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Span<byte> keyBuffer = stackalloc byte[Md5.HashLength];
        if (method == CryptMethod.None)
        {
            return data.ToArray();
        }

        if (method == CryptMethod.AesGcm)
        {
            return DecryptGcm(data, out var authenticated)
                ? authenticated!
                : throw new PdfException(PdfError.Format, "AES-GCM data is too short or failed authentication, so it was not decrypted.");
        }

        if (method == CryptMethod.Rc4)
        {
            var plain = data.ToArray();
            Rc4.Apply(ObjectKey(id, false, keyBuffer), plain);

            // Dispose zeroes the file key, so a call that overlapped it must not return what it made from the zeroed key.
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return plain;
        }

        return DecryptAes(method == CryptMethod.Aes256 ? _fileKey : ObjectKey(id, true, keyBuffer), data);
    }

    /// <summary>Encrypts data.</summary>
    /// <param name="method">The method.</param>
    /// <param name="id">The object.</param>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The encrypted bytes.</returns>
    /// <exception cref="PdfException">The method is AES-GCM and the data is over the size limit or the key has reached its encryption limit.</exception>
    private byte[] Encrypt(CryptMethod method, PdfObjectId id, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Span<byte> keyBuffer = stackalloc byte[Md5.HashLength];
        if (method == CryptMethod.None)
        {
            return data.ToArray();
        }

        if (method == CryptMethod.AesGcm)
        {
            return EncryptGcm(data);
        }

        if (method == CryptMethod.Rc4)
        {
            var cipher = data.ToArray();
            Rc4.Apply(ObjectKey(id, false, keyBuffer), cipher);
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return cipher;
        }

        return EncryptAes(method == CryptMethod.Aes256 ? _fileKey : ObjectKey(id, true, keyBuffer), data);
    }

    /// <summary>Decrypts data, reporting AES-GCM data that fails authentication instead of throwing.</summary>
    /// <param name="method">The method.</param>
    /// <param name="id">The object.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <param name="plain">The plain bytes when decryption succeeded.</param>
    /// <returns><see langword="false"/> when AES-GCM data is too short or fails authentication.</returns>
    private bool TryDecrypt(CryptMethod method, PdfObjectId id, ReadOnlySpan<byte> data, [NotNullWhen(true)] out byte[]? plain)
    {
        if (method != CryptMethod.AesGcm)
        {
            plain = Decrypt(method, id, data);
            return true;
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return DecryptGcm(data, out plain);
    }

    /// <summary>Authenticates and decrypts AES-GCM data with the file key.</summary>
    /// <param name="data">The IV, cipher text and tag.</param>
    /// <param name="plain">The plain bytes when authentication succeeded.</param>
    /// <returns><see langword="false"/> when the data is too short or fails authentication.</returns>
    private bool DecryptGcm(ReadOnlySpan<byte> data, [NotNullWhen(true)] out byte[]? plain)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            return AesGcmCipher.TryDecrypt(Gcm(), data, out plain);
        }
    }

    /// <summary>Encrypts with AES-GCM under a random 12-byte IV, counting the encryption against the key's limit.</summary>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The IV, cipher text and tag.</returns>
    /// <exception cref="PdfException">The data is over the size limit, or the key has reached its encryption limit.</exception>
    private byte[] EncryptGcm(ReadOnlySpan<byte> data)
    {
        if (!AesGcmCipher.IsWithinSizeLimit(data.Length))
        {
            throw new PdfException(PdfError.Security, "A string or stream is too large to encrypt with AES-GCM; it must be split into smaller objects.");
        }

        if (!AesGcmCipher.TryCountEncryption(ref _gcmEncryptions))
        {
            throw new PdfException(PdfError.Security, "The AES-GCM key has encrypted as many objects as the standard allows; save the document with a new key.");
        }

        Span<byte> iv = stackalloc byte[AesGcmCipher.IvLength];
        RandomNumberGenerator.Fill(iv);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            return AesGcmCipher.Encrypt(Gcm(), data, iv);
        }
    }

    /// <summary>Gets the AES-GCM instance keyed with the file key, creating it on first use. The caller holds <see cref="_gate"/>.</summary>
    /// <returns>The cipher.</returns>
    private AesGcm Gcm() => _gcm ??= new(_fileKey, AesGcmCipher.TagLength);

    /// <summary>Decrypts AES-CBC data whose first block is the IV, tolerating bad padding.</summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The IV followed by the cipher text.</param>
    /// <returns>The plain bytes.</returns>
    private byte[] DecryptAes(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        if (data.Length < AesBlock + AesBlock)
        {
            return [];
        }

        var body = data[AesBlock..];
        body = body[..(body.Length - (body.Length % AesBlock))];
        var aes = RentAes(key);
        try
        {
            byte[] plain;
            try
            {
                plain = aes.DecryptCbc(body, data[..AesBlock]);
            }
            catch (CryptographicException)
            {
                // Some writers pad wrongly; keep the bytes, dropping a trailing pad when its length byte is plausible.
                plain = StripPlausiblePad(aes.DecryptCbc(body, data[..AesBlock], PaddingMode.None));
            }

            // Dispose sets the flag and then zeroes the file key, so a call that overlapped it must not return what it made from the zeroed key.
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return plain;
        }
        finally
        {
            ReturnAes(aes);
        }
    }

    /// <summary>Takes an AES instance from the pool, or makes one, and keys it.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The keyed instance; give it back with <see cref="ReturnAes"/>.</returns>
    private Aes RentAes(ReadOnlySpan<byte> key)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Aes? aes = null;
        for (var i = 0; i < _aesSlots.Length && aes is null; i++)
        {
            aes = Interlocked.Exchange(ref _aesSlots[i], null);
        }

        aes ??= Aes.Create();
        aes.SetKey(key);
        return aes;
    }

    /// <summary>Puts an AES instance back in a free slot, or disposes it when every slot is full or the handler has been disposed.</summary>
    /// <param name="aes">The instance.</param>
    private void ReturnAes(Aes aes)
    {
        var stored = false;
        for (var i = 0; i < _aesSlots.Length && !stored; i++)
        {
            stored = Interlocked.CompareExchange(ref _aesSlots[i], aes, null) is null;
        }

        if (!stored)
        {
            aes.Dispose();
        }

        // A disposal that ran before the store did not see this instance, so empty the slots again.
        if (Volatile.Read(ref _disposed) != 0)
        {
            DisposeAesPool();
        }
    }

    /// <summary>Disposes the idle AES instances.</summary>
    private void DisposeAesPool()
    {
        for (var i = 0; i < _aesSlots.Length; i++)
        {
            Interlocked.Exchange(ref _aesSlots[i], null)?.Dispose();
        }
    }

    /// <summary>Encrypts with AES-CBC, prefixing a random IV.</summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The IV followed by the cipher text.</returns>
    private byte[] EncryptAes(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        Span<byte> iv = stackalloc byte[AesBlock];
        RandomNumberGenerator.Fill(iv);
        var aes = RentAes(key);
        try
        {
            var cipher = aes.EncryptCbc(data, iv);
            var result = new byte[AesBlock + cipher.Length];
            iv.CopyTo(result);
            cipher.CopyTo(result.AsSpan(AesBlock));
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return result;
        }
        finally
        {
            ReturnAes(aes);
        }
    }

    /// <summary>Computes an object's key (algorithm 1).</summary>
    /// <param name="id">The object.</param>
    /// <param name="aes">Whether the key is for AES-128.</param>
    /// <param name="destination">A 16-byte buffer.</param>
    /// <returns>The key, a slice of the buffer.</returns>
    private Span<byte> ObjectKey(PdfObjectId id, bool aes, Span<byte> destination)
    {
        Span<byte> input = stackalloc byte[Md5.HashLength + ObjectSaltLength + AesSaltLength];
        _fileKey.CopyTo(input);
        var salt = input[_fileKey.Length..];
        salt[0] = (byte)id.Number;
        salt[1] = (byte)(id.Number >> ByteBits);
        salt[ThirdNumberByte] = (byte)(id.Number >> ThirdByteShift);
        BinaryPrimitives.WriteUInt16LittleEndian(salt[GenerationOffset..], (ushort)id.Generation);
        var length = _fileKey.Length + ObjectSaltLength;
        if (aes)
        {
            AesSalt.CopyTo(input[length..]);
            length += AesSaltLength;
        }

        _ = Md5.HashData(input[..length], destination);

        // The input starts with the file key.
        CryptographicOperations.ZeroMemory(input);
        return destination[..Math.Min(_fileKey.Length + ObjectSaltLength, Md5.HashLength)];
    }
}
