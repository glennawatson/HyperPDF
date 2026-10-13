// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace HyperPdfLibrary.Security;

/// <summary>
/// The AESV4 crypt filter method (ISO/TS 32003:2023 5.2): AES-GCM with the 32-byte crypt filter key, a 12-byte IV, no
/// additional authenticated data, and the data stored as IV, cipher text, then the 16-byte tag.
/// </summary>
internal static class AesGcmCipher
{
    /// <summary>The IV length in bytes.</summary>
    internal const int IvLength = 12;

    /// <summary>The authentication tag length in bytes.</summary>
    internal const int TagLength = 16;

    /// <summary>The bytes a protected value adds to its plain text: the IV and the tag.</summary>
    internal const int Overhead = IvLength + TagLength;

    /// <summary>
    /// The most plain text one string or stream may hold. NIST SP 800-38D 5.2.1.1 limits a message to 2^39 - 256 bits;
    /// ISO/TS 32003 5.2 prints the limit as "2^39 - 256" with the unit bytes, so the smaller bit limit, in bytes, is used.
    /// </summary>
    internal const long MaxPlainLength = (1L << 36) - 32;

    /// <summary>
    /// The most encryptions under one key. IVs are random, so NIST SP 800-38D 8.3 limits a key to 2^32 invocations to keep
    /// the chance of a repeated IV below 2^-32.
    /// </summary>
    internal const long MaxEncryptions = 1L << 32;

    /// <summary>Gets a value indicating whether the platform provides AES-GCM.</summary>
    internal static bool IsSupported => AesGcm.IsSupported;

    /// <summary>Determines whether a value is small enough to encrypt under one IV.</summary>
    /// <param name="length">The plain text length in bytes.</param>
    /// <returns><see langword="true"/> when it is within <see cref="MaxPlainLength"/>.</returns>
    internal static bool IsWithinSizeLimit(long length) => length <= MaxPlainLength;

    /// <summary>Counts one encryption against the key's limit.</summary>
    /// <param name="counter">The encryptions done so far with the key.</param>
    /// <returns><see langword="true"/> when the key may still be used, so this encryption is within <see cref="MaxEncryptions"/>.</returns>
    internal static bool TryCountEncryption(ref long counter) => Interlocked.Increment(ref counter) <= MaxEncryptions;

    /// <summary>Authenticates and decrypts a protected value. Nothing is returned for a value that fails authentication.</summary>
    /// <param name="gcm">The cipher keyed with the crypt filter key.</param>
    /// <param name="data">The IV, cipher text and tag.</param>
    /// <param name="plain">The plain bytes when authentication succeeded.</param>
    /// <returns><see langword="false"/> when the value is shorter than the IV and tag, or the tag does not match.</returns>
    internal static bool TryDecrypt(AesGcm gcm, ReadOnlySpan<byte> data, out byte[]? plain)
    {
        plain = null;
        if (data.Length < Overhead)
        {
            return false;
        }

        var cipher = data[IvLength..^TagLength];
        var result = new byte[cipher.Length];
        try
        {
            gcm.Decrypt(data[..IvLength], cipher, data[^TagLength..], result);
        }
        catch (CryptographicException)
        {
            // AesGcm clears the output when the tag does not match, so nothing unauthenticated escapes.
            return false;
        }

        plain = result;
        return true;
    }

    /// <summary>Encrypts a value under a fresh IV.</summary>
    /// <param name="gcm">The cipher keyed with the crypt filter key.</param>
    /// <param name="plain">The plain bytes.</param>
    /// <param name="iv">The 12-byte IV, never used before with this key.</param>
    /// <returns>The IV, cipher text and tag.</returns>
    internal static byte[] Encrypt(AesGcm gcm, ReadOnlySpan<byte> plain, ReadOnlySpan<byte> iv)
    {
        var result = new byte[plain.Length + Overhead];
        iv.CopyTo(result);
        gcm.Encrypt(iv, plain, result.AsSpan(IvLength, plain.Length), result.AsSpan(IvLength + plain.Length, TagLength));
        return result;
    }
}
