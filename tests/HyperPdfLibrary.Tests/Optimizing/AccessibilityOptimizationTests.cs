// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Structure preservation through renumbering, the catalog's accessibility entries, and inferred tags.</summary>
[NotInParallel]
public sealed class AccessibilityOptimizationTests
{
    /// <summary>The language the options give.</summary>
    private const string Language = "en-GB";

    /// <summary>The title of the untagged sample.</summary>
    private const string Title = "Quarterly Figures";

    /// <summary>A tagged document keeps its structure tree, parent tree and marked content ids through the rewrite.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsTheStructureTree()
    {
        var source = TaggedSamples.Basic();
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller);
        using var before = TaggedTestDocument.Open(source);
        using var after = TaggedTestDocument.Open(result.Bytes);

        await Assert.That(Signature(after)).IsEqualTo(Signature(before));
        await Assert.That(Spoken(after)).IsEqualTo(Spoken(before));
        var afterType = PdfDocumentTagged.GetStructureTree(after)!.GetMarkedContentParent(0, 1)!.MappedType;
        var beforeType = PdfDocumentTagged.GetStructureTree(before)!.GetMarkedContentParent(0, 1)!.MappedType;
        await Assert.That(afterType).IsEqualTo(beforeType);
        await Assert.That(after.Catalog.GetText(KnownName.Lang)).IsEqualTo(TaggedSamples.DocumentLanguage);
    }

    /// <summary>Missing entries are filled: language from the options, the title shown, and no /Marked without a structure tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FillsMissingEntries()
    {
        var result = OptimizerTestKit.Optimize(OptimizerSamples.UntaggedText(Title), PdfOptimizeOptions.Balanced with { Language = Language });
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var names = document.Objects.Names;

        await Assert.That(document.Catalog.GetText(KnownName.Lang)).IsEqualTo(Language);
        await Assert.That(document.Catalog.GetDictionary(KnownName.ViewerPreferences)!.GetBoolean(names.Intern("DisplayDocTitle"u8))).IsTrue();
        await Assert.That(document.Catalog.ContainsKey(KnownName.MarkInfo)).IsFalse();
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(Title);
    }

    /// <summary>With no language known, /Lang stays missing and the report says how to add one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsUnknownLanguage()
    {
        var result = OptimizerTestKit.Optimize(OptimizerSamples.UntaggedText(null), PdfOptimizeOptions.Balanced);
        using var document = PdfDocumentReader.Open(result.Bytes, null);

        await Assert.That(document.Catalog.ContainsKey(KnownName.Lang)).IsFalse();
        await Assert.That(result.Report.Skipped.Any(static skip => skip.Category == PdfOptimizeCategory.Accessibility)).IsTrue();
    }

    /// <summary>
    /// Inferred tags give an untagged page a heading, paragraphs and a figure in reading order, the page draws and reads
    /// the same, the figure is counted for alternative text and the XMP says the tags were inferred.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InfersTagsForUntaggedPages()
    {
        var source = OptimizerSamples.UntaggedText(Title);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality with { AddInferredTags = true, Language = Language });
        using var document = SkiaTestRegistration.Open(result.Bytes, null);
        var tagged = PdfReadingStructure.ReadTagged(document, 0);
        var types = new List<PdfSemanticRole>();
        var spoken = new StringBuilder();
        foreach (var node in tagged.Nodes)
        {
            Flatten(node, types, spoken);
        }

        await Assert.That(result.Report.TagsInferred).IsTrue();
        await Assert.That(result.Report.FiguresNeedingAltText).IsEqualTo(1);
        await Assert.That(PdfDocumentTagged.GetStructureTree(document)).IsNotNull();
        await Assert.That(document.Catalog.GetDictionary(KnownName.MarkInfo)!.GetBoolean(document.Objects.Names.Intern("Marked"u8))).IsTrue();
        await Assert.That(types).Contains(PdfSemanticRole.Heading);
        await Assert.That(types).Contains(PdfSemanticRole.Paragraph);
        await Assert.That(types).Contains(PdfSemanticRole.Figure);
        await Assert.That(spoken.ToString()).StartsWith($"Heading:{OptimizerSamples.Heading}");
        await Assert.That(PdfDocumentMetadata.GetXmp(document)!.GetValue(InferredTagsXmp.Namespace, InferredTagsXmp.Property)).IsEqualTo("true");
        await Assert.That(OptimizerTestKit.Text(result.Bytes)).IsEquivalentTo(OptimizerTestKit.Text(source));
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>A tagged document is never retagged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavesTaggedDocumentsAlone()
    {
        var source = TaggedSamples.Basic();
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality with { AddInferredTags = true });
        using var before = TaggedTestDocument.Open(source);
        using var after = TaggedTestDocument.Open(result.Bytes);

        await Assert.That(result.Report.TagsInferred).IsFalse();
        await Assert.That(Signature(after)).IsEqualTo(Signature(before));
    }

    /// <summary>Lists the roles and spoken text of the leaf nodes, depth first.</summary>
    /// <param name="node">The node.</param>
    /// <param name="roles">Receives every node's role.</param>
    /// <param name="spoken">Receives each leaf's role and text.</param>
    private static void Flatten(PdfSemanticNode node, List<PdfSemanticRole> roles, StringBuilder spoken)
    {
        roles.Add(node.Role);
        if (node.Role is PdfSemanticRole.Heading or PdfSemanticRole.Paragraph)
        {
            _ = spoken.Append(node.Role).Append(':').Append(node.SpokenText.Trim()).Append('|');
        }

        foreach (var child in node.Children)
        {
            Flatten(child, roles, spoken);
        }
    }

    /// <summary>Describes a structure tree: each element's type, page and kids, depth first.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The description.</returns>
    private static string Signature(PdfDocument document)
    {
        var text = new StringBuilder();
        foreach (var root in PdfDocumentTagged.GetStructureTree(document)!.Roots)
        {
            Describe(root, text);
        }

        return text.ToString();
    }

    /// <summary>Describes one element and its descendants.</summary>
    /// <param name="element">The element.</param>
    /// <param name="text">The description.</param>
    private static void Describe(PdfStructureElement element, StringBuilder text)
    {
        _ = text.Append('(').Append(element.MappedType).Append('@').Append(element.PageIndex).Append(element.AlternateText);
        foreach (var kid in element.Kids)
        {
            if (kid.Element is { } child)
            {
                Describe(child, text);
            }
            else
            {
                _ = text.Append(' ').Append(kid.Kind).Append(kid.Mcid);
            }
        }

        _ = text.Append(')');
    }

    /// <summary>Gets what a screen reader says for the page, node by node.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The spoken text.</returns>
    private static string Spoken(PdfDocument document)
    {
        var text = new StringBuilder();
        foreach (var node in PdfReadingStructure.ReadTagged(document, 0).Nodes)
        {
            _ = text.Append(node.Role).Append(':').Append(node.SpokenText).Append('|');
        }

        return text.ToString();
    }
}
