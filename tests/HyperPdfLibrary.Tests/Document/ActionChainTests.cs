// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>
/// Tests that action types, <c>/Next</c> chains and <c>/AA</c> triggers read as data (ISO 32000-2 12.6), that embedded
/// go-to actions keep their target and destination, and that malformed values give empty results instead of throwing.
/// </summary>
public sealed class ActionChainTests
{
    /// <summary>The first object after the catalog, page tree and page.</summary>
    private const int FirstObject = 4;

    /// <summary>The left coordinate of the sample go-to destination.</summary>
    private const float GoToLeft = 10;

    /// <summary>The top coordinate of the sample go-to destination.</summary>
    private const float GoToTop = 20;

    /// <summary>The zoom of the sample go-to destination.</summary>
    private const float GoToZoom = 2;

    /// <summary>The top coordinate of the named destination.</summary>
    private const float NamedTop = 50;

    /// <summary>The page in the other file of the remote go-to action.</summary>
    private const int RemotePage = 2;

    /// <summary>The page in the target of the sibling embedded go-to action.</summary>
    private const int SiblingPage = 4;

    /// <summary>The top coordinate of the sibling embedded go-to destination.</summary>
    private const float SiblingTop = 792;

    /// <summary>The page of the file attachment annotation in the attachment target.</summary>
    private const int AttachmentPage = 2;

    /// <summary>The number of steps in the grandchild target.</summary>
    private const int TwoSteps = 2;

    /// <summary>The number of actions in the array chain.</summary>
    private const int ArrayChain = 3;

    /// <summary>The number of actions in the long linear chain, more than the reader follows.</summary>
    private const int LongChain = 70;

    /// <summary>The most actions the reader follows down one chain.</summary>
    private const int MaxChainDepth = 64;

    /// <summary>The number of actions in the wide chain, more than the reader reads in one chain.</summary>
    private const int WideChain = 1100;

    /// <summary>The most actions the reader reads in one chain.</summary>
    private const int MaxChainActions = 1000;

    /// <summary>The destination name in the embedded go-to samples.</summary>
    private const string Chapter = "Chapter 1";

    /// <summary>The embedded file name in the embedded go-to samples.</summary>
    private const string EmbeddedName = "Embedded document";

    /// <summary>The relationship name of a step to a child document, which is also the close trigger's name.</summary>
    private const string Child = "C";

    /// <summary>The relationship name of a step to the parent document.</summary>
    private const string Parent = "P";

    /// <summary>The subtype of a named action.</summary>
    private const string Named = "Named";

    /// <summary>The named action that goes to the next page.</summary>
    private const string NextPage = "NextPage";

    /// <summary>Go-to, named, remote go-to and launch actions read their destinations and names.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The sample does not read as six actions.</exception>
    [Test]
    public async Task NavigationActionsAreRead()
    {
        using var document = StructureDocuments.Open(
            "/Names << /Dests << /Names [(Intro) [3 0 R /FitH 50]] >> >>",
            "<< /S /GoTo /D [3 0 R /XYZ 10 20 2] >>",
            "<< /S /GoTo /D (Intro) >>",
            "<< /S /GoTo /D [99 0 R /Fit] >>",
            "<< /S /Named /N /NextPage >>",
            "<< /S /GoToR /F (other.pdf) /D [2 /Fit] >>",
            "<< /S /Launch /F (notes.txt) >>");
        if (Nodes(document) is not [var explicitGoTo, var namedGoTo, var nowhere, var named, var remote, var launch])
        {
            throw new InvalidOperationException("The sample has six actions.");
        }

        await Assert.That(explicitGoTo.Action.Value).IsEqualTo(new GoToAction(new(0, GoToLeft, GoToTop, GoToZoom)));
        await Assert.That(namedGoTo.Action.Value).IsEqualTo(new GoToAction(new(0, null, NamedTop, null)));
        await Assert.That(nowhere.Action.Value).IsEqualTo(new UnsupportedAction("GoTo"));
        await Assert.That(named.Action.Value).IsEqualTo(new NamedAction(NextPage));
        await Assert.That(named.Subtype).IsEqualTo(Named);
        await Assert.That(remote.Action.Value is RemoteGoToAction { File: "other.pdf", PageIndex: RemotePage }).IsTrue();
        await Assert.That(launch.Action.Value).IsEqualTo(new LaunchAction("notes.txt"));
    }

