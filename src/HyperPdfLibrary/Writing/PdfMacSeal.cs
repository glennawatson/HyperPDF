// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdf.Cms;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Gives a saved revision of an encrypted document its own standalone PDF MAC (ISO/TS 32004:2024 6.5.1). The trailer gets an
/// <c>/AuthCode</c> dictionary whose <c>/MAC</c> space is set aside before the token is computed, so the token's length
/// is known; the byte range is then settled, because its digits are part of the bytes it covers; and the token is
/// written into the space. A document that needs a PDF MAC and cannot get one stops the save.
/// </summary>
internal sealed class PdfMacSeal
{
    /// <summary>The most times the trailer is written while the byte range settles.</summary>
    private const int MaxPasses = 8;

    /// <summary>The brackets around the hexadecimal token.</summary>
    private const int Brackets = 2;

    /// <summary>The security handler, which holds the file key.</summary>
    private readonly PdfSecurityHandler _security;

    /// <summary>The 32-byte <c>/KDFSalt</c>.</summary>
    private readonly byte[] _salt;

    /// <summary>The hexadecimal string the token will replace: brackets around zeros.</summary>
    private readonly byte[] _placeholder;

    /// <summary>The <c>/AuthCode</c> dictionary placed in the trailer.</summary>
    private readonly PdfDictionary _authCode;

    /// <summary>The name <c>/AuthCode</c>.</summary>
    private readonly PdfName _authCodeName;

    /// <summary>The name <c>/ByteRange</c>.</summary>
    private readonly PdfName _byteRangeName;

    /// <summary>The length of the first covered range in the byte range now written.</summary>
    private long _first;

    /// <summary>The start of the second covered range in the byte range now written.</summary>
    private long _second;

    /// <summary>The length of the second covered range in the byte range now written.</summary>
    private long _length;

    /// <summary>The number of times the trailer has been written.</summary>
    private int _passes;

    /// <summary>Initializes a new instance of the <see cref="PdfMacSeal"/> class.</summary>
    /// <param name="store">The document.</param>
    /// <param name="security">The security handler.</param>
    /// <param name="salt">The salt.</param>
    private PdfMacSeal(PdfObjectStore store, PdfSecurityHandler security, byte[] salt)
    {
        _security = security;
        _salt = salt;
        var tokenLength = PdfMacTokenWriter.Length(CmsDigestAlgorithm.Sha256, false);
        _placeholder = new byte[(tokenLength * PdfMacHex.DigitsPerByte) + Brackets];
        _placeholder.AsSpan().Fill((byte)'0');
        _placeholder[0] = (byte)'<';
        _placeholder[^1] = (byte)'>';
        var names = store.Names;
        _authCodeName = names.Intern("AuthCode"u8);
        _byteRangeName = KnownName.ByteRange;
        _authCode = new(null);
        _authCode.Set(names.Intern("MAC"u8), PdfValue.FromString(new byte[tokenLength]));
        _authCode.Set(_byteRangeName, ByteRange(0, 0, 0));
        _authCode.Set(names.Intern("MACLocation"u8), PdfValue.FromName(names.Intern("Standalone"u8)));
    }

    /// <summary>Gets the name of the trailer entry that holds the PDF MAC.</summary>
    internal PdfName AuthCodeName => _authCodeName;

    /// <summary>
    /// Determines whether saving the document needs a PDF MAC: it is encrypted, and its permissions require one or an
    /// earlier revision has one.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <returns><see langword="true"/> when a saved revision needs a fresh token.</returns>
    internal static bool IsNeeded(PdfObjectStore store) =>
        store.Security is { } security
        && store.Trailer.GetDictionary(KnownName.Encrypt) is { } encrypt
        && (PdfMacRequirement.IsRequired(encrypt, security) || store.Trailer.ContainsKey(store.Names.Intern("AuthCode"u8)));

    /// <summary>Prepares the PDF MAC of a revision about to be saved.</summary>
    /// <param name="store">The document.</param>
    /// <returns>The seal, or <see langword="null"/> when the document needs no PDF MAC.</returns>
    /// <exception cref="PdfException">The document needs a PDF MAC and the writer cannot make a valid one.</exception>
    internal static PdfMacSeal? Create(PdfObjectStore store)
    {
        if (!IsNeeded(store))
        {
            return null;
        }

        var encrypt = store.Trailer.GetDictionary(KnownName.Encrypt)!;
        var salt = encrypt.GetRaw(store.Names.Intern("KDFSalt"u8));
        var usable = encrypt.GetInt32(KnownName.V) >= PdfMacKeys.MinimumVersion
            && salt.Kind == PdfKind.String
            && salt.AsStringBytes().Length == PdfMacKeys.SaltLength;
        if (!usable)
        {
            throw new PdfException(
                PdfError.Security,
                "This document needs a PDF MAC (ISO/TS 32004) to be saved, but its encryption has no valid 32-byte /KDFSalt, "
                + "so a valid one cannot be written. The file was not saved.");
        }

        return new(store, store.Security!, salt.AsStringBytes().ToArray());
    }

