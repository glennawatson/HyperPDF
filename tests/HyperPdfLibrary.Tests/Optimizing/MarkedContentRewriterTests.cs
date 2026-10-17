// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks path grouping and allocation-free operator classification.</summary>
public sealed class MarkedContentRewriterTests
{
    /// <summary>The scans performed after the working array is warm.</summary>
    private const int RepeatedScans = 128;

    /// <summary>Construction, clipping and painting remain one marked path.</summary>
    /// <param name="content">The operators to classify.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("0 0 m S")]
    [Arguments("0 0 l s")]
    [Arguments("0 0 1 1 2 2 c f")]
    [Arguments("0 0 1 1 v f*")]
    [Arguments("0 0 1 1 y B")]
    [Arguments("h B*")]
    [Arguments("0 0 1 1 re b")]
    [Arguments("0 0 1 1 re b*")]
    [Arguments("0 0 1 1 re W f")]
    [Arguments("0 0 1 1 re W* f*")]
    public async Task FindUnits_PathOperators_KeepsCompletePath(string content)
    {
        using var document = OpenEmpty();
        var bytes = Encoding.UTF8.GetBytes(content);
        var units = Find(document, bytes);

        await Assert.That(units.Length).IsEqualTo(1);
        await Assert.That(units[0]).IsEqualTo(new(0, bytes.Length, TagUnitKind.Path));
    }

    /// <summary>A paint operator cannot wrap a path across a state or text boundary.</summary>
    /// <param name="content">The operators to classify.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("S")]
    [Arguments("W S")]
    [Arguments("0 0 m n S")]
    [Arguments("0 0 m q Q S")]
    [Arguments("0 0 m BT ET S")]
    [Arguments("0 0 m 1 g S")]
    public async Task FindUnits_NoContiguousPath_DoesNotWrap(string content)
    {
        using var document = OpenEmpty();

        await Assert.That(Find(document, Encoding.UTF8.GetBytes(content)).Length).IsEqualTo(0);
    }

    /// <summary>Repeated classification uses the leased array without constructing operator tables.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FindUnits_WarmStorage_DoesNotAllocate()
    {
        using var document = OpenEmpty();
        var allocated = MeasureRepeatedScans(document);

        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>Copies the classified units before returning their rented array.</summary>
    /// <param name="document">The object store used to resolve resources.</param>
    /// <param name="content">The content bytes.</param>
    /// <returns>The independent units.</returns>
    private static TagUnit[] Find(PdfDocument document, ReadOnlySpan<byte> content)
    {
        RentedList<TagUnit> units = default;
        try
        {
            MarkedContentRewriter.FindUnits(content, document.Objects.Names, null, document.Objects, ref units);
            return units.Span.ToArray();
        }
        finally
        {
            units.Dispose();
        }
    }

    /// <summary>Measures synchronous scans after warming the reader and rented storage.</summary>
    /// <param name="document">The object store used to resolve resources.</param>
    /// <returns>The bytes allocated by repeated scans.</returns>
    private static long MeasureRepeatedScans(PdfDocument document)
    {
        RentedList<TagUnit> units = default;
        try
        {
            var content = "0 0 m 1 1 l h W* B* 0 0 1 1 re f BT (text) Tj ET"u8;
            MarkedContentRewriter.FindUnits(content, document.Objects.Names, null, document.Objects, ref units);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < RepeatedScans; i++)
            {
                units.Clear();
                MarkedContentRewriter.FindUnits(content, document.Objects.Names, null, document.Objects, ref units);
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        finally
        {
            units.Dispose();
        }
    }

    /// <summary>Opens an empty document that supplies name and object tables.</summary>
    /// <returns>The owned document.</returns>
    private static PdfDocument OpenEmpty() => PdfDocumentReader.Open(
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [] /Count 0 >>"),
        null);
}