    /// <summary>Embedded go-to actions keep each target step, the root file and the destination in the target, following ISO 32000-2 12.6.4.4's examples.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The sample does not read as five embedded go-to actions.</exception>
    [Test]
    public async Task EmbeddedGoToKeepsTargetsAndDestination()
    {
        using var document = StructureDocuments.Open(
            string.Empty,
            "<< /Type /Action /S /GoToE /D (Chapter 1) /T << /R /C /N (Embedded document) >> >>",
            "<< /S /GoToE /D (Chapter 1) /T << /R /P >> >>",
            "<< /S /GoToE /D [4 /XYZ 0 792 0] /NewWindow true /T << /R /P /T << /R /C /N (Another embedded document) >> >> >>",
            "<< /S /GoToE /D /Chapter#201 /F (someFile.pdf) /T << /R /C /N (Embedded document) >> >>",
            "<< /S /GoToE /D [0 /Fit] /NewWindow false /T << /R /C /N (Embedded document) /T << /R /C /P (A destination name) /A (annotName) >> >> >>");
        if (Embedded(document) is not [var child, var parent, var sibling, var external, var grandchild])
        {
            throw new InvalidOperationException("The sample has five embedded go-to actions.");
        }

        await Assert.That(child.EmbeddedFile).IsEqualTo(EmbeddedName);
        await Assert.That(child.NamedDestination).IsEqualTo(Chapter);
        await Assert.That(child.Destination).IsNull();
        await Assert.That(child.File).IsNull();
        await Assert.That(child.NewWindow).IsNull();
        await Assert.That(child.Targets).IsEquivalentTo([new PdfEmbeddedTarget(Child, EmbeddedName, null, null, null, null)]);
        await Assert.That(parent.Targets).IsEquivalentTo([new PdfEmbeddedTarget(Parent, null, null, null, null, null)]);
        await Assert.That(sibling.Destination).IsEqualTo(new PdfDestination(SiblingPage, 0, SiblingTop, 0));
        await Assert.That(sibling.NewWindow is true).IsTrue();
        await Assert.That(sibling.EmbeddedFile).IsNull();
        await Assert.That(sibling.Targets.Select(static step => step.Relationship)).IsEquivalentTo([Parent, Child]);
        await Assert.That(sibling.Targets[^1].FileName).IsEqualTo("Another embedded document");
        await Assert.That(external.File).IsEqualTo("someFile.pdf");
        await Assert.That(external.NamedDestination).IsEqualTo(Chapter);
        await Assert.That(grandchild.Destination).IsEqualTo(new PdfDestination(0, null, null, null));
        await Assert.That(grandchild.NewWindow is false).IsTrue();
        await Assert.That(grandchild.Targets.Length).IsEqualTo(TwoSteps);
        await Assert.That(grandchild.Targets[^1]).IsEqualTo(new(Child, null, null, "A destination name", null, "annotName"));
        await Assert.That(grandchild.HasTargetLoop).IsFalse();
    }

    /// <summary>A target path that loops is cut and flagged, a page reference is not taken as a page number, and missing entries give empty results.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The sample does not read as four objects.</exception>
    [Test]
    public async Task MalformedEmbeddedGoToIsBounded()
    {
        using var document = StructureDocuments.Open(
            string.Empty,
            "<< /S /GoToE /D [3 0 R /Fit] /T << /R /C /P 2 /A 1 >> >>",
            "<< /S /GoToE /D 7 /T 6 0 R /NewWindow (yes) >>",
            "<< /R /C /N (loop) /T 6 0 R >>",
            "<< /S /GoToE /T (child) /F 5 >>");
        if (Nodes(document) is not [var attachmentNode, var loopingNode, _, var emptyNode])
        {
            throw new InvalidOperationException("The sample has three embedded go-to actions and a target dictionary.");
        }

        var attachment = (EmbeddedGoToAction)attachmentNode.Action.Value!;
        var looping = (EmbeddedGoToAction)loopingNode.Action.Value!;
        var empty = (EmbeddedGoToAction)emptyNode.Action.Value!;

        await Assert.That(attachment.Destination).IsNull();
        await Assert.That(attachment.Targets[0]).IsEqualTo(new(Child, null, AttachmentPage, null, 1, null));
        await Assert.That(looping.HasTargetLoop).IsTrue();
        await Assert.That(looping.Targets.Length).IsEqualTo(1);
        await Assert.That(looping.EmbeddedFile).IsEqualTo("loop");
        await Assert.That(looping.Destination).IsNull();
        await Assert.That(looping.NamedDestination).IsNull();
        await Assert.That(looping.NewWindow).IsNull();
        await Assert.That(empty.Targets.Length).IsEqualTo(0);
        await Assert.That(empty.HasTargetLoop).IsFalse();
        await Assert.That(empty.File).IsNull();
        await Assert.That(empty.EmbeddedFile).IsNull();
    }

