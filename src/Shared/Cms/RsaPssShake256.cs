// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Security.Cryptography;

namespace HyperPdf.Cms;

/// <summary>
/// Verifies id-RSASSA-PSS-SHAKE256 signatures (RFC 8702, 3.2.1): RSASSA-PSS (RFC 8017, 8.1.2 and 9.1.2) with SHAKE256
/// as both the 64-byte message hash and the mask generation function, a 64-byte salt and the 0xBC trailer. .NET's RSA
/// only offers MGF1, so the public-key step runs on <see cref="BigInteger"/>; it uses only the public key.
/// </summary>
internal static class RsaPssShake256
{
    /// <summary>The hash and salt length.</summary>
    internal const int HashLength = 64;

    /// <summary>The trailer byte.</summary>
    private const byte Trailer = 0xBC;

    /// <summary>The zero bytes that start M'.</summary>
    private const int Padding = 8;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of an encoded message besides its hash and salt: the separator and the trailer.</summary>
    private const int Overhead = 2;

    /// <summary>The byte that separates the padding from the salt in DB.</summary>
    private const byte Separator = 0x01;

    /// <summary>The largest RSA modulus accepted, in bytes (16384 bits).</summary>
    private const int MaxModulusLength = 2048;

    /// <summary>Verifies a signature over a SHAKE256 message hash.</summary>
    /// <param name="rsa">The public key.</param>
    /// <param name="hash">The 64-byte SHAKE256 hash of the message.</param>
    /// <param name="signature">The signature.</param>
    /// <returns><see langword="true"/> when the signature is good.</returns>
    internal static bool Verify(RSA rsa, ReadOnlySpan<byte> hash, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(rsa);
        var key = rsa.ExportParameters(false);
        var modulus = new BigInteger(key.Modulus, true, true);
        var length = key.Modulus!.Length;
        if (hash.Length != HashLength || signature.Length != length || length > MaxModulusLength)
        {
            return false;
        }

        var s = new BigInteger(signature, true, true);
        if (s >= modulus)
        {
            return false;
        }

        var bits = (int)modulus.GetBitLength() - 1;
        var encodedLength = (bits + ByteBits - 1) / ByteBits;
        var message = BigInteger.ModPow(s, new(key.Exponent, true, true), modulus);
        var buffer = ArrayPool<byte>.Shared.Rent(encodedLength);
        try
        {
            var encoded = buffer.AsSpan(0, encodedLength);
            return message.TryWriteBytes(encoded, out var written, true, true) && Check(Align(encoded, written), hash, bits);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Applies the SHAKE256 mask to the data block in place: XORs it with SHAKE256 of the hash.</summary>
    /// <param name="block">The data block.</param>
    /// <param name="seed">The mask seed, the encoded message's hash.</param>
    private static void Mask(Span<byte> block, ReadOnlySpan<byte> seed)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(block.Length);
        try
        {
            var mask = buffer.AsSpan(0, block.Length);
            Shake256.HashData(seed, mask);
            for (var i = 0; i < block.Length; i++)
            {
                block[i] ^= mask[i];
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Computes H, the SHAKE256 hash of M' = eight zero bytes, the message hash and the salt.</summary>
    /// <param name="hash">The message hash.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="destination">Where the 64-byte result goes.</param>
    private static void HashOfPadded(ReadOnlySpan<byte> hash, ReadOnlySpan<byte> salt, Span<byte> destination)
    {
        Span<byte> padded = stackalloc byte[Padding + HashLength + HashLength];
        padded[..Padding].Clear();
        hash.CopyTo(padded[Padding..]);
        salt.CopyTo(padded[(Padding + HashLength)..]);
        Shake256.HashData(padded, destination[..HashLength]);
    }

    /// <summary>Gets the mask that clears the bits above emBits in the first byte.</summary>
    /// <param name="encodedLength">The encoded message's length.</param>
    /// <param name="bits">emBits.</param>
    /// <returns>The mask.</returns>
    private static byte TopMask(int encodedLength, int bits) => (byte)(0xFF >> ((encodedLength * ByteBits) - bits));

    /// <summary>Moves a big-endian number written at the start of a buffer to its end, zero filling the front.</summary>
    /// <param name="encoded">The buffer, as long as the encoded message.</param>
    /// <param name="written">The bytes written.</param>
    /// <returns>The encoded message.</returns>
    private static Span<byte> Align(Span<byte> encoded, int written)
    {
        if (written < encoded.Length)
        {
            encoded[..written].CopyTo(encoded[(encoded.Length - written)..]);
            encoded[..(encoded.Length - written)].Clear();
        }

        return encoded;
    }

    /// <summary>EMSA-PSS-VERIFY (RFC 8017, 9.1.2) with SHAKE256 for the hash and the mask.</summary>
    /// <param name="encoded">EM.</param>
    /// <param name="hash">The message hash.</param>
    /// <param name="bits">emBits.</param>
    /// <returns><see langword="true"/> when consistent.</returns>
    private static bool Check(Span<byte> encoded, ReadOnlySpan<byte> hash, int bits)
    {
        var length = encoded.Length;
        var blockLength = length - HashLength - 1;
        var top = TopMask(length, bits);
        if (length < HashLength + HashLength + Overhead || encoded[^1] != Trailer || (encoded[0] & ~top) != 0)
        {
            return false;
        }

        var block = encoded[..blockLength];
        var expected = encoded.Slice(blockLength, HashLength);
        Mask(block, expected);
        block[0] &= top;
        var padding = blockLength - HashLength - 1;
        if (block[..padding].ContainsAnyExcept((byte)0) || block[padding] != Separator)
        {
            return false;
        }

        Span<byte> actual = stackalloc byte[HashLength];
        HashOfPadded(hash, block[(padding + 1)..], actual);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
