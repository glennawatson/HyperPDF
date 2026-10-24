// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.Versioning;
using HyperPdf.Core.Accessibility;
using HyperPdf.Platform.Windows.Accessibility.Uia;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>
/// Checks the parts of the UI Automation provider that are plain managed code: which patterns an element offers, its properties, the
/// way UI Automation moves through the tree, and the text ranges a screen reader moves by word and line. The calls into Windows itself
/// are checked by running the app on Windows. The class carries the Windows annotation only because the assembly it tests does; none of
/// the code these tests reach calls into Windows, so they run on every system.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UiaProviderTests
{
    /// <summary>The number the window is given.</summary>
    private const long WindowNumber = 4242;

    /// <summary>The UI Automation number of the heading level 2.</summary>
    private const int LevelTwoNumber = 80_052;

    /// <summary>A number of elements that no tree holds.</summary>
    private const int MissingId = 999;

    /// <summary>The HRESULT that tells UI Automation an element has gone.</summary>
    private const uint ElementGoneCode = 0x80040201;

    /// <summary>The HRESULT that tells UI Automation a call is not supported.</summary>
    private const uint NotSupportedCode = 0x80040204;

    /// <summary>The length of the first word of the page text, with its space.</summary>
    private const int FirstWordLength = 6;

    /// <summary>The length of the text the tests ask for at most.</summary>
    private const int FiveCharacters = 5;

    /// <summary>Verifies that a button offers the invoke pattern, a page of text the text pattern, and a heading neither.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Patterns_FollowTheActionsAndTheText()
    {
        var tree = MakeTree();

        await Assert.That(UiaPatterns.Offers((int)UiaPatternId.Invoke, tree.Nodes[FakeAccessibleTree.ButtonId])).IsTrue();
        await Assert.That(UiaPatterns.Offers((int)UiaPatternId.Toggle, tree.Nodes[FakeAccessibleTree.ButtonId])).IsFalse();
        await Assert.That(UiaPatterns.Offers((int)UiaPatternId.Text, tree.Nodes[FakeAccessibleTree.PageId])).IsTrue();
        await Assert.That(UiaPatterns.Offers((int)UiaPatternId.Value, tree.Nodes[FakeAccessibleTree.PageId])).IsFalse();
        await Assert.That(UiaPatterns.Offers((int)UiaPatternId.Text, tree.Nodes[FakeAccessibleTree.HeadingId])).IsFalse();
    }

    /// <summary>Verifies that a heading is reported as text with its level, and a button as a button with its place in a set.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Properties_ReportHeadingsAndPlaces()
    {
        var tree = MakeTree();
        var heading = tree.Nodes[FakeAccessibleTree.HeadingId];
        var button = tree.Nodes[FakeAccessibleTree.ButtonId];

        await Assert.That(UiaProperties.Read((int)UiaPropertyId.HeadingLevel, heading)).IsEqualTo(ComVariant.FromInt32(LevelTwoNumber));
        await Assert.That(UiaProperties.Read((int)UiaPropertyId.ControlType, heading)).IsEqualTo(ComVariant.FromInt32((int)UiaControlType.Text));
        await Assert.That(UiaProperties.Read((int)UiaPropertyId.ControlType, button)).IsEqualTo(ComVariant.FromInt32((int)UiaControlType.Button));
        await Assert.That(UiaProperties.Read((int)UiaPropertyId.PositionInSet, button)).IsEqualTo(ComVariant.FromInt32(1));
        await Assert.That(UiaProperties.Read((int)UiaPropertyId.HeadingLevel, button)).IsEqualTo(default(ComVariant));
    }

    /// <summary>Verifies that UI Automation can walk from the window to its children and back, and that the window is a root with no parent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Navigation_WalksTheTree()
    {
        var tree = MakeTree();
        var window = tree.Nodes[FakeAccessibleTree.WindowId];

        await Assert.That(UiaNavigation.Find(tree, window, NavigateDirection.FirstChild)).IsEqualTo(FakeAccessibleTree.HeadingId);
        await Assert.That(UiaNavigation.Find(tree, window, NavigateDirection.LastChild)).IsEqualTo(FakeAccessibleTree.ButtonId);
        await Assert.That(UiaNavigation.Find(tree, window, NavigateDirection.Parent)).IsEqualTo(-1);
        await Assert.That(UiaNavigation.Find(tree, tree.Nodes[FakeAccessibleTree.HeadingId], NavigateDirection.NextSibling)).IsEqualTo(FakeAccessibleTree.PageId);
        await Assert.That(UiaNavigation.Find(tree, tree.Nodes[FakeAccessibleTree.PageId], NavigateDirection.PreviousSibling)).IsEqualTo(FakeAccessibleTree.HeadingId);
        await Assert.That(UiaNavigation.Find(tree, tree.Nodes[FakeAccessibleTree.ButtonId], NavigateDirection.NextSibling)).IsEqualTo(-1);
        await Assert.That(UiaNavigation.Find(tree, tree.Nodes[FakeAccessibleTree.ButtonId], NavigateDirection.Parent)).IsEqualTo(FakeAccessibleTree.WindowId);
    }

    /// <summary>Verifies that an element finds the root of its window and that the root is the window's element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Elements_FindTheRootOfTheirWindow()
    {
        var elements = new UiaElements(MakeTree());

        await Assert.That(elements.RootOfWindow(WindowNumber)?.Id).IsEqualTo(FakeAccessibleTree.WindowId);
        await Assert.That(elements.RootOf(elements.Tree.Get(FakeAccessibleTree.ButtonId)!)?.Id).IsEqualTo(FakeAccessibleTree.WindowId);
        await Assert.That(elements.RootOfWindow(WindowNumber + 1)).IsNull();
        await Assert.That(elements.Get(FakeAccessibleTree.PageId)).IsSameReferenceAs(elements.Get(FakeAccessibleTree.PageId));
    }

    /// <summary>Verifies that invoking an element carries out its click action.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Invoke_CarriesOutTheClickAction()
    {
        var tree = MakeTree();
        var elements = new UiaElements(tree);

        elements.Get(FakeAccessibleTree.ButtonId).Invoke();

        await Assert.That(tree.LastAction).IsEqualTo(0);
    }

    /// <summary>Verifies that a call on an element that has gone fails with the error that makes UI Automation drop it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GoneElement_FailsWithTheElementNotAvailableError()
    {
        var elements = new UiaElements(MakeTree());

        var error = Capture(() => elements.Get(MissingId).GetPropertyValue((int)UiaPropertyId.Name));

        await Assert.That((uint)error!.HResult).IsEqualTo(ElementGoneCode);
    }

    /// <summary>Verifies that a call an element does not support fails with the not supported error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnsupportedCall_FailsWithTheNotSupportedError()
    {
        var elements = new UiaElements(MakeTree());

        var error = Capture(() => elements.Get(FakeAccessibleTree.ButtonId).Toggle());

        await Assert.That((uint)error!.HResult).IsEqualTo(NotSupportedCode);
    }

    /// <summary>Verifies that the document range holds all of the text and a clone of it is a separate range with the same text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DocumentRange_HoldsAllTheText()
    {
        var document = MakeTextElement();
        var range = document.GetDocumentRange();
        var clone = range.Clone();

        await Assert.That(range.GetText(-1)).IsEqualTo(FakeAccessibleTree.PageText);
        await Assert.That(clone.GetText(FiveCharacters)).IsEqualTo(FakeAccessibleTree.PageText[..FiveCharacters]);
        await Assert.That(range.Compare(clone)).IsTrue();
    }

    /// <summary>Verifies that a range collapsed to the start grows to the word and then moves word by word the way a screen reader reads.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Range_MovesWordByWord()
    {
        var range = MakeTextElement().GetDocumentRange();
        range.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start);

        range.ExpandToEnclosingUnit(TextRangeUnit.Word);
        var first = range.GetText(-1);
        range.Move(TextRangeUnit.Word, 1, out var moved);

        await Assert.That(first).IsEqualTo(FakeAccessibleTree.PageText[..FirstWordLength]);
        await Assert.That(moved).IsEqualTo(1);
        await Assert.That(range.GetText(-1)).IsEqualTo("sentence ");
    }

    /// <summary>Verifies that expanding to a line holds the whole first line with its line feed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Range_ExpandsToALine()
    {
        var range = MakeTextElement().GetDocumentRange();
        range.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start);

        range.ExpandToEnclosingUnit(TextRangeUnit.Line);

        await Assert.That(range.GetText(-1)).IsEqualTo("First sentence here. Second one follows.\n");
    }

    /// <summary>Verifies that moving the end by a word grows the range and that comparing the ends tells their order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Range_MovesAnEndAndComparesEnds()
    {
        var document = MakeTextElement();
        var range = document.GetDocumentRange();
        var whole = document.GetDocumentRange();
        range.MoveEndpointByRange(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.Start);

        range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextRangeUnit.Word, 1, out var moved);
        whole.CompareEndpoints(TextPatternRangeEndpoint.End, range, TextPatternRangeEndpoint.End, out var wholeVersusWord);
        range.CompareEndpoints(TextPatternRangeEndpoint.Start, whole, TextPatternRangeEndpoint.Start, out var sameStart);

        await Assert.That(moved).IsEqualTo(1);
        await Assert.That(range.GetText(-1)).IsEqualTo(FakeAccessibleTree.PageText[..FirstWordLength]);
        await Assert.That(wholeVersusWord).IsGreaterThan(0);
        await Assert.That(sameStart).IsEqualTo(0);
    }

    /// <summary>Verifies that text can be found in a range, forward and backward.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Range_FindsText()
    {
        var range = MakeTextElement().GetDocumentRange();

        var forward = range.FindText("s", false, true);
        var backward = range.FindText("s", true, true);
        var missing = range.FindText("zzz", false, true);

        await Assert.That(forward?.GetText(-1)).IsEqualTo("s");
        await Assert.That(forward?.Compare(backward!)).IsFalse();
        await Assert.That(missing).IsNull();
    }

    /// <summary>Makes the fake tree with a window number on the window, so the window has the number UI Automation knows it by.</summary>
    /// <returns>The tree.</returns>
    private static FakeAccessibleTree MakeTree()
    {
        var tree = new FakeAccessibleTree();
        tree.Nodes[FakeAccessibleTree.WindowId] = tree.Nodes[FakeAccessibleTree.WindowId] with { WindowNumber = WindowNumber };
        return tree;
    }

    /// <summary>Makes the provider of the element that holds the page text.</summary>
    /// <returns>The provider.</returns>
    private static UiaElement MakeTextElement() => new UiaElements(MakeTree()).Get(FakeAccessibleTree.PageId);

    /// <summary>Runs a call and returns the error it threw.</summary>
    /// <param name="call">The call.</param>
    /// <returns>The error, or <see langword="null"/> when the call did not fail.</returns>
    private static Exception? Capture(Action call)
    {
        try
        {
            call();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
