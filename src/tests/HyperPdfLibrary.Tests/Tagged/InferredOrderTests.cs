// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Tests for the reading order inferred from layout on untagged pages and from text recognition words.</summary>
[NotInParallel]
public sealed class InferredOrderTests
{
    /// <summary>The nodes on the two-column page: a heading and one paragraph per column.</summary>
    private const int TwoColumnNodes = 3;

    /// <summary>The right column's place.</summary>
    private const int RightIndex = 2;

    /// <summary>The left edge of the recognised words.</summary>
    private const float WordsLeft = 72;

    /// <summary>The right edge of the first word.</summary>
    private const float FirstRight = 120;

    /// <summary>The left edge of the second word.</summary>
    private const float SecondLeft = 125;

    /// <summary>The right edge of the second word.</summary>
    private const float SecondRight = 170;

    /// <summary>The right edge of the third word.</summary>
    private const float ThirdRight = 110;

    /// <summary>The top of the first line.</summary>
    private const float FirstTop = 100;

    /// <summary>The bottom of the first line.</summary>
    private const float FirstBottom = 112;

    /// <summary>The top of the second line.</summary>
    private const float SecondTop = 115;

    /// <summary>The bottom of the second line.</summary>
    private const float SecondBottom = 127;

    /// <summary>A recogniser's confidence.</summary>
    private const float Confidence = 90;

    /// <summary>The words in recognition order.</summary>
    private const int WordCount = 3;

    /// <summary>An untagged page is read heading first, then the left column, then the right, each node labelled as inferred.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsColumnsLeftToRight()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.TwoColumns());
        var page = PdfReadingStructure.Read(document, 0);

        await Assert.That(page.Origin).IsEqualTo(PdfNodeOrigin.Inferred);
        await Assert.That(page.Nodes.Count).IsEqualTo(TwoColumnNodes);
        await Assert.That(page.Nodes[0].Role).IsEqualTo(PdfSemanticRole.Heading);
        await Assert.That(page.Nodes[0].Level).IsEqualTo(1);
        await Assert.That(page.Nodes[0].Text).IsEqualTo("Big heading");
        await Assert.That(page.Nodes[1].Text).IsEqualTo("left0 left1 left2");
        await Assert.That(page.Nodes[RightIndex].Text).IsEqualTo("right0 right1 right2");
        foreach (var node in page.Nodes)
        {
            await Assert.That(node.Origin).IsEqualTo(PdfNodeOrigin.Inferred);
        }
    }

    /// <summary>A tagged page can also be read from its layout, for comparison.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InfersLayoutForTaggedPagesOnRequest()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.Basic());
        var page = PdfReadingStructure.InferLayout(document, 0);

        await Assert.That(page.Nodes[0].Text).IsEqualTo(TaggedSamples.Title);
        await Assert.That(page.Nodes[0].Role).IsEqualTo(PdfSemanticRole.Heading);
        foreach (var node in page.Nodes)
        {
            await Assert.That(node.Text).DoesNotContain(TaggedSamples.Header);
        }
    }

    /// <summary>Recognised words become nodes labelled as text recognition, with word indices as items.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BuildsNodesFromRecognisedWords()
    {
        PdfOcrWord[] words =
        [
            new("Hello", new(WordsLeft, FirstTop, FirstRight, FirstBottom), Confidence),
            new("world", new(SecondLeft, FirstTop, SecondRight, FirstBottom), Confidence),
            new("again", new(WordsLeft, SecondTop, ThirdRight, SecondBottom), Confidence),
        ];

        var page = PdfReadingStructure.FromOcrWords(0, words);
        var node = page.Nodes[0];

        await Assert.That(page.Origin).IsEqualTo(PdfNodeOrigin.Ocr);
        await Assert.That(page.Nodes.Count).IsEqualTo(1);
        await Assert.That(node.Origin).IsEqualTo(PdfNodeOrigin.Ocr);
        await Assert.That(node.Text).IsEqualTo("Hello world again");
        await Assert.That(node.Items.Length).IsEqualTo(WordCount);
        await Assert.That(page.NeedsTextRecognition).IsFalse();
    }
}