    /// <summary>A three-action <c>/Next</c> chain keeps its order, and an array of next actions keeps the array's order.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The sample does not read as seven actions.</exception>
    [Test]
    public async Task ThreeActionChainKeepsOrder()
    {
        using var document = StructureDocuments.Open(
            string.Empty,
            "<< /S /URI /URI (http://a/) /Next 5 0 R >>",
            "<< /S /Named /N /NextPage /Next 6 0 R >>",
            "<< /S /GoTo /D [3 0 R /Fit] >>",
            "<< /S /Named /N /PrevPage /Next [8 0 R 9 0 R 10 0 R] >>",
            "<< /S /Named /N /FirstPage >>",
            "<< /S /Named /N /LastPage >>",
            "<< /S /URI /URI (http://b/) >>");
        if (Nodes(document) is not [var first, _, _, var array, ..])
        {
            throw new InvalidOperationException("The sample has seven actions.");
        }

        await Assert.That(first.Action.Value).IsEqualTo(new UriAction("http://a/"));
        await Assert.That(first.Next.Length).IsEqualTo(1);
        await Assert.That(first.Next[0].Action.Value).IsEqualTo(new NamedAction(NextPage));
        await Assert.That(first.Next[0].Next.Length).IsEqualTo(1);
        await Assert.That(first.Next[0].Next[0].Action.Value is GoToAction { Destination.PageIndex: 0 }).IsTrue();
        await Assert.That(first.Next[0].Next[0].Next.Length).IsEqualTo(0);
        await Assert.That(array.Next.Length).IsEqualTo(ArrayChain);
        await Assert.That(array.Next.Select(static node => node.Subtype)).IsEquivalentTo([Named, Named, "URI"]);
        await Assert.That(array.Next[1].Action.Value).IsEqualTo(new NamedAction("LastPage"));
    }

    /// <summary>Missing or wrong-typed <c>/S</c> and <c>/Next</c> values, and loops, give empty results instead of throwing.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The sample does not read as seven actions.</exception>
    [Test]
    public async Task MalformedSubtypesAndNextValuesGiveEmptyResults()
    {
        using var document = StructureDocuments.Open(
            string.Empty,
            "<< /N /NextPage >>",
            "<< /S (GoTo) /D [3 0 R /Fit] >>",
            "<< /S 5 >>",
            "<< /S /URI /URI (http://a/) /Next 7 >>",
            "<< /S /URI /URI (http://b/) /Next [1 (x) /Name 99 0 R] >>",
            "<< /S /URI /URI (http://c/) /Next 9 0 R >>",
            "<< /S /URI /URI (http://d/) /Next (text) >>");
        var nodes = Nodes(document);
        if (nodes is not [var missing, var stringSubtype, var numberSubtype, ..])
        {
            throw new InvalidOperationException("The sample has seven actions.");
        }

        foreach (var node in new[] { missing, stringSubtype, numberSubtype })
        {
            await Assert.That(node.Subtype).IsEmpty();
            await Assert.That(node.Action.Value).IsEqualTo(new UnsupportedAction(string.Empty));
        }

        await Assert.That(Array.TrueForAll(nodes, static node => node.Next.Length == 0)).IsTrue();
    }

    /// <summary>A chain longer than the reader follows stops at the nesting limit, and a very wide chain stops at the action limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongChainsStopWithinLimits()
    {
        var linear = new string[LongChain];
        for (var i = 0; i < LongChain; i++)
        {
            linear[i] = $"<< /S /Named /N /NextPage /Next {FirstObject + i + 1} 0 R >>";
        }

        var wide = new string[WideChain + 1];
        wide[0] = $"<< /S /Named /N /NextPage /Next [{string.Join(' ', Enumerable.Range(FirstObject + 1, WideChain).Select(static number => $"{number} 0 R"))}] >>";
        Array.Fill(wide, "<< /S /Named /N /PrevPage >>", 1, WideChain);
        using var deepDocument = StructureDocuments.Open(string.Empty, linear);
        using var wideDocument = StructureDocuments.Open(string.Empty, wide);
        var depth = 1;
        for (var node = Node(deepDocument, FirstObject); node.Next.Length != 0; node = node.Next[0])
        {
            depth++;
        }

        await Assert.That(depth).IsEqualTo(MaxChainDepth);
        await Assert.That(Node(wideDocument, FirstObject).Next.Length + 1).IsEqualTo(MaxChainActions);
    }

