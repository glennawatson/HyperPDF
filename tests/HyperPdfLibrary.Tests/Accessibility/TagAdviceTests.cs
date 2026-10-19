// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks the suggestions of the Tagged PDF Best Practice Guide: Syntax.</summary>
public sealed class TagAdviceTests
{
    /// <summary>The entry that gives a tag content.</summary>
    private const string Content = " /K 0";

    /// <summary>Each suggestion names the guide's clause, and ordinary tags get none.</summary>
    /// <param name="parent">The first nested tag's type.</param>
    /// <param name="child">The tag inside it, which holds content unless it is empty.</param>
    /// <param name="empty">Whether the inner tag has no content.</param>
    /// <param name="clause">The expected clause, or empty for none about tags.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Sect", "P", false, "")]
    [Arguments("Sect", "Figure", true, "3.5")]
    [Arguments("Sect", "TD", true, "")]
    [Arguments("P", "P", false, "4.2.1")]
    [Arguments("L", "Caption", false, "")]
    public async Task AdviceNamesTheGuideClause(string parent, string child, bool empty, string clause, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(parent, child, empty ? string.Empty : Content), null, cancellationToken);

        var advice = PdfDocumentTagAdvice.Get(document, cancellationToken);

        await Assert.That(advice.Count(a => a.Clause != "7.2" && a.Clause == clause)).IsEqualTo(clause.Length == 0 ? 0 : 1);
    }

    /// <summary>A document with headings and no outline gets the navigation suggestion; one without headings does not.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeadingsWithoutOutlinesGetNavigationAdvice(CancellationToken cancellationToken)
    {
        using var withHeading = await PdfDocumentReader.OpenAsync(Build("Sect", "H1", Content), null, cancellationToken);
        using var without = await PdfDocumentReader.OpenAsync(Build("Sect", "P", Content), null, cancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(PdfDocumentTagAdvice.Get(withHeading, cancellationToken).Any(static a => a.Clause == "7.2")).IsTrue();
            await Assert.That(PdfDocumentTagAdvice.Get(without, cancellationToken).Any(static a => a.Clause == "7.2")).IsFalse();
        }
    }

    /// <summary>An untagged file has nothing to advise on.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedFileGetsNoAdvice(CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);

        await Assert.That(PdfDocumentTagAdvice.Get(document, cancellationToken).IsEmpty).IsTrue();
    }

    /// <summary>Builds a Document holding two nested tags.</summary>
    /// <param name="parent">The first nested tag's type.</param>
    /// <param name="child">The tag inside it.</param>
    /// <param name="content">The inner tag's content entry.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string parent, string child, string content) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
        "<< /Type /StructElem /S /Document /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R >>",
        $"<< /Type /StructElem /S /{parent} /P 4 0 R /K [7 0 R] >>",
        $"<< /Type /StructElem /S /{child} /P 6 0 R /Pg 3 0 R{content} >>",
    ]);
}
