// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Signing;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Keys the library holds for a while are zeroed once they are not needed.</summary>
public sealed class SecretWipingTests
{
    /// <summary>The length of an AES-128 file key.</summary>
    private const int Aes128KeyLength = 16;

    /// <summary>The permission word that allows everything.</summary>
    private const int AllPermissions = -4;

    /// <summary>The revision of an AES-128 handler.</summary>
    private const int Revision4 = 4;

    /// <summary>A handler that is disposed zeroes its file key and refuses to give it out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposedHandlerZeroesItsFileKey()
    {
        using var store = StoreOpening.Open(EncryptionTestFiles.Plain(), null);
        var key = RandomNumberGenerator.GetBytes(Aes128KeyLength);
        var handler = PdfSecurityHandler.CreateNew(key, Revision4, new(CryptMethod.Aes128, CryptMethod.Aes128), AllPermissions, true, new(store));

        handler.Dispose();

        await Assert.That(key.AsSpan().IndexOfAnyExcept((byte)0)).IsEqualTo(-1);
        await Assert.That(() => handler.FileKey.ToArray()).Throws<ObjectDisposedException>();
    }

    /// <summary>The keys that make a PDF MAC token are zeroed when wiped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WipedMacKeyMaterialIsZero()
    {
        var keys = new PdfMacKeyMaterial(RandomNumberGenerator.GetBytes(Aes128KeyLength), RandomNumberGenerator.GetBytes(Aes128KeyLength));

        keys.Wipe();

        await Assert.That(keys.FileKey.AsSpan().IndexOfAnyExcept((byte)0)).IsEqualTo(-1);
        await Assert.That(keys.Salt.AsSpan().IndexOfAnyExcept((byte)0)).IsEqualTo(-1);
    }
}
