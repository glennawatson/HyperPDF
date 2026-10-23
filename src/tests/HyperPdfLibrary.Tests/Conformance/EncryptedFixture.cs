// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Tests.Security;
using HyperPdfLibrary.Tests.Syntax;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Builds a small encrypted file that opens with the empty password.</summary>
internal static class EncryptedFixture
{
    /// <summary>The object number of the /Encrypt dictionary.</summary>
    private const int EncryptNumber = 3;

    /// <summary>The object number of the object stream.</summary>
    private const int ContainerNumber = 4;

    /// <summary>The page tree's object number.</summary>
    private const int PagesNumber = 2;

    /// <summary>The /Size of the trailer.</summary>
    private const int FileSize = 5;

    /// <summary>The catalog object, which sits inside the object stream.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree object, which sits inside the object stream.</summary>
    private const string Pages = "<< /Type /Pages /Kids [] /Count 0 >>";

    /// <summary>Builds the file.</summary>
    /// <returns>The file bytes.</returns>
    internal static byte[] Build()
    {
        using var setup = EncryptionSetup.Revision4(true, new());
        return new RawPdf()
            .Object(EncryptNumber, setup.EncryptText)
            .ObjectStream(ContainerNumber, [1, PagesNumber], [Catalog, Pages], data => setup.Handler.EncryptStream(new(ContainerNumber, 0), data))
            .Append($"trailer\n<< /Size {FileSize} /Root 1 0 R /Encrypt {EncryptNumber} 0 R /ID [<{setup.FileIdHex}> <{setup.FileIdHex}>] >>\n%%EOF\n")
            .ToArray();
    }
}