    /// <summary>Catalog, page, annotation and field <c>/AA</c> triggers keep their event names, order and chains.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CatalogPageAndAnnotationTriggersAreRead()
    {
        using var document = StructureDocuments.OpenPage(
            "/AA << /WC 4 0 R /WS 5 0 R /DS 5 0 R /WP 5 0 R /DP 5 0 R >>",
            "/AA << /O 4 0 R /C 5 0 R >> /Annots [6 0 R]",
            "<< /S /JavaScript /JS (go\\(\\)) /Next 5 0 R >>",
            "<< /S /Named /N /Print >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /Rect [0 0 10 10] "
            + "/AA << /E 4 0 R /X 5 0 R /D 5 0 R /U 5 0 R /Fo 5 0 R /Bl 5 0 R /PO 5 0 R /PC 5 0 R /PV 5 0 R /PI 5 0 R /K 5 0 R /F 5 0 R /V 5 0 R /C 5 0 R >> >>");
        var catalog = PdfDocumentActions.GetTriggers(document);
        var page = PdfDocumentActions.GetTriggers(document, PdfDocumentPages.GetPage(document, 0));
        var widget = PdfDocumentPages.GetPage(document, 0).Dictionary.Array("Annots")!.GetDictionary(0)!;
        var annotation = PdfDocumentActions.GetTriggers(document, widget);

        await Assert.That(catalog.Select(static trigger => trigger.Event)).IsEquivalentTo(["WC", "WS", "DS", "WP", "DP"]);
        await Assert.That(catalog[0].Action.Action.Value).IsEqualTo(new JavaScriptAction("go()"));
        await Assert.That(catalog[0].Action.Next[0].Action.Value).IsEqualTo(new NamedAction("Print"));
        await Assert.That(page.Select(static trigger => trigger.Event)).IsEquivalentTo(["O", Child]);
        await Assert.That(page[^1].Action.Subtype).IsEqualTo(Named);
        await Assert.That(annotation.Select(static trigger => trigger.Event)).IsEquivalentTo(["E", "X", "D", "U", "Fo", "Bl", "PO", "PC", "PV", "PI", "K", "F", "V", Child]);
        await Assert.That(annotation[0].Action.Next.Length).IsEqualTo(1);
    }

    /// <summary>Wrong-typed <c>/AA</c> dictionaries and entries give no triggers instead of throwing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedTriggersGiveEmptyResults()
    {
        using var document = StructureDocuments.OpenPage(
            "/AA 5",
            "/AA << /O 7 /C (x) /X 99 0 R /D /Name >> /Annots [4 0 R]",
            "<< /Type /Annot /Subtype /Link /Rect [0 0 1 1] /AA [5 0 R] >>",
            "<< /S /Named /N /Print >>");

        await Assert.That(PdfDocumentActions.GetTriggers(document).Length).IsEqualTo(0);
        await Assert.That(PdfDocumentActions.GetTriggers(document, PdfDocumentPages.GetPage(document, 0)).Length).IsEqualTo(0);
        await Assert.That(PdfDocumentActions.GetTriggers(document, StructureDocuments.Dictionary(document, FirstObject)).Length).IsEqualTo(0);
    }

    /// <summary>Reads an action node.</summary>
    /// <param name="document">The document.</param>
    /// <param name="number">The action object number.</param>
    /// <returns>The node.</returns>
    private static PdfActionNode Node(PdfDocument document, int number) => PdfDocumentActions.ReadActionNode(document, StructureDocuments.Dictionary(document, number));

    /// <summary>Reads every object after the page as an action node, in object order.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The nodes.</returns>
    private static PdfActionNode[] Nodes(PdfDocument document)
    {
        var nodes = new PdfActionNode[document.Objects.Size - FirstObject];
        for (var i = 0; i < nodes.Length; i++)
        {
            nodes[i] = Node(document, FirstObject + i);
        }

        return nodes;
    }

    /// <summary>Reads every object after the page as an embedded go-to action, in object order.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The actions.</returns>
    private static EmbeddedGoToAction[] Embedded(PdfDocument document) => [.. Nodes(document).Select(static node => (EmbeddedGoToAction)node.Action.Value!)];
}
