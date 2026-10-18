// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests namespace role-map cycles, depth boundaries and independent repeated walks.</summary>
public sealed class AccessibilityNamespaceMapTests
{
    /// <summary>The namespace role-map depth limit.</summary>
    private const int DepthLimit = 32;

    /// <summary>The deliberately excessive number of namespace dictionaries.</summary>
    private const int DeepChainLength = 10_000;

    /// <summary>The shared namespace URI in UTF-16BE.</summary>
    private const string Utf16BigEndianUri = "<FEFF00750072006E003A006500780061006D0070006C0065003A007300680061007200650064>";

    /// <summary>The shared namespace URI in UTF-16LE.</summary>
    private const string Utf16LittleEndianUri = "<FFFE750072006E003A006500780061006D0070006C0065003A00730068006100720065006400>";

    /// <summary>The shared namespace URI in UTF-8 with its byte-order mark.</summary>
    private const string Utf8Uri = "<EFBBBF75726E3A6578616D706C653A736861726564>";

    /// <summary>Finite chains at the boundary end cleanly; cycles and deeper chains are bounded.</summary>
    /// <param name="length">The namespace dictionary count.</param>
    /// <param name="circular">Whether the last namespace returns to the first.</param>
    /// <param name="expected">The expected problem.</param>
    /// <param name="cancellationToken">Cancels PDF parsing.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(1, false, 0)]
    [Arguments(1, true, 1)]
    [Arguments(DepthLimit, false, 0)]
    [Arguments(DepthLimit, true, 1)]
    [Arguments(DepthLimit + 1, false, 0)]
    [Arguments(DeepChainLength, false, 1)]
    public async Task Read_BoundedChain_PreservesClassification(int length, bool circular, int expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(NamespaceRoleMapPdfs.Build(length, circular), null, cancellationToken);
        var space = StoreReading.GetObject(document.Objects, new(NamespaceRoleMapPdfs.FirstNamespace, 0)).AsDictionary()!;
        await Assert.That((int)AccessibilityNamespaceMap.Read(space, "X", cancellationToken)).IsEqualTo(expected);
        await Assert.That((int)AccessibilityNamespaceMap.Read(space, "X", cancellationToken)).IsEqualTo(expected);
    }

    /// <summary>Namespace URI equality follows decoded text even when the two dictionaries use different encodings.</summary>
    /// <param name="encodedUri">The shared URI as a PDF hex string.</param>
    /// <param name="targetType">The target type name.</param>
    /// <param name="expected">Cycle for the same type, or a repeated namespace for a different type.</param>
    /// <param name="cancellationToken">Cancels PDF parsing.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(Utf16BigEndianUri, "X", 1)]
    [Arguments(Utf16BigEndianUri, "Y", (int)NamespaceMapProblem.SameNamespace)]
    [Arguments(Utf16LittleEndianUri, "X", 1)]
    [Arguments(Utf16LittleEndianUri, "Y", (int)NamespaceMapProblem.SameNamespace)]
    [Arguments(Utf8Uri, "X", 1)]
    [Arguments(Utf8Uri, "Y", (int)NamespaceMapProblem.SameNamespace)]
    public async Task Read_EquivalentUriEncodings_DetectsRepeatedNamespace(
        string encodedUri,
        string targetType,
        int expected,
        CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
            $"<< /Type /Namespace /NS (urn:example:shared) /RoleMapNS << /X [/{targetType} 5 0 R] >> >>",
            $"<< /Type /Namespace /NS {encodedUri} >>");
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var space = StoreReading.GetObject(document.Objects, new(NamespaceRoleMapPdfs.FirstNamespace, 0)).AsDictionary()!;
        await Assert.That((int)AccessibilityNamespaceMap.Read(space, "X", cancellationToken)).IsEqualTo(expected);
    }

    /// <summary>Non-UTF-8 type-name bytes retain their PDF name identity when the next role map is looked up.</summary>
    /// <param name="cancellationToken">Cancels PDF parsing.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Read_BinaryTargetType_FollowsTheRawName(CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
            "<< /Type /Namespace /NS (urn:example:first) /RoleMapNS << /X [/#FF 5 0 R] >> >>",
            "<< /Type /Namespace /NS (urn:example:shared) /RoleMapNS << /#FF [/#FF 6 0 R] >> >>",
            "<< /Type /Namespace /NS (urn:example:shared) >>");
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var space = StoreReading.GetObject(document.Objects, new(NamespaceRoleMapPdfs.FirstNamespace, 0)).AsDictionary()!;
        await Assert.That(AccessibilityNamespaceMap.Read(space, "X", cancellationToken)).IsEqualTo(NamespaceMapProblem.Cycle);
    }

    /// <summary>A cancelled walk does not resolve namespace entries or retain its leased storage.</summary>
    /// <param name="cancellationToken">Cancels PDF parsing.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Read_Cancelled_ThrowsAndAllowsAnotherWalk(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(NamespaceRoleMapPdfs.Build(DepthLimit, true), null, cancellationToken);
        var space = StoreReading.GetObject(document.Objects, new(NamespaceRoleMapPdfs.FirstNamespace, 0)).AsDictionary()!;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.That(() => AccessibilityNamespaceMap.Read(space, "X", cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(AccessibilityNamespaceMap.Read(space, "X", cancellationToken)).IsEqualTo(NamespaceMapProblem.Cycle);
    }
}
