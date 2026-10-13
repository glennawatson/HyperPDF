// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Makes the recipient lists and the file key of a public-key encrypted file (ISO 32000-2 7.6.5.3): a random 20-byte seed
/// and 4 bytes of permissions are enveloped for each set of recipients with equal rights, and the file key is the hash of
/// the seed and every list.
/// </summary>
internal static class PublicKeyEncryptionWriter
{
    /// <summary>Creates the recipient lists and the file key.</summary>
    /// <param name="recipients">The recipients and their permissions.</param>
    /// <param name="algorithm">The cipher; AES-128 uses a 16-byte key from SHA-1 and AES-256 a 32-byte key from SHA-256.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <param name="fill">The random source.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="PdfException">A recipient's certificate cannot receive an encrypted file.</exception>
    internal static PublicKeyEntries Create(IReadOnlyList<PdfEncryptionRecipient> recipients, PdfEncryptionAlgorithm algorithm, bool encryptMetadata, FillBytes fill)
    {
        var seed = new byte[PublicKeyFileKey.SeedLength];
        fill(seed);
        var words = new List<int>();
        var groups = new List<List<X509Certificate2>>();
        foreach (var recipient in recipients)
        {
            var word = PdfPermissionBits.ToPublicKey(recipient.Permissions);
            var index = words.IndexOf(word);
            if (index < 0)
            {
                index = words.Count;
                words.Add(word);
                groups.Add([]);
            }

            groups[index].Add(recipient.Certificate);
        }

        var lists = new byte[groups.Count][];
        for (var i = 0; i < lists.Length; i++)
        {
            lists[i] = CmsEnvelopedDataWriter.Write(Content(seed, words[i]), groups[i], fill);
        }

        var keyLength = algorithm == PdfEncryptionAlgorithm.Aes128 ? CmsAlgorithms.Aes128KeyLength : CmsAlgorithms.Aes256KeyLength;
        return new(PublicKeyFileKey.Derive(seed, lists, encryptMetadata, keyLength), lists, words[0]);
    }

    /// <summary>Makes the content of an enveloped list: the seed, then the permission word, most significant byte first.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="permissions">The permission word.</param>
    /// <returns>The 24 bytes.</returns>
    private static byte[] Content(byte[] seed, int permissions)
    {
        var content = new byte[PublicKeyFileKey.SeedLength + PublicKeyFileKey.PermissionsLength];
        seed.CopyTo(content, 0);
        BinaryPrimitives.WriteInt32BigEndian(content.AsSpan(PublicKeyFileKey.SeedLength), permissions);
        return content;
    }
}
