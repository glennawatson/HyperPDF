// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
#if !NET11_0_OR_GREATER
using System.Buffers.Binary;
#endif

namespace HyperPdfLibrary.Compat;

/// <summary>
/// Unwraps keys wrapped with the AES key wrap of RFC 3394 and checks the default initial value. .NET 11 has the
/// algorithm; .NET 10 has only the padded variant of RFC 5649, so its fallback runs the RFC 3394 2.2.2 rounds over AES-ECB.
/// </summary>
internal static class AesKeyWrapping
{
    /// <summary>The bytes in a key wrap block.</summary>
    internal const int BlockLength = 8;

    /// <summary>The fewest key blocks a wrapped key has.</summary>
    private const int MinKeyBlocks = 2;

#if !NET11_0_OR_GREATER
    /// <summary>The rounds over the blocks.</summary>
    private const int Rounds = 6;

    /// <summary>The default initial value from RFC 3394 2.2.3.1.</summary>
    private const ulong DefaultInitialValue = 0xA6A6A6A6A6A6A6A6;
#endif

    /// <summary>Unwraps a key.</summary>
    /// <param name="kek">The AES instance holding the key-encryption key.</param>
    /// <param name="wrapped">The wrapped key.</param>
    /// <param name="key">Receives the key, 8 bytes shorter than <paramref name="wrapped"/>.</param>
    /// <returns><see langword="true"/> when the key unwrapped and its check value is right.</returns>
    internal static bool TryUnwrap(Aes kek, ReadOnlySpan<byte> wrapped, Span<byte> key)
    {
        if (wrapped.Length % BlockLength != 0 || key.Length != wrapped.Length - BlockLength || key.Length < BlockLength * MinKeyBlocks)
        {
            return false;
        }

#if NET11_0_OR_GREATER
        try
        {
            return kek.TryDecryptKeyWrap(wrapped, key, out var written) && written == key.Length;
        }
        catch (CryptographicException)
        {
            // The check value is wrong: the key-encryption key does not match or the wrapped key is damaged.
            return false;
        }
#else
        return Unwrap(kek, wrapped, key);
#endif
    }

    /// <summary>Wraps a key.</summary>
    /// <param name="kek">The AES instance holding the key-encryption key.</param>
    /// <param name="key">The key to wrap: a whole number of 8-byte blocks, at least 16 bytes.</param>
    /// <param name="wrapped">Receives the wrapped key, 8 bytes longer than <paramref name="key"/>.</param>
    /// <returns><see langword="true"/> when the key wrapped.</returns>
    internal static bool TryWrap(Aes kek, ReadOnlySpan<byte> key, Span<byte> wrapped)
    {
        if (key.Length % BlockLength != 0 || key.Length < BlockLength * MinKeyBlocks || wrapped.Length != key.Length + BlockLength)
        {
            return false;
        }

#if NET11_0_OR_GREATER
        kek.EncryptKeyWrap(key, wrapped);
        return true;
#else
        Wrap(kek, key, wrapped);
        return true;
#endif
    }

#if !NET11_0_OR_GREATER
    /// <summary>Runs the wrap rounds of RFC 3394 2.2.1 (the index-based form).</summary>
    /// <param name="kek">The AES instance holding the key-encryption key.</param>
    /// <param name="key">The key.</param>
    /// <param name="wrapped">Receives the wrapped key.</param>
    private static void Wrap(Aes kek, ReadOnlySpan<byte> key, Span<byte> wrapped)
    {
        var blocks = key.Length / BlockLength;
        Span<byte> input = stackalloc byte[BlockLength * MinKeyBlocks];
        Span<byte> output = stackalloc byte[BlockLength * MinKeyBlocks];
        var check = DefaultInitialValue;
        var data = wrapped[BlockLength..];
        key.CopyTo(data);
        for (var round = 0; round < Rounds; round++)
        {
            for (var block = 1; block <= blocks; block++)
            {
                var slot = data.Slice((block - 1) * BlockLength, BlockLength);
                BinaryPrimitives.WriteUInt64BigEndian(input, check);
                slot.CopyTo(input[BlockLength..]);
                _ = kek.EncryptEcb(input, output, PaddingMode.None);
                check = BinaryPrimitives.ReadUInt64BigEndian(output) ^ (ulong)((blocks * round) + block);
                output[BlockLength..].CopyTo(slot);
            }
        }

        BinaryPrimitives.WriteUInt64BigEndian(wrapped, check);
        CryptographicOperations.ZeroMemory(input);
        CryptographicOperations.ZeroMemory(output);
    }

    /// <summary>Runs the unwrap rounds of RFC 3394 2.2.2 (the index-based form).</summary>
    /// <param name="kek">The AES instance holding the key-encryption key.</param>
    /// <param name="wrapped">The wrapped key.</param>
    /// <param name="key">Receives the key.</param>
    /// <returns><see langword="true"/> when the check value is right.</returns>
    private static bool Unwrap(Aes kek, ReadOnlySpan<byte> wrapped, Span<byte> key)
    {
        var blocks = key.Length / BlockLength;
        Span<byte> input = stackalloc byte[BlockLength * MinKeyBlocks];
        Span<byte> output = stackalloc byte[BlockLength * MinKeyBlocks];
        var check = BinaryPrimitives.ReadUInt64BigEndian(wrapped);
        wrapped[BlockLength..].CopyTo(key);
        for (var round = Rounds - 1; round >= 0; round--)
        {
            for (var block = blocks; block > 0; block--)
            {
                var slot = key.Slice((block - 1) * BlockLength, BlockLength);
                BinaryPrimitives.WriteUInt64BigEndian(input, check ^ (ulong)((blocks * round) + block));
                slot.CopyTo(input[BlockLength..]);
                _ = kek.DecryptEcb(input, output, PaddingMode.None);
                check = BinaryPrimitives.ReadUInt64BigEndian(output);
                output[BlockLength..].CopyTo(slot);
            }
        }

        CryptographicOperations.ZeroMemory(input);
        CryptographicOperations.ZeroMemory(output);
        Span<byte> expected = stackalloc byte[BlockLength];
        Span<byte> actual = stackalloc byte[BlockLength];
        BinaryPrimitives.WriteUInt64BigEndian(expected, DefaultInitialValue);
        BinaryPrimitives.WriteUInt64BigEndian(actual, check);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
#endif
}
