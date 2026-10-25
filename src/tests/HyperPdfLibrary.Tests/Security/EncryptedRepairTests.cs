// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Syntax;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests that a damaged encrypted file keeps the objects packed in its encrypted object streams.</summary>
public sealed class EncryptedRepairTests
{
    /// <summary>The object number of the /Encrypt dictionary.</summary>
    private const int EncryptNumber = 3;

    /// <summary>The object number of the object stream.</summary>
    private const int ContainerNumber = 4;

    /// <summary>The page tree's object number.</summary>
    private const int PagesNumber = 2;

    /// <summary>The /Size of the trailer.</summary>
    private const int FileSize = 5;

    /// <summary>The catalog object, which exists only inside the object stream.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree object, which exists only inside the object stream.</summary>
    private const string Pages = "<< /Type /Pages /Kids [] /Count 0 >>";

    /// <summary>The catalog and page tree are found after the repaired table is indexed again with the file key.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepairedEncryptedFileIndexesObjectStreamsAfterAuthentication()
    {
        var file = DamagedEncryptedFile();
        using var store = StoreOpening.Open(file, null);
        await Assert.That(store.WasRepaired).IsTrue();
        await Assert.That(store.Security).IsNotNull();
        await Assert.That(store.Catalog.IsName(KnownName.Type, KnownName.Catalog)).IsTrue();
        await Assert.That(StoreReading.GetObject(store, new(PagesNumber, 0)).AsDictionary()!.IsName(KnownName.Type, KnownName.Pages)).IsTrue();
    }

    /// <summary>Builds an encrypted file with no cross-reference table whose catalog lives in an encrypted object stream.</summary>
    /// <returns>The file.</returns>
    private static byte[] DamagedEncryptedFile()
    {
        using var setup = EncryptionSetup.Revision4(true, new());
        return new RawPdf().Object(
        EncryptNumber,
        setup.EncryptText).ObjectStream(
        ContainerNumber,
        [1,
        PagesNumber],
        [Catalog,
        Pages],
        data =>
        setup.Handler.EncryptStream(
        new(
        ContainerNumber,
        0),
        data)).Append($"trailer\n<< /Size {FileSize} /Root 1 0 R /Encrypt {EncryptNumber} 0 R /ID [<{setup.FileIdHex}> <{setup.FileIdHex}>] >>\n%%EOF\n").ToArray();
    }
}
