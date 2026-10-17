// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.PdfUa;

/// <summary>Checks document ownership and cleanup of reusable structure-reference collections.</summary>
public sealed class PdfUaStructureReferencesTests
{
    /// <summary>A repeated scan returns empty working collections to the same document.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Check_RepeatedDocument_ReusesCleanBuffers()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        List<PdfUaFinding> findings = [];
        PdfUaStructureReferences.Check(document, findings, CancellationToken.None);
        var first = document.State.StructureReferences;
        var expected = findings.ToArray();
        findings.Clear();

        PdfUaStructureReferences.Check(document, findings, CancellationToken.None);

        await Assert.That(first).IsNotNull();
        await Assert.That(document.State.StructureReferences).IsSameReferenceAs(first);
        await Assert.That(findings).IsEquivalentTo(expected);
        await Assert.That(first!.PagesByObject.Count).IsEqualTo(0);
        await Assert.That(first.Drawn.Count).IsEqualTo(0);
        await Assert.That(first.Missing.Count).IsEqualTo(0);
        await Assert.That(first.Chain.Count).IsEqualTo(0);
        await Assert.That(first.NewKids.Count).IsEqualTo(0);
        await Assert.That(first.Deleted.Count).IsEqualTo(0);
        await Assert.That(first.Marks.Count).IsEqualTo(0);
        await Assert.That(first.Order.Count).IsEqualTo(0);
    }

    /// <summary>Reference scans never transfer working collections between documents.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Check_SeparateDocuments_IsolatesBuffers()
    {
        var bytes = TaggedSamples.Basic();
        using var first = PdfDocumentReader.Open(bytes, null);
        using var second = PdfDocumentReader.Open(bytes, null);
        List<PdfUaFinding> findings = [];
        PdfUaStructureReferences.Check(first, findings, CancellationToken.None);
        findings.Clear();
        PdfUaStructureReferences.Check(second, findings, CancellationToken.None);

        await Assert.That(first.State.StructureReferences).IsNotNull();
        await Assert.That(second.State.StructureReferences).IsNotNull();
        await Assert.That(first.State.StructureReferences).IsNotSameReferenceAs(second.State.StructureReferences);
    }

    /// <summary>Cancellation precedes tree creation and working-storage rental.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Check_CancelledToken_DoesNotCreateState()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        List<PdfUaFinding> findings = [];

        await Assert.That(() => PdfUaStructureReferences.Check(document, findings, cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(document.State.StructureTree).IsNull();
        await Assert.That(document.State.StructureReferences).IsNull();
        await Assert.That(findings.Count).IsEqualTo(0);
    }

    /// <summary>Cancellation leaves previously cleaned working storage available for another scan.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Check_CancelledWarmScan_PreservesIdleBuffers()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        List<PdfUaFinding> findings = [];
        PdfUaStructureReferences.Check(document, findings, CancellationToken.None);
        var buffers = document.State.StructureReferences;
        findings.Clear();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(() => PdfUaStructureReferences.Check(document, findings, cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(document.State.StructureReferences).IsSameReferenceAs(buffers);
        await Assert.That(findings.Count).IsEqualTo(0);
    }

    /// <summary>Disposing a document drops its idle reference collections.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Dispose_AfterCheck_DropsIdleBuffers()
    {
        var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        using (document)
        {
            PdfUaStructureReferences.Check(document, [], CancellationToken.None);
            await Assert.That(document.State.StructureReferences).IsNotNull();
        }

        await Assert.That(document.State.StructureReferences).IsNull();
    }
}
