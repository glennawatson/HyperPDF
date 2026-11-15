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

    /// <summary>The provider option for a server side provider, called on UI Automation's own threads so a client in this process cannot deadlock the window thread.</summary>
    private const int ServerSideProvider = 0x2;

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
    public void GetProviderOptions(out int options) => options = ServerSideProvider;

    /// <inheritdoc/>
    public void GetPatternProvider(int patternId, out nint provider) => provider = 0;

    /// <inheritdoc/>
    public void GetPropertyValue(int propertyId, out UiaVariant value) =>
        value = _bridge.Tree?.Get(_id) is { } node ? UiaProperties.Read(propertyId, node) : default;

    /// <inheritdoc/>
    public void GetHostRawElementProvider(out nint provider)
    {
        provider = 0;
        if (_bridge.IsWindowRoot(_id))
        {
            _ = NativeMethods.HostProviderFromWindow((nint)_bridge.Window, out provider);
        }
    }

    /// <inheritdoc/>
    public void Navigate(int direction, out IRawElementProviderFragment? result)
    {
        result = null;
        if (_bridge.Tree is not { } tree || (direction == ParentDirection && _bridge.IsWindowRoot(_id)))
        {
            return;
        }

        var target = direction switch
        {
            ParentDirection => AccessibleNavigation.Parent(tree, _id),
            NextDirection => _bridge.IsWindowRoot(_id) ? AccessibleNavigation.None : AccessibleNavigation.NextSibling(tree, _id),
            PreviousDirection => _bridge.IsWindowRoot(_id) ? AccessibleNavigation.None : AccessibleNavigation.PreviousSibling(tree, _id),
            FirstChildDirection => AccessibleNavigation.FirstChild(tree, _id),
            LastChildDirection => AccessibleNavigation.LastChild(tree, _id),
            _ => AccessibleNavigation.None,
        };
        result = target == AccessibleNavigation.None ? null : _bridge.ProviderFor(target);
    }

    /// <inheritdoc/>
    public void GetRuntimeId(out nint runtimeId) => runtimeId = _bridge.IsWindowRoot(_id) ? 0 : UiaRuntimeIds.Create(_id);

    /// <inheritdoc/>
    public void GetBoundingRectangle(out UiaRect bounds) =>
        bounds = _bridge.Tree?.Get(_id) is { Bounds: var b } ? new(b.X, b.Y, b.Width, b.Height) : default;

    /// <inheritdoc/>
    public void GetEmbeddedFragmentRoots(out nint roots) => roots = 0;

    /// <inheritdoc/>
    public void SetFocus() => _ = _bridge.Tree?.Focus(_id);

    /// <inheritdoc/>
    public void GetFragmentRoot(out IRawElementProviderFragmentRoot? root) => root = _bridge.RootProvider;

    /// <inheritdoc/>
    public void ElementProviderFromPoint(double x, double y, out IRawElementProviderFragment? result)
    {
        var id = _bridge.Tree?.HitTest((int)x, (int)y) ?? AccessibleNavigation.None;
        result = id == AccessibleNavigation.None ? null : _bridge.ProviderFor(id);
    }

    /// <inheritdoc/>
    public void GetFocus(out IRawElementProviderFragment? result)
    {
        var id = _bridge.Tree?.FocusedId ?? AccessibleNavigation.None;
        result = id == AccessibleNavigation.None ? null : _bridge.ProviderFor(id);
    }
}
