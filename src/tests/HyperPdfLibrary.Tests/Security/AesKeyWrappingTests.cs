// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdfLibrary.Compat;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests the AES key unwrap against the RFC 3394 section 4 test vectors, on both target frameworks.</summary>
public sealed class AesKeyWrappingTests
{
    /// <summary>Half a key wrap block, too short to be a key.</summary>
    private const int HalfBlock = 4;

    /// <summary>A wrapped length with room for only one key block, which RFC 3394 does not allow.</summary>
    private const int ShortWrappedLength = 16;

    /// <summary>Each vector unwraps to its key.</summary>
    /// <param name="kek">The key-encryption key in hex.</param>
    /// <param name="wrapped">The wrapped key in hex.</param>
    /// <param name="key">The key in hex.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("000102030405060708090A0B0C0D0E0F", "1FA68B0A8112B447AEF34BD8FB5A7B829D3E862371D2CFE5", "00112233445566778899AABBCCDDEEFF")]
    [Arguments(
        "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F",
        "28C9F404C4B810F4CBCCB35CFB87F8263F5786E2D80ED326CBC7F0E71A99F43BFB988B9B7A02DD21",
        "00112233445566778899AABBCCDDEEFF000102030405060708090A0B0C0D0E0F")]
    public async Task UnwrapsTestVectors(string kek, string wrapped, string key)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromHexString(kek);
        var unwrapped = new byte[Convert.FromHexString(key).Length];

        await Assert.That(AesKeyWrapping.TryUnwrap(aes, Convert.FromHexString(wrapped), unwrapped)).IsTrue();
        await Assert.That(Convert.ToHexString(unwrapped)).IsEqualTo(key);
    }

    /// <summary>Each vector wraps to its wrapped key, so a key wrapped here unwraps anywhere.</summary>
    /// <param name="kek">The key-encryption key in hex.</param>
    /// <param name="wrapped">The wrapped key in hex.</param>
    /// <param name="key">The key in hex.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("000102030405060708090A0B0C0D0E0F", "1FA68B0A8112B447AEF34BD8FB5A7B829D3E862371D2CFE5", "00112233445566778899AABBCCDDEEFF")]
    [Arguments(
        "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F",
        "28C9F404C4B810F4CBCCB35CFB87F8263F5786E2D80ED326CBC7F0E71A99F43BFB988B9B7A02DD21",
        "00112233445566778899AABBCCDDEEFF000102030405060708090A0B0C0D0E0F")]
    public async Task WrapsTestVectors(string kek, string wrapped, string key)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromHexString(kek);
        var result = new byte[Convert.FromHexString(key).Length + AesKeyWrapping.BlockLength];

        await Assert.That(AesKeyWrapping.TryWrap(aes, Convert.FromHexString(key), result)).IsTrue();
        await Assert.That(Convert.ToHexString(result)).IsEqualTo(wrapped);
    }

    /// <summary>A key that is not whole blocks, a single block, or a destination of the wrong size is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrapRefusesBadLengths()
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");

        await Assert.That(AesKeyWrapping.TryWrap(aes, new byte[ShortWrappedLength - 1], new byte[ShortWrappedLength + AesKeyWrapping.BlockLength])).IsFalse();
        await Assert.That(AesKeyWrapping.TryWrap(aes, new byte[ShortWrappedLength], new byte[ShortWrappedLength + AesKeyWrapping.BlockLength])).IsTrue();
        await Assert.That(AesKeyWrapping.TryWrap(aes, new byte[AesKeyWrapping.BlockLength], new byte[AesKeyWrapping.BlockLength + AesKeyWrapping.BlockLength])).IsFalse();
        await Assert.That(AesKeyWrapping.TryWrap(aes, new byte[ShortWrappedLength], new byte[ShortWrappedLength])).IsFalse();
        await Assert.That(AesKeyWrapping.TryWrap(aes, new byte[HalfBlock], new byte[AesKeyWrapping.BlockLength])).IsFalse();
    }

    /// <summary>A flipped bit, a wrong key or a bad length fails the unwrap instead of giving a key.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsDamagedInput()
    {
        var wrapped = Convert.FromHexString("1FA68B0A8112B447AEF34BD8FB5A7B829D3E862371D2CFE5");
        var damaged = wrapped.ToArray();
        damaged[^1] ^= 1;
        using var aes = Aes.Create();
        aes.Key = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
        using var other = Aes.Create();
        other.Key = Convert.FromHexString("0F0E0D0C0B0A09080706050403020100");
        var key = new byte[wrapped.Length - AesKeyWrapping.BlockLength];

        await Assert.That(AesKeyWrapping.TryUnwrap(aes, damaged, key)).IsFalse();
        await Assert.That(AesKeyWrapping.TryUnwrap(other, wrapped, key)).IsFalse();
        await Assert.That(AesKeyWrapping.TryUnwrap(aes, wrapped.AsSpan(1), key)).IsFalse();
        await Assert.That(AesKeyWrapping.TryUnwrap(aes, wrapped.AsSpan(0, ShortWrappedLength), new byte[AesKeyWrapping.BlockLength])).IsFalse();
    }
}
