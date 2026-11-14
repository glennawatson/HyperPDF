// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Windowing.Tests;

/// <summary>A fixed accessible tree for tests: an application holding a window, which holds a page of two text lines.</summary>
internal sealed class FakeAccessibleTree : IAccessibleTree
{
    /// <summary>The application root id.</summary>
    internal const int ApplicationId = 1;

    /// <summary>The window id.</summary>
    internal const int WindowId = 2;

    /// <summary>The page id.</summary>
    internal const int PageId = 3;

    /// <summary>The first text line id.</summary>
    internal const int FirstLineId = 4;

    /// <summary>The second text line id.</summary>
    internal const int SecondLineId = 5;

    /// <summary>The width of the window.</summary>
    private const int Width = 400;

    /// <summary>The height of the window.</summary>
    private const int Height = 300;

    /// <summary>The height of one text line.</summary>
    private const int LineHeight = 20;

    /// <summary>The nodes by id.</summary>
    private readonly Dictionary<int, AccessibleNode> _nodes = [];

    /// <summary>Initializes a new instance of the <see cref="FakeAccessibleTree"/> class.</summary>
    internal FakeAccessibleTree()
    {
        Add(ApplicationId, -1, AccessibleRole.Application, "HyperPDF", [WindowId], new(0, 0, Width, Height));
        Add(WindowId, ApplicationId, AccessibleRole.Window, "Document window", [PageId], new(0, 0, Width, Height));
        Add(PageId, WindowId, AccessibleRole.Document, "Page 1", [FirstLineId, SecondLineId], new(0, 0, Width, Height));
        Add(FirstLineId, PageId, AccessibleRole.Paragraph, "First line", [], new(0, 0, Width, LineHeight));
        Add(SecondLineId, PageId, AccessibleRole.Paragraph, "Second line", [], new(0, LineHeight, Width, LineHeight));
    }

    /// <inheritdoc/>
    public int RootId => ApplicationId;

    /// <inheritdoc/>
    public int FocusedId => FirstLineId;

    /// <inheritdoc/>
    public IObservable<AccessibleChange> Changes { get; } = new SilentChanges();

    /// <inheritdoc/>
    public AccessibleNode? Get(int id) => _nodes.GetValueOrDefault(id);

    /// <inheritdoc/>
    public int HitTest(int x, int y)
    {
        var found = -1;
        foreach (var node in _nodes.Values)
        {
            var bounds = node.Bounds;
            if (x >= bounds.X && x < bounds.X + bounds.Width && y >= bounds.Y && y < bounds.Y + bounds.Height)
            {
                found = node.Id;
            }
        }

        return found;
    }

    /// <inheritdoc/>
    public bool DoAction(int id, int action) => false;

    /// <inheritdoc/>
    public bool Focus(int id) => _nodes.ContainsKey(id);

    /// <inheritdoc/>
    public bool SetCaret(int id, int offset) => false;

    /// <summary>Adds a node.</summary>
    /// <param name="id">The node id.</param>
    /// <param name="parentId">The parent id.</param>
    /// <param name="role">The role.</param>
    /// <param name="name">The name.</param>
    /// <param name="children">The child ids.</param>
    /// <param name="bounds">The bounds.</param>
    private void Add(int id, int parentId, AccessibleRole role, string name, int[] children, AccessibleBounds bounds) =>
        _nodes[id] = new(id, parentId, role, name, string.Empty, AccessibleStates.Enabled | AccessibleStates.Showing, bounds, children, 0, 0, 0, string.Empty, [], null, 0);

    /// <summary>A change stream that never produces a change.</summary>
    private sealed class SilentChanges : IObservable<AccessibleChange>
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<AccessibleChange> observer) => new Nothing();

        /// <summary>A subscription with nothing to release.</summary>
        private sealed class Nothing : IDisposable
        {
            /// <inheritdoc/>
            public void Dispose()
            {
                // There is no stream to unsubscribe from.
            }
        }
    }
}