    /// <summary>Stops a save that cannot write a PDF MAC, for a writer that has no way to place one.</summary>
    /// <param name="store">The document.</param>
    /// <param name="operation">What the writer does, for the message, such as "Linearizing".</param>
    /// <exception cref="PdfException">The document needs a PDF MAC.</exception>
    internal static void ThrowIfNeeded(PdfObjectStore store, string operation)
    {
        if (IsNeeded(store))
        {
            throw new PdfException(
                PdfError.Security,
                $"{operation} cannot keep this document's PDF MAC (ISO/TS 32004), so the result would not be protected against changes. "
                + "Save the document instead, or remove its encryption. The file was not written.");
        }
    }

    /// <summary>Puts the <c>/AuthCode</c> entry, with the space for the token, in a trailer.</summary>
    /// <param name="trailer">The trailer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddTo(PdfDictionary trailer) => trailer.Set(_authCodeName, PdfValue.FromDictionary(_authCode));

    /// <summary>
    /// Settles the byte range of the trailer just written, and once it is right computes the token and writes it into its
    /// space. When the byte range changed, the caller writes the trailer again and calls this again.
    /// </summary>
    /// <param name="writer">The writer; the trailer section starts at <paramref name="sectionStart"/>.</param>
    /// <param name="prefix">The bytes of the file before the writer's first byte, or <see langword="null"/> when it starts the file.</param>
    /// <param name="sectionStart">Where the trailer section starts in the writer.</param>
    /// <param name="cancellationToken">Cancels the hashing.</param>
    /// <returns><see langword="true"/> when the token was written; <see langword="false"/> when the byte range changed and the trailer must be written again.</returns>
    /// <exception cref="PdfException">The space for the token is not in the section, or the byte range does not settle.</exception>
    internal bool TryComplete(ref PdfObjectWriter writer, PdfByteSource? prefix, int sectionStart, CancellationToken cancellationToken)
    {
        var written = writer.WrittenSpan;
        var at = written[sectionStart..].IndexOf(_placeholder);
        _passes++;
        if (at < 0 || _passes > MaxPasses)
        {
            throw new PdfException(PdfError.Format, "The PDF MAC could not be placed in the trailer, so the file was not saved.");
        }

        var prefixLength = prefix?.Length ?? 0;
        var first = prefixLength + sectionStart + at;
        var second = first + _placeholder.Length;
        var length = prefixLength + written.Length - second;
        if (first != _first || second != _second || length != _length)
        {
            _first = first;
            _second = second;
            _length = length;
            _authCode.Set(_byteRangeName, ByteRange(first, second, length));
            return false;
        }

        Span<byte> digest = stackalloc byte[CmsDigests.MaxLength];
        var digestLength = Digest(prefix, written, first, second, digest, cancellationToken);
        var token = PdfMacTokenWriter.CreateStandalone(_security.FileKey, _salt, digest[..digestLength]);
        PdfMacHex.Encode(token, writer.Buffer.Array.AsSpan(sectionStart + at + 1, token.Length * PdfMacHex.DigitsPerByte));
        return true;
    }

    /// <summary>Builds the <c>/ByteRange</c> array: zero, the length before the token, where the rest starts and its length.</summary>
    /// <param name="first">The length of the first range.</param>
    /// <param name="second">The start of the second range.</param>
    /// <param name="length">The length of the second range.</param>
    /// <returns>The array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfValue ByteRange(long first, long second, long length) =>
        PdfValue.FromArray(new(null, [PdfValue.FromInteger(0), PdfValue.FromInteger(first), PdfValue.FromInteger(second), PdfValue.FromInteger(length)]));

    /// <summary>Digests everything but the token: the bytes before it and the bytes after it, across the earlier file and the writer.</summary>
    /// <param name="prefix">The earlier file, or <see langword="null"/>.</param>
    /// <param name="written">The writer's bytes.</param>
    /// <param name="first">The length of the first range.</param>
    /// <param name="second">The start of the second range.</param>
    /// <param name="destination">Receives the digest.</param>
    /// <param name="cancellationToken">Cancels the hashing.</param>
    /// <returns>The digest length.</returns>
    private static int Digest(PdfByteSource? prefix, ReadOnlySpan<byte> written, long first, long second, Span<byte> destination, CancellationToken cancellationToken)
    {
        var total = (prefix?.Length ?? 0) + written.Length;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendRange(hash, prefix, written, 0, first, cancellationToken);
        AppendRange(hash, prefix, written, second, total, cancellationToken);
        return hash.GetHashAndReset(destination);
    }

    /// <summary>Adds a range of the whole file (the earlier file, then the writer's bytes) to a running digest.</summary>
    /// <param name="hash">The digest.</param>
    /// <param name="prefix">The earlier file, or <see langword="null"/>.</param>
    /// <param name="written">The writer's bytes.</param>
    /// <param name="from">The first byte.</param>
    /// <param name="to">The position after the last byte.</param>
    /// <param name="cancellationToken">Cancels the hashing.</param>
    private static void AppendRange(IncrementalHash hash, PdfByteSource? prefix, ReadOnlySpan<byte> written, long from, long to, CancellationToken cancellationToken)
    {
        var prefixLength = prefix?.Length ?? 0;
        if (prefix is not null && from < prefixLength)
        {
            PdfMacDigest.Append(hash, prefix, from, Math.Min(to, prefixLength) - from, cancellationToken);
        }

        var start = Math.Max(from, prefixLength);
        if (to > start)
        {
            hash.AppendData(written.Slice((int)(start - prefixLength), (int)(to - start)));
        }
    }
}
