// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for encrypting a file for certificates (ISO 32000-2 7.6.5): RSA and ECDH recipients read back by the library and by OpenSSL.</summary>
public sealed class PdfPublicKeyWriteTests
{
    /// <summary>The kind name of an RSA recipient.</summary>
    private const string RsaKind = "rsa";

    /// <summary>The kind name of a P-256 recipient.</summary>
    private const string P256Kind = "p256";

    /// <summary>The kind name of a P-384 recipient.</summary>
    private const string P384Kind = "p384";

    /// <summary>The kind name of a P-521 recipient.</summary>
    private const string P521Kind = "p521";

    /// <summary>The kind name of a list with an RSA and a P-256 recipient together.</summary>
    private const string MixedKind = "mixed";

    /// <summary>The recipient lists a file with two sets of permissions has.</summary>
    private const int TwoLists = 2;

    /// <summary>The revision AES-128 gives a public-key file: its <c>/V</c>.</summary>
    private const int Version4 = 4;

    /// <summary>The revision AES-256 gives a public-key file: its <c>/V</c>.</summary>
    private const int Version5 = 5;

    /// <summary>The recipient opens a file written for them, with every cipher and key type.</summary>
    /// <param name="algorithm">The cipher.</param>
    /// <param name="kind">The recipient's key type.</param>
    /// <param name="version">The expected <c>/V</c>.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfEncryptionAlgorithm.Aes128, RsaKind, Version4)]
    [Arguments(PdfEncryptionAlgorithm.Aes128, P256Kind, Version4)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, RsaKind, Version5)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, P256Kind, Version5)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, P384Kind, Version5)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, P521Kind, Version5)]
    [Arguments(PdfEncryptionAlgorithm.Aes256, MixedKind, Version5)]
    public async Task RecipientOpensTheFile(PdfEncryptionAlgorithm algorithm, string kind, int version)
    {
        var holders = Holders(kind);
        try
        {
            var options = new PdfEncryptionOptions { Algorithm = algorithm, Recipients = Recipients(holders, PdfAccessPermissions.All) };

            var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), options, kind != MixedKind);

            await Assert.That(file.AsSpan().IndexOf(EncryptionTestFiles.PageTextBytes)).IsEqualTo(-1);
            foreach (var holder in holders)
            {
                using var document = PdfDocumentReader.OpenWith(file, new PdfOpenOptions { Certificate = holder });
                await Assert.That(PdfDocumentText.GetTextPage(document, 0).Text).Contains(EncryptionTestFiles.PageText);
            }

            using var store = StoreOpening.OpenWith(file, new PdfOpenOptions { Certificate = holders[0] });
            await Assert.That(store.Trailer.GetDictionary(KnownName.Encrypt)!.GetInt32(KnownName.V)).IsEqualTo(version);
        }
        finally
        {
            Dispose(holders);
        }
    }

    /// <summary>Each set of permissions has its own recipient list, and a recipient gets the rights of their list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EachRecipientGetsTheRightsOfTheirList()
    {
        var holders = Holders(MixedKind);
        try
        {
            var reader = new PdfEncryptionRecipient(RecipientCertificates.PublicOnly(holders[0]), PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent);
            var manager = new PdfEncryptionRecipient(RecipientCertificates.PublicOnly(holders[1]), PdfAccessPermissions.All | PdfAccessPermissions.ChangeEncryption);
            var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), new() { Recipients = [reader, manager] }, true);

            using var asReader = StoreOpening.OpenWith(file, new PdfOpenOptions { Certificate = holders[0] });
            using var asManager = StoreOpening.OpenWith(file, new PdfOpenOptions { Certificate = holders[1] });

            await Assert.That(asReader.Security!.Role).IsEqualTo(PdfSecurityRole.User);
            await Assert.That(PdfPermissionBits.FromWord(asReader.Security.Permissions) & ~PdfAccessPermissions.ExtractForAccessibility)
                .IsEqualTo(PdfAccessPermissions.Print | PdfAccessPermissions.CopyContent);
            await Assert.That(asManager.Security!.Role).IsEqualTo(PdfSecurityRole.Owner);
            await Assert.That(RecipientLists(file, holders[1]).Length).IsEqualTo(TwoLists);
        }
        finally
        {
            Dispose(holders);
        }
    }

    /// <summary>A certificate that is not a recipient cannot open the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherCertificateIsRefused()
    {
        var holders = Holders(RsaKind);
        using var stranger = RecipientCertificates.Rsa();
        try
        {
            var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), new() { Recipients = Recipients(holders, PdfAccessPermissions.All) }, true);

            var exception = await Assert.That(() => StoreOpening.OpenWith(file, new PdfOpenOptions { Certificate = stranger })).Throws<PdfException>();

            await Assert.That(exception!.Error).IsEqualTo(PdfError.Certificate);
        }
        finally
        {
            Dispose(holders);
        }
    }

    /// <summary>Metadata can stay clear in a public-key file, with the key made from the 0xFFFFFFFF marker as the standard says.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MetadataStaysClearWhenNotEncrypted()
    {
        var holders = Holders(P256Kind);
        try
        {
            var options = new PdfEncryptionOptions { Recipients = Recipients(holders, PdfAccessPermissions.All), EncryptMetadata = false };

            var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.WithMetadata(), options, false);

            await Assert.That(file.AsSpan().IndexOf(EncryptionTestFiles.MetadataMarkerBytes) >= 0).IsTrue();
            using var store = StoreOpening.OpenWith(file, new PdfOpenOptions { Certificate = holders[0] });
            await Assert.That(Encoding.UTF8.GetString(store.Catalog.Get(KnownName.Metadata).AsStream()!.DecodeToArray())).Contains(EncryptionTestFiles.MetadataMarker);
            await Assert.That(store.Security!.EncryptMetadata).IsFalse();
        }
        finally
        {
            Dispose(holders);
        }
    }

    /// <summary>OpenSSL, which shares no code with this library, decrypts each recipient list to the same seed and permissions.</summary>
    /// <param name="kind">The recipient's key type.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(RsaKind)]
    [Arguments(P256Kind)]
    [Arguments(P384Kind)]
    [Arguments(P521Kind)]
    public async Task OpensslDecryptsTheRecipientList(string kind, CancellationToken cancellationToken)
    {
        var holders = Holders(kind);
        try
        {
            var file = EncryptionTestFiles.Encrypt(EncryptionTestFiles.Plain(), new() { Recipients = Recipients(holders, PdfAccessPermissions.Print) }, true);
            var lists = RecipientLists(file, holders[0]);

            var result = await OpensslCms.DecryptAsync(lists[0], holders[0], cancellationToken);
            if (result is null)
            {
                Skip.Test("openssl is not installed.");
                return;
            }

            await Assert.That(result.Value.Content).IsNotNull().Because(result.Value.Output);
            var expected = CmsRecipientOpening.Open(lists, holders[0]).Content!;
            await Assert.That(result.Value.Content!.AsSpan().SequenceEqual(expected)).IsTrue();
            await Assert.That(result.Value.Content.Length).IsEqualTo(PublicKeyFileKey.SeedLength + PublicKeyFileKey.PermissionsLength);
        }
        finally
        {
            Dispose(holders);
        }
    }

    /// <summary>A password file can be encrypted again for certificates, and the file then opens with the certificate only.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PasswordFileCanBeEncryptedForCertificates()
    {
        var holders = Holders(RsaKind);
        try
        {
            var byPassword = EncryptionTestFiles.Encrypt(
                EncryptionTestFiles.Plain(),
                new() { UserPassword = EncryptionTestFiles.UserPassword, OwnerPassword = EncryptionTestFiles.OwnerPassword },
                true);

            var byCertificate = EncryptionTestFiles.Encrypt(byPassword, new() { Recipients = Recipients(holders, PdfAccessPermissions.All) }, true, EncryptionTestFiles.OwnerPassword);

            using var document = PdfDocumentReader.OpenWith(byCertificate, new PdfOpenOptions { Certificate = holders[0] });
            await Assert.That(PdfDocumentText.GetTextPage(document, 0).Text).Contains(EncryptionTestFiles.PageText);
            await Assert.That(() => StoreOpening.Open(byCertificate, EncryptionTestFiles.UserPassword)).Throws<PdfException>();
        }
        finally
        {
            Dispose(holders);
        }
    }

    /// <summary>Options that name recipients are refused when they are empty, mix in passwords, ask for AES-GCM or name a certificate that cannot receive a file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusableRecipientOptionsAreRefused()
    {
        var holders = Holders(RsaKind);
        using var signingOnly = RecipientCertificates.Rsa(X509KeyUsageFlags.DigitalSignature);
        try
        {
            var plain = EncryptionTestFiles.Plain();
            var good = Recipients(holders, PdfAccessPermissions.All);

            await Assert.That(() => EncryptionTestFiles.Encrypt(plain, new() { Recipients = [] }, true)).Throws<ArgumentException>();
            await Assert.That(() => EncryptionTestFiles.Encrypt(plain, new() { Recipients = good, UserPassword = EncryptionTestFiles.UserPassword }, true)).Throws<ArgumentException>();
            await Assert.That(() => EncryptionTestFiles.Encrypt(plain, new() { Recipients = good, Algorithm = PdfEncryptionAlgorithm.Aes256Gcm }, true)).Throws<ArgumentException>();
            var exception = await Assert.That(() => EncryptionTestFiles.Encrypt(plain, new() { Recipients = [new(signingOnly, PdfAccessPermissions.All)] }, true)).Throws<PdfException>();
            await Assert.That(exception!.Error).IsEqualTo(PdfError.Certificate);
        }
        finally
        {
            Dispose(holders);
        }
    }

    /// <summary>Makes the recipients' certificates with their private keys.</summary>
    /// <param name="kind">The kind of recipient list.</param>
    /// <returns>The certificates.</returns>
    private static X509Certificate2[] Holders(string kind) => kind switch
    {
        RsaKind => [RecipientCertificates.Rsa()],
        P256Kind => [RecipientCertificates.EllipticCurve(ECCurve.NamedCurves.nistP256)],
        P384Kind => [RecipientCertificates.EllipticCurve(ECCurve.NamedCurves.nistP384)],
        P521Kind => [RecipientCertificates.EllipticCurve(ECCurve.NamedCurves.nistP521)],
        _ => [RecipientCertificates.Rsa(), RecipientCertificates.EllipticCurve(ECCurve.NamedCurves.nistP256)],
    };

    /// <summary>Makes recipients from certificates, as someone who has only the public certificates would.</summary>
    /// <param name="holders">The certificates with keys.</param>
    /// <param name="permissions">The permissions of every recipient.</param>
    /// <returns>The recipients.</returns>
    private static PdfEncryptionRecipient[] Recipients(X509Certificate2[] holders, PdfAccessPermissions permissions)
    {
        var recipients = new PdfEncryptionRecipient[holders.Length];
        for (var i = 0; i < holders.Length; i++)
        {
            recipients[i] = new(RecipientCertificates.PublicOnly(holders[i]), permissions);
        }

        return recipients;
    }

    /// <summary>Reads the recipient lists out of a file's encryption dictionary.</summary>
    /// <param name="file">The file.</param>
    /// <param name="holder">A recipient's certificate with its key.</param>
    /// <returns>The CMS objects.</returns>
    private static byte[][] RecipientLists(byte[] file, X509Certificate2 holder)
    {
        using var store = StoreOpening.OpenWith(file, new PdfOpenOptions { Certificate = holder });
        var array = store.Trailer.GetDictionary(KnownName.Encrypt)!.GetDictionary(KnownName.CF)!.GetDictionary(store.Names.Intern("DefaultCryptFilter"))!.GetArray(KnownName.Recipients)!;
        var lists = new byte[array.Count][];
        for (var i = 0; i < lists.Length; i++)
        {
            lists[i] = array.Get(i).AsStringBytes().ToArray();
        }

        return lists;
    }

    /// <summary>Disposes certificates.</summary>
    /// <param name="certificates">The certificates.</param>
    private static void Dispose(X509Certificate2[] certificates)
    {
        foreach (var certificate in certificates)
        {
            certificate.Dispose();
        }
    }
}
