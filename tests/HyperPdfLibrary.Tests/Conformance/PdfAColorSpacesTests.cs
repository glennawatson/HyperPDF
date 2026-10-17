// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks bounded traversal of pattern resource values.</summary>
public sealed class PdfAColorSpacesTests
{
    /// <summary>A value deeper than the resource-resolution bound.</summary>
    private const int ExcessiveDepth = 20;

    /// <summary>The adversarial depth used for in-memory container tests.</summary>
    private const int AdversarialDepth = 10_000;

    /// <summary>Ordinary Pattern names and singleton wrappers remain accepted.</summary>
    /// <param name="depth">The number of wrappers.</param>
    /// <param name="cancellationToken">Cancels traversal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task IsPattern_NormalSingletonDepth_ReturnsTrue(int depth, CancellationToken cancellationToken) =>
        await Assert.That(PdfAColorSpaces.IsPattern(SingletonPattern(depth, cancellationToken), cancellationToken)).IsTrue();

    /// <summary>Nested singleton arrays cannot bypass the resource-resolution depth bound.</summary>
    /// <param name="cancellationToken">Cancels sample construction.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IsPattern_ExcessiveSingletonDepth_ReturnsFalse(CancellationToken cancellationToken)
    {
        var value = SingletonPattern(ExcessiveDepth, cancellationToken);

        await Assert.That(PdfAColorSpaces.IsPattern(value, cancellationToken)).IsFalse();
    }

    /// <summary>A deep value returns after bounded work instead of exhausting the stack.</summary>
    /// <param name="cancellationToken">Cancels traversal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IsPattern_TenThousandSingletonArrays_ReturnsFalse(CancellationToken cancellationToken) =>
        await Assert.That(PdfAColorSpaces.IsPattern(SingletonPattern(AdversarialDepth, cancellationToken), cancellationToken)).IsFalse();

    /// <summary>A directly circular value terminates without recursive calls.</summary>
    /// <param name="cancellationToken">Cancels traversal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IsPattern_CircularSingleton_ReturnsFalse(CancellationToken cancellationToken)
    {
        var array = new PdfArray(null, 1);
        array.Add(PdfValue.FromArray(array));

        await Assert.That(PdfAColorSpaces.IsPattern(PdfValue.FromArray(array), cancellationToken)).IsFalse();
    }

    /// <summary>An indirect singleton-array cycle cannot bypass the colour-space bound.</summary>
    /// <param name="cancellationToken">Cancels opening and traversal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FromResource_CircularIndirectArray_ReturnsInvalid(CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /ColorSpace << /Loop 4 0 R >> >> >>",
            "[4 0 R]",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var walk = new PdfAGraphicsWalk(new(document, PdfALevel.PdfA2B, cancellationToken));
        var resources = PdfDocumentPages.GetPage(document, 0).Dictionary.GetDictionary(KnownName.Resources);
        var name = document.Objects.Names.Intern("Loop"u8);

        await Assert.That(PdfAColorSpaces.FromResource(walk, resources, name, false)).IsEqualTo(PdfAColorSpaceKind.Invalid);
    }

    /// <summary>Cancellation is observed even for an immediately available Pattern name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IsPattern_CancelledLeaf_ThrowsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(() => PdfAColorSpaces.IsPattern(PdfValue.FromName(KnownName.Pattern), cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Family classification preserves all standard array families and rejects device names in arrays.</summary>
    /// <param name="name">The family name.</param>
    /// <param name="expected">The expected colour-space kind.</param>
    /// <param name="cancellationToken">Cancels opening and traversal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments((int)KnownName.CalGray, (int)PdfAColorSpaceKind.Other)]
    [Arguments((int)KnownName.CalRGB, (int)PdfAColorSpaceKind.Other)]
    [Arguments((int)KnownName.Lab, (int)PdfAColorSpaceKind.Other)]
    [Arguments((int)KnownName.ICCBased, (int)PdfAColorSpaceKind.Other)]
    [Arguments((int)KnownName.Separation, (int)PdfAColorSpaceKind.Other)]
    [Arguments((int)KnownName.DeviceN, (int)PdfAColorSpaceKind.Other)]
    [Arguments((int)KnownName.Indexed, (int)PdfAColorSpaceKind.Other)]
    [Arguments((int)KnownName.Pattern, (int)PdfAColorSpaceKind.Pattern)]
    [Arguments((int)KnownName.DeviceRGB, (int)PdfAColorSpaceKind.Invalid)]
    public async Task FromResource_ArrayFamily_PreservesClassification(int name, int expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(TestPdf.Create(1), null, cancellationToken);
        var walk = new PdfAGraphicsWalk(new(document, PdfALevel.PdfA2B, cancellationToken));
        var spaces = new PdfDictionary(document.Objects);
        var resources = new PdfDictionary(document.Objects);
        var array = new PdfArray(document.Objects, [PdfValue.FromName((KnownName)name), PdfValue.FromInteger(0)]);
        var resourceName = document.Objects.Names.Intern("Space"u8);
        spaces.Set(resourceName, PdfValue.FromArray(array));
        resources.Set(KnownName.ColorSpace, PdfValue.FromDictionary(spaces));

        await Assert.That(PdfAColorSpaces.FromResource(walk, resources, resourceName, false)).IsEqualTo((PdfAColorSpaceKind)expected);
    }

    /// <summary>Creates singleton wrappers around the Pattern name.</summary>
    /// <param name="depth">The number of wrappers.</param>
    /// <param name="cancellationToken">Cancels construction.</param>
    /// <returns>The nested value.</returns>
    private static PdfValue SingletonPattern(int depth, CancellationToken cancellationToken)
    {
        var value = PdfValue.FromName(KnownName.Pattern);
        for (var i = 0; i < depth; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var array = new PdfArray(null, 1);
            array.Add(value);
            value = PdfValue.FromArray(array);
        }

        return value;
    }
}
