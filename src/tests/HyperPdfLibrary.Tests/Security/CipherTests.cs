// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for the managed <see cref="Md5"/> and <see cref="Rc4"/>.</summary>
public sealed class CipherTests
{
    /// <summary>MD5 matches the RFC 1321 test suite, including inputs that need a second padding block.</summary>
    /// <param name="input">The message.</param>
    /// <param name="expected">The digest in hexadecimal.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", "d41d8cd98f00b204e9800998ecf8427e")]
    [Arguments("abc", "900150983cd24fb0d6963f7d28e17f72")]
    [Arguments("message digest", "f96b697d7cb7938d525a2f31aaf161d0")]
    [Arguments("abcdefghijklmnopqrstuvwxyz", "c3fcd3d76192e4007dfb496cca67e13b")]
    [Arguments("12345678901234567890123456789012345678901234567890123456789012345678901234567890", "57edf4a22be3c955ac49da2e2107b67a")]
    public async Task Md5MatchesRfc1321(string input, string expected)
    {
        var digest = new byte[Md5.HashLength];
        _ = Md5.HashData(Encoding.ASCII.GetBytes(input), digest);

        await Assert.That(Convert.ToHexStringLower(digest)).IsEqualTo(expected);
    }

    /// <summary>RC4 matches the published test vectors and is its own inverse.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Rc4MatchesTestVector()
    {
        var data = "Plaintext"u8.ToArray();
        Rc4.Apply("Key"u8, data);

        await Assert.That(Convert.ToHexString(data)).IsEqualTo("BBF316E8D940AF0AD3");

        Rc4.Apply("Key"u8, data);
        await Assert.That(Encoding.ASCII.GetString(data)).IsEqualTo("Plaintext");
    }
}
