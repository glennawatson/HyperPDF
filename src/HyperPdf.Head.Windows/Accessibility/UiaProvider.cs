// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>The UI Automation element for one node of the accessible tree. JAWS, NVDA, Narrator and ZoomText read the window through these.</summary>
[SupportedOSPlatform("windows")]
[GeneratedComClass]
[DebuggerDisplay("UiaProvider node {_id}")]
internal sealed partial class UiaProvider : IRawElementProviderSimple, IRawElementProviderFragment, IRawElementProviderFragmentRoot
{
    /// <summary>The navigation direction for the parent.</summary>
    private const int ParentDirection = 0;

    /// <summary>The navigation direction for the next sibling.</summary>
    private const int NextDirection = 1;

    /// <summary>The navigation direction for the previous sibling.</summary>
    private const int PreviousDirection = 2;

    /// <summary>The navigation direction for the first child.</summary>
    private const int FirstChildDirection = 3;

    /// <summary>The navigation direction for the last child.</summary>
    private const int LastChildDirection = 4;

    /// <summary>The provider option for a server side provider that uses COM threading.</summary>
    private const int ServerSideComThreading = 0x22;

    /// <summary>The bridge that owns the tree.</summary>
    private readonly UiaAccessibilityBridge _bridge;

    /// <summary>The node id.</summary>
    private readonly int _id;

    /// <summary>Initializes a new instance of the <see cref="UiaProvider"/> class.</summary>
    /// <param name="bridge">The bridge that owns the tree.</param>
    /// <param name="id">The node id.</param>
    internal UiaProvider(UiaAccessibilityBridge bridge, int id)
    {
        _bridge = bridge;
        _id = id;
    }

    /// <inheritdoc/>
    public void GetProviderOptions(out int options) => options = ServerSideComThreading;

    /// <inheritdoc/>
    public void GetPatternProvider(int patternId, out nint provider) => provider = 0;

    /// <inheritdoc/>
    public void GetPropertyValue(int propertyId, out UiaVariant value)
    {
        var tree = _bridge.Tree;
        var index = tree.IndexOf(_id);
        value = index == AccessibleTree.None ? default : UiaProperties.Read(propertyId, tree.Node(index));
    }

    /// <inheritdoc/>
    public void GetHostRawElementProvider(out nint provider)
    {
        provider = 0;
        if (_bridge.Tree.Parent(_bridge.Tree.IndexOf(_id)) == AccessibleTree.None)
        {
            _ = NativeMethods.HostProviderFromWindow((nint)_bridge.Window, out provider);
        }
    }

    /// <inheritdoc/>
    public void Navigate(int direction, out IRawElementProviderFragment? result)
    {
        var tree = _bridge.Tree;
        var index = tree.IndexOf(_id);
        var target = direction switch
        {
            ParentDirection => tree.Parent(index),
            NextDirection => tree.Next(index),
            PreviousDirection => tree.Previous(index),
            FirstChildDirection => tree.FirstChild(index),
            LastChildDirection => tree.LastChild(index),
            _ => AccessibleTree.None,
        };
        result = target == AccessibleTree.None ? null : _bridge.ProviderFor(tree.Node(target).Id);
    }

    /// <inheritdoc/>
    public void GetRuntimeId(out nint runtimeId) => runtimeId = _bridge.Tree.Parent(_bridge.Tree.IndexOf(_id)) == AccessibleTree.None ? 0 : UiaRuntimeIds.Create(_id);

    /// <inheritdoc/>
    public void GetBoundingRectangle(out UiaRect bounds)
    {
        var tree = _bridge.Tree;
        var index = tree.IndexOf(_id);
        bounds = index == AccessibleTree.None ? default : _bridge.ToScreen(tree.Node(index).Bounds);
    }

    /// <inheritdoc/>
    public void GetEmbeddedFragmentRoots(out nint roots) => roots = 0;

    /// <inheritdoc/>
    public void SetFocus()
    {
        // Focus follows the viewer's own focus handling; a client asking to move it is answered when the viewer publishes the new focus.
    }

    /// <inheritdoc/>
    public void GetFragmentRoot(out IRawElementProviderFragmentRoot? root) => root = _bridge.RootProvider;

    /// <inheritdoc/>
    public void ElementProviderFromPoint(double x, double y, out IRawElementProviderFragment? result)
    {
        var index = _bridge.HitTestScreen(x, y);
        result = index == AccessibleTree.None ? null : _bridge.ProviderFor(_bridge.Tree.Node(index).Id);
    }

    /// <inheritdoc/>
    public void GetFocus(out IRawElementProviderFragment? result)
    {
        var index = _bridge.Tree.FocusedIndex();
        result = index == AccessibleTree.None ? null : _bridge.ProviderFor(_bridge.Tree.Node(index).Id);
    }
}
