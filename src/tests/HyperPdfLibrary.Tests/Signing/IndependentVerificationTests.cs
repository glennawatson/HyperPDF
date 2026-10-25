// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Tests.Signatures;
using HyperPdfLibrary.Tests.Writing;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Checks signed files with tools that do not use the library: openssl for the CMS signature, pdfsig for a PDF reader and qpdf for the structure.</summary>
public sealed partial class IndependentVerificationTests
{
    /// <summary>The hexadecimal digits a byte takes.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The numbers in a byte range.</summary>
    private const int RangeNumbers = 4;

    /// <summary>The line openssl prints when a signature verifies.</summary>
    private const string OpensslSuccess = "Verification successful";

    /// <summary>The line pdfsig prints when a signature verifies.</summary>
    private const string PdfsigSuccess = "Signature is Valid";

    /// <summary>The reason shown for a skipped check.</summary>
    private const string OpensslMissing = "openssl is not installed.";

    /// <summary>The rectangle of the visible signature.</summary>
    private static readonly HyperPdfLibrary.Objects.PdfRectangle VisibleBox = new(20, 20, 180, 80);

    /// <summary>An RSA signature verifies with openssl over the byte ranges, for both formats.</summary>
    /// <param name="format">The signature format.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfSignatureFormat.Pkcs7Detached)]
    [Arguments(PdfSignatureFormat.CadesDetached)]
    public async Task OpensslVerifiesRsaSignature(PdfSignatureFormat format, CancellationToken cancellationToken)
    {
        var file = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Format = format, Reason = "Approved" });

        await AssertOpensslVerifiesAsync(file, cancellationToken);
    }

    /// <summary>An ECDSA signature verifies with openssl over the byte ranges.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensslVerifiesEcdsaSignature(CancellationToken cancellationToken)
    {
        using var ec = SigningFixtures.CreateEc(ECCurve.NamedCurves.nistP256, "EC P-256");
        var file = SigningFixtures.Sign(SignatureFixtures.Blank(), ec, new() { Format = PdfSignatureFormat.CadesDetached });

        await AssertOpensslVerifiesAsync(file, cancellationToken);
    }

    /// <summary>Both signatures of a twice-signed file verify with openssl, so the second signature did not disturb the first.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensslVerifiesBothSignaturesOfATwiceSignedFile(CancellationToken cancellationToken)
    {
        var once = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new());
        var twice = SigningFixtures.Sign(once, SigningFixtures.SecondSigner, new());

        await AssertOpensslVerifiesAsync(twice, 0, cancellationToken);
        await AssertOpensslVerifiesAsync(twice, 1, cancellationToken);
    }

    /// <summary>A tampered file fails with openssl, so the check above is not vacuous.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensslRejectsAChangedByte(CancellationToken cancellationToken)
    {
        var file = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new());
        file[ByteRange(file, 0)[1] / DigitsPerByte]++;
        var folder = ExternalTool.MakeFolder();
        try
        {
            var result = await RunOpensslAsync(file, folder, 0, cancellationToken);
            if (result is not { } outcome)
            {
                Skip.Test(OpensslMissing);
                return;
            }

            await Assert.That(outcome.Output).DoesNotContain(OpensslSuccess);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>A PDF reader (pdfsig) accepts the signature.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfsigAcceptsSignature(CancellationToken cancellationToken)
    {
        var file = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Reason = "Approved" });
        var folder = ExternalTool.MakeFolder();
        try
        {
            var path = Path.Combine(folder, "signed.pdf");
            await File.WriteAllBytesAsync(path, file, cancellationToken);
            if (await ExternalTool.RunAsync("pdfsig", [path], cancellationToken) is not { } result)
            {
                Skip.Test("pdfsig is not installed.");
                return;
            }

            using (Assert.Multiple())
            {
                await Assert.That(result.Output).Contains(PdfsigSuccess);
                await Assert.That(result.Output).Contains("Total document signed");
            }
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Files at each stage of signing pass the structure check of qpdf.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task QpdfChecksSignedFiles(CancellationToken cancellationToken)
    {
        var approved = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Appearance = new() { Rectangle = VisibleBox } });
        var certified = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Certification = PdfMdpPermission.FormFillAndSign });
        var twice = SigningFixtures.Sign(approved, SigningFixtures.SecondSigner, new());
        var emptyField = SigningFixtures.Sign(SigningFixtures.WithEmptyField(string.Empty), SignatureFixtures.Signer, new() { FieldName = "Approval" });
        var compressed = SigningFixtures.Sign(HyperPdf.TestAssets.TestPdf.CreateCompressed(), SignatureFixtures.Signer, new());
        foreach (var (name, file) in new Dictionary<string, byte[]> { ["approved"] = approved, ["certified"] = certified, ["twice"] = twice, ["field"] = emptyField, ["compressed"] = compressed })
        {
            if (await QpdfCheck.RunAsync($"signed-{name}", file, cancellationToken) is not { } outcome)
            {
                Skip.Test("qpdf is not installed.");
                return;
            }

            await Assert.That(outcome.IsClean).IsTrue().Because($"{name}: {outcome.Output}");
        }
    }

    /// <summary>Reads the four numbers of a signature's /ByteRange.</summary>
    /// <param name="file">The file.</param>
    /// <param name="index">The zero-based signature.</param>
    /// <returns>The numbers.</returns>
    private static int[] ByteRange(byte[] file, int index)
    {
        var text = Encoding.Latin1.GetString(file);
        var found = ByteRangePattern().Matches(text)[index];
        var numbers = new int[RangeNumbers];
        for (var i = 0; i < numbers.Length; i++)
        {
            numbers[i] = int.Parse(found.Groups[i + 1].Value, CultureInfo.InvariantCulture);
        }

        return numbers;
    }

    /// <summary>Checks a signature of a file with openssl, which reads the signature and the signed bytes from files.</summary>
    /// <param name="file">The signed file.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    private static Task AssertOpensslVerifiesAsync(byte[] file, CancellationToken cancellationToken) => AssertOpensslVerifiesAsync(file, 0, cancellationToken);

    /// <summary>Checks one signature of a file with openssl, which reads the signature and the signed bytes from files.</summary>
    /// <param name="file">The signed file.</param>
    /// <param name="index">The zero-based signature.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    private static async Task AssertOpensslVerifiesAsync(byte[] file, int index, CancellationToken cancellationToken)
    {
        var folder = ExternalTool.MakeFolder();
        try
        {
            if (await RunOpensslAsync(file, folder, index, cancellationToken) is not { } result)
            {
                Skip.Test(OpensslMissing);
                return;
            }

            await Assert.That(result.Output).Contains(OpensslSuccess).Because(result.Output);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Writes the signature and the signed bytes of one signature to files and runs openssl over them.</summary>
    /// <param name="file">The signed file.</param>
    /// <param name="folder">The folder for the files.</param>
    /// <param name="index">The zero-based signature.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The result, or <see langword="null"/> when openssl is not installed.</returns>
    private static async Task<ExternalTool.ToolResult?> RunOpensslAsync(byte[] file, string folder, int index, CancellationToken cancellationToken)
    {
        var range = ByteRange(file, index);

        // The signed bytes are the file without the /Contents value, which sits between the two ranges (ISO 32000-2, 12.8.1).
        byte[] signed = [.. file.AsSpan(range[0], range[1]), .. file.AsSpan(range[2], range[3])];
        var contents = file.AsSpan(range[1] + 1, range[2] - range[1] - DigitsPerByte);
        var hex = Convert.FromHexString(Encoding.ASCII.GetString(contents));
        _ = AsnDecoder.ReadEncodedValue(hex, AsnEncodingRules.BER, out _, out _, out var consumed);
        var signedPath = Path.Combine(folder, "signed.bin");
        var cmsPath = Path.Combine(folder, "signature.der");
        await File.WriteAllBytesAsync(signedPath, signed, cancellationToken);
        await File.WriteAllBytesAsync(cmsPath, hex.AsMemory(0, consumed).ToArray(), cancellationToken);

        // The certificate is self-signed, so only the signature over the bytes is checked.
        return await ExternalTool.RunAsync(
            "openssl",
            ["cms", "-verify", "-inform", "DER", "-in", cmsPath, "-content", signedPath, "-binary", "-noverify", "-out", Path.Combine(folder, "content.out")],
            cancellationToken);
    }

    /// <summary>Matches a /ByteRange array.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]")]
    private static partial Regex ByteRangePattern();
}
