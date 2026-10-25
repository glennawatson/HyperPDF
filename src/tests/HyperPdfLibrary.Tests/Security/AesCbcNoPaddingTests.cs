// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests that <see cref="AesCbcNoPadding"/> gives the cipher text the .NET AES gives.</summary>
public sealed class AesCbcNoPaddingTests
{
    /// <summary>The key length of AES-128 in bytes.</summary>
    private const int Aes128 = 16;

    /// <summary>The key length of AES-192 in bytes.</summary>
    private const int Aes192 = 24;

    /// <summary>The key length of AES-256 in bytes.</summary>
    private const int Aes256 = 32;

    /// <summary>The blocks of a short message.</summary>
    private const int FewBlocks = 7;

    /// <summary>The blocks of a message as long as the one the password hash encrypts.</summary>
    private const int ManyBlocks = 800;

    /// <summary>Encrypting with the CPU's AES instructions gives the .NET cipher's bytes for every key size and message length.</summary>
    /// <param name="keyLength">The key length in bytes.</param>
    /// <param name="blocks">The number of blocks.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Aes128, 1)]
    [Arguments(Aes128, FewBlocks)]
    [Arguments(Aes128, ManyBlocks)]
    [Arguments(Aes192, FewBlocks)]
    [Arguments(Aes256, 1)]
    [Arguments(Aes256, FewBlocks)]
    [Arguments(Aes256, ManyBlocks)]
    public async Task MatchesTheDotNetCipher(int keyLength, int blocks)
    {
        if (!AesCbcNoPadding.IsAccelerated)
        {
            Skip.Test("This computer has no AES instructions, so the .NET cipher is used instead.");
        }

        var key = new byte[keyLength];
        var iv = new byte[AesCbcNoPadding.BlockLength];
        var plain = new byte[blocks * AesCbcNoPadding.BlockLength];
        RandomNumberGenerator.Fill(key);
        RandomNumberGenerator.Fill(iv);
        RandomNumberGenerator.Fill(plain);
        using var aes = Aes.Create();
        aes.SetKey(key);
        var expected = aes.EncryptCbc(plain, iv, PaddingMode.None);
        var actual = new byte[plain.Length];

        await Assert.That(AesCbcNoPadding.TryEncrypt(key, iv, plain, actual)).IsTrue();
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    /// <summary>A key or message of the wrong size is refused, and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesWrongSizes()
    {
        var destination = new byte[AesCbcNoPadding.BlockLength];
        var iv = new byte[AesCbcNoPadding.BlockLength];
        var plain = new byte[AesCbcNoPadding.BlockLength];

        using (Assert.Multiple())
        {
            await Assert.That(AesCbcNoPadding.TryEncrypt(new byte[Aes128 - 1], iv, plain, destination)).IsFalse();
            await Assert.That(AesCbcNoPadding.TryEncrypt(new byte[Aes128], iv.AsSpan(1), plain, destination)).IsFalse();
            await Assert.That(AesCbcNoPadding.TryEncrypt(new byte[Aes128], iv, plain.AsSpan(1), destination)).IsFalse();
            await Assert.That(AesCbcNoPadding.TryEncrypt(new byte[Aes128], iv, plain, destination.AsSpan(1))).IsFalse();
        }
    }
}
