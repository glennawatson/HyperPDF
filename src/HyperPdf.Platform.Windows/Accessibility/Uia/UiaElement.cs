// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>
/// One element of the app as UI Automation sees it. It holds only the element's number and reads the element's current node from
/// the tree on every call, so it never goes out of date; when the element has gone, calls fail with the error that makes UI
/// Automation drop it. Every element offers every pattern interface, but <see cref="IRawElementProviderSimple.GetPatternProvider"/>
/// only hands out the ones the element's node supports, and UI Automation asks there.
/// </summary>
[GeneratedComClass]
[DebuggerDisplay("UiaElement: {_id}")]
internal sealed partial class UiaElement :
    IRawElementProviderSimple,
    IRawElementProviderFragment,
    IRawElementProviderFragmentRoot,
    IInvokeProvider,
    IToggleProvider,
    IExpandCollapseProvider,
    ISelectionItemProvider,
    IValueProvider,
    ITextProvider
{
    /// <summary>The first value of a runtime identifier that asks UI Automation to add the identifier of the window.</summary>
    private const int AppendRuntimeId = 3;

    /// <summary>The provider list and tree this element belongs to.</summary>
    private readonly UiaElements _owner;

    /// <summary>The element's number.</summary>
    private readonly int _id;

    /// <summary>Initializes a new instance of the <see cref="UiaElement"/> class.</summary>
    /// <param name="owner">The provider list and tree.</param>
    /// <param name="id">The element's number.</param>
    internal UiaElement(UiaElements owner, int id)
    {
        _owner = owner;
        _id = id;
    }

    /// <summary>Gets the element's number.</summary>
    internal int Id => _id;

    /// <summary>Gets the element's current node; fails with an error UI Automation understands when the element has gone.</summary>
    internal AccessibleNode Node => _owner.Tree.Get(_id) ?? throw UiaErrors.ElementGone();

    /// <summary>Gets the element's text; fails with an error UI Automation understands when the element has gone or has no text.</summary>
    internal AccessibleText Text => Node.Text ?? throw UiaErrors.Unsupported();

    /// <summary>Gets the tree the element belongs to.</summary>
    internal IAccessibleTree Tree => _owner.Tree;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetProviderOptions(out ProviderOptions options) => options = ProviderOptions.ServerSideProvider;

    /// <inheritdoc/>
    public nint GetPatternProvider(int patternId) =>
        UiaPatterns.Offers(patternId, Node)
            ? (UiaPatternId)patternId switch
            {
                UiaPatternId.Invoke => NativeMethods.ComPointerOf<IInvokeProvider>(this),
                UiaPatternId.Toggle => NativeMethods.ComPointerOf<IToggleProvider>(this),
                UiaPatternId.ExpandCollapse => NativeMethods.ComPointerOf<IExpandCollapseProvider>(this),
                UiaPatternId.SelectionItem => NativeMethods.ComPointerOf<ISelectionItemProvider>(this),
                UiaPatternId.Value => NativeMethods.ComPointerOf<IValueProvider>(this),
                UiaPatternId.Text => NativeMethods.ComPointerOf<ITextProvider>(this),
                _ => 0,
            }
            : 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComVariant GetPropertyValue(int propertyId) => UiaProperties.Read(propertyId, Node);

    /// <inheritdoc/>
    public nint GetHostRawElementProvider()
    {
        var window = Node.WindowNumber;
        if (window == 0)
        {
            return 0;
        }

        Marshal.ThrowExceptionForHR(NativeMethods.UiaHostProviderFromHwnd((nint)window, out var host));
        return host;
    }

    /// <inheritdoc/>
    public IRawElementProviderFragment? Navigate(NavigateDirection direction)
    {
        var id = UiaNavigation.Find(_owner.Tree, Node, direction);
        return id < 0 ? null : _owner.Get(id);
    }

    /// <inheritdoc/>
    public nint GetRuntimeId() => Node.WindowNumber != 0 ? 0 : SafeArrays.FromInt32([AppendRuntimeId, _id]);

    /// <inheritdoc/>
    public UiaRect GetBoundingRectangle()
    {
        var bounds = Node.Bounds;
        return new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nint GetEmbeddedFragmentRoots() => 0;

    /// <inheritdoc/>
    public void SetFocus()
    {
        if (!_owner.Tree.Focus(_id))
        {
            throw UiaErrors.Unsupported();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRawElementProviderFragmentRoot? GetFragmentRoot() => _owner.RootOf(Node);

    /// <inheritdoc/>
    public IRawElementProviderFragment? ElementProviderFromPoint(double x, double y)
    {
        var id = _owner.Tree.HitTest((int)x, (int)y);
        return id >= 0 && InThisWindow(id) ? _owner.Get(id) : null;
    }

    /// <inheritdoc/>
    public IRawElementProviderFragment? GetFocus()
    {
        var id = _owner.Tree.FocusedId;
        return id >= 0 && InThisWindow(id) ? _owner.Get(id) : null;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke() => Act(AccessibleActions.Click);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Toggle() => Act(AccessibleActions.Toggle);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetToggleState(out ToggleState state) => state = UiaProperties.ToggleOf(Node);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Expand() => Act(AccessibleActions.Expand);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Collapse() => Act(AccessibleActions.Collapse);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetExpandCollapseState(out ExpandCollapseState state) => state = UiaProperties.ExpandOf(Node);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Select() => Act(AccessibleActions.Select);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddToSelection() => Select();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveFromSelection() => throw UiaErrors.Unsupported();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetIsSelected() => (Node.States & AccessibleStates.Selected) != 0;

    /// <inheritdoc/>
    public IRawElementProviderSimple? GetSelectionContainer()
    {
        var parent = Node.ParentId;
        return parent < 0 || parent == _owner.Tree.RootId ? null : _owner.Get(parent);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(string value) => throw UiaErrors.Unsupported();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetValue() => Text.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetIsReadOnly() => (Node.States & AccessibleStates.Editable) == 0;

    /// <inheritdoc/>
    public nint GetSelection()
    {
        var text = Text;
        if (text.CaretOffset < 0)
        {
            return SafeArrays.FromUnknowns([]);
        }

        var selected = text.SelectionStart >= 0 && text.SelectionEnd >= text.SelectionStart;
        return OneRange(selected ? text.SelectionStart : text.CaretOffset, selected ? text.SelectionEnd : text.CaretOffset);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nint GetVisibleRanges() => OneRange(0, Text.Value.Length);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ITextRangeProvider RangeFromChild(IRawElementProviderSimple childElement) => new UiaTextRange(this, 0, Text.Value.Length);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ITextRangeProvider RangeFromPoint(double x, double y) => new UiaTextRange(this, 0, 0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ITextRangeProvider GetDocumentRange() => new UiaTextRange(this, 0, Text.Value.Length);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetSupportedTextSelection(out SupportedTextSelection selection) =>
        selection = Text.CaretOffset >= 0 ? SupportedTextSelection.Single : SupportedTextSelection.None;

    /// <summary>Carries out one of the element's actions; fails with an error UI Automation understands when the element has no such action.</summary>
    /// <param name="action">The action's name.</param>
    private void Act(string action)
    {
        var index = UiaPatterns.IndexOf(Node, action);
        if (index < 0 || !_owner.Tree.DoAction(_id, index))
        {
            throw UiaErrors.Unsupported();
        }
    }

    /// <summary>Checks whether an element is in the same window as this one.</summary>
    /// <param name="id">The other element's number.</param>
    /// <returns>Whether it is.</returns>
    private bool InThisWindow(int id) =>
        _owner.Tree.Get(id) is { } other && _owner.RootOf(other)?._id == (_owner.RootOf(Node)?._id ?? -1);

    /// <summary>Makes an array that holds one text range.</summary>
    /// <param name="start">The offset where the range starts.</param>
    /// <param name="end">The offset just after the range.</param>
    /// <returns>The <c>SAFEARRAY</c>.</returns>
    private nint OneRange(int start, int end)
    {
        var range = NativeMethods.ComPointerOf<ITextRangeProvider>(new UiaTextRange(this, start, end));
        try
        {
            return SafeArrays.FromUnknowns([range]);
        }
        finally
        {
            _ = Marshal.Release(range);
        }
    }
}
