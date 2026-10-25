// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>A small fixed tree of elements for tests of the AT-SPI server: an application, a window, a heading, a page of text and a button.</summary>
[DebuggerDisplay("FakeAccessibleTree: {Nodes.Count} elements")]
internal sealed class FakeAccessibleTree : IAccessibleTree
{
    /// <summary>The number of the window.</summary>
    internal const int WindowId = 1;

    /// <summary>The number of the heading.</summary>
    internal const int HeadingId = 2;

    /// <summary>The number of the page text.</summary>
    internal const int PageId = 3;

    /// <summary>The number of the button.</summary>
    internal const int ButtonId = 4;

    /// <summary>The text of the page.</summary>
    internal const string PageText = "First sentence here. Second one follows.\nNew line.";

    /// <summary>The level of the heading.</summary>
    private const int LevelTwo = 2;

    /// <summary>The place of the button among its neighbours.</summary>
    private const int FirstPlace = 1;

    /// <summary>The number of neighbours of the same kind as the button.</summary>
    private const int ThreePlaces = 3;

    /// <summary>The left edge of every element.</summary>
    private const int Left = 10;

    /// <summary>The top edge of every element.</summary>
    private const int Top = 20;

    /// <summary>The width of every element.</summary>
    private const int Width = 300;

    /// <summary>The height of every element.</summary>
    private const int Height = 40;

    /// <summary>Initializes a new instance of the <see cref="FakeAccessibleTree"/> class.</summary>
    internal FakeAccessibleTree() => Nodes = new Dictionary<int, AccessibleNode>
    {
        [0] = Make(0, -1, AccessibleRole.Application, "HyperPDF", [WindowId]),
        [WindowId] = Make(WindowId, 0, AccessibleRole.Window, "Test window", [HeadingId, PageId, ButtonId]),
        [HeadingId] = Make(HeadingId, WindowId, AccessibleRole.Heading, "A heading", []) with { HeadingLevel = LevelTwo },
        [PageId] = Make(PageId, WindowId, AccessibleRole.Document, "Page 1 of 1", []) with { Text = AccessibleText.Plain(PageText) },
        [ButtonId] = Make(ButtonId, WindowId, AccessibleRole.Button, "Save", []) with { Actions = ["click"], PositionInSet = FirstPlace, SetSize = ThreePlaces },
    };

    /// <inheritdoc/>
    public int RootId => 0;

    /// <inheritdoc/>
    public int FocusedId => -1;

    /// <inheritdoc/>
    public IObservable<AccessibleChange> Changes { get; } = new Signal<AccessibleChange>();

    /// <summary>Gets the elements by number.</summary>
    internal Dictionary<int, AccessibleNode> Nodes { get; }

    /// <summary>Gets the number of the action last asked for, or -1.</summary>
    internal int LastAction { get; private set; } = -1;

    /// <inheritdoc/>
    public AccessibleNode? Get(int id) => Nodes.GetValueOrDefault(id);

    /// <inheritdoc/>
    public int HitTest(int x, int y) => ButtonId;

    /// <inheritdoc/>
    public bool DoAction(int id, int action)
    {
        LastAction = action;
        return true;
    }

    /// <inheritdoc/>
    public bool Focus(int id) => true;

    /// <inheritdoc/>
    public bool SetCaret(int id, int offset) => true;

    /// <summary>Makes an element with a few default values.</summary>
    /// <param name="id">The element's number.</param>
    /// <param name="parent">The parent's number.</param>
    /// <param name="role">The role.</param>
    /// <param name="name">The name.</param>
    /// <param name="children">The numbers of the children.</param>
    /// <returns>The element.</returns>
    private static AccessibleNode Make(int id, int parent, AccessibleRole role, string name, int[] children) => new(
        id,
        parent,
        role,
        name,
        string.Empty,
        AccessibleStates.Enabled | AccessibleStates.Showing,
        new(Left, Top, Width, Height),
        children,
        0,
        0,
        0,
        string.Empty,
        [],
        null,
        0);
}
