// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using HyperPdf.App.Accessibility;
using HyperPdf.App.Views;
using HyperPdf.Core.Reading;

namespace HyperPdf.App.Controls;

/// <summary>
/// One block of the shown page for assistive technology: a heading and its level, a list item and its place in the list, a table cell,
/// a figure with its description, a caption or a footnote. The page canvas lists them in reading order, so a screen reader can move
/// by structure in the main view as it does in Focus Mode.
/// </summary>
[DebuggerDisplay("ReadingBlockAutomationPeer: {_block.Kind}: {_block.Text}")]
internal sealed class ReadingBlockAutomationPeer : AutomationPeer, ISetMember
{
    /// <summary>The page canvas' peer, which gives the block its place on the screen.</summary>
    private readonly PageCanvasAutomationPeer _owner;

    /// <summary>The page the block is on.</summary>
    private readonly int _page;

    /// <summary>The block's place among the page's blocks.</summary>
    private readonly int _index;

    /// <summary>The block.</summary>
    private readonly ReadingBlock _block;

    /// <summary>The block's number among the items of its list, or zero when it is not a list item.</summary>
    private readonly int _positionInList;

    /// <summary>The number of items in the block's list, or zero when it is not a list item.</summary>
    private readonly int _listSize;

    /// <summary>Initializes a new instance of the <see cref="ReadingBlockAutomationPeer"/> class.</summary>
    /// <param name="owner">The page canvas' peer.</param>
    /// <param name="page">The page the block is on.</param>
    /// <param name="index">The block's place among the page's blocks.</param>
    /// <param name="block">The block.</param>
    /// <param name="positionInList">The block's number among the items of its list, or zero.</param>
    /// <param name="listSize">The number of items in its list, or zero.</param>
    internal ReadingBlockAutomationPeer(PageCanvasAutomationPeer owner, int page, int index, ReadingBlock block, int positionInList, int listSize)
    {
        _owner = owner;
        _page = page;
        _index = index;
        _block = block;
        _positionInList = positionInList;
        _listSize = listSize;
    }

    /// <inheritdoc/>
    public int PositionInSet => _positionInList;

    /// <inheritdoc/>
    public int SetSize => _listSize;

    /// <summary>Gets the block this peer describes.</summary>
    internal ReadingBlock Block => _block;

    /// <inheritdoc/>
    protected override void BringIntoViewCore()
    {
        // The page canvas is scrolled by the reader, and a block has no control to scroll to.
    }

    /// <inheritdoc/>
    protected override string? GetAcceleratorKeyCore() => null;

    /// <inheritdoc/>
    protected override string? GetAccessKeyCore() => null;

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => FocusAccessibility.ControlType(_block.Kind);

    /// <inheritdoc/>
    protected override string? GetAutomationIdCore() => string.Create(CultureInfo.InvariantCulture, $"ReadingBlock{_page}.{_index}");

    /// <inheritdoc/>
    protected override Rect GetBoundingRectangleCore()
    {
        var owner = _owner.GetBoundingRectangle();
        var rect = _owner.GetCanvasRect(_page, _block);
        return new(owner.X + rect.X, owner.Y + rect.Y, rect.Width, rect.Height);
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => [];

    /// <inheritdoc/>
    protected override string GetClassNameCore() => nameof(ReadingBlockAutomationPeer);

    /// <inheritdoc/>
    protected override AutomationPeer? GetLabeledByCore() => null;

    /// <inheritdoc/>
    protected override string? GetNameCore() => _block.Text;

    /// <inheritdoc/>
    protected override string? GetHelpTextCore()
    {
        var role = _block.Kind switch
        {
            ReadingBlockKind.ListItem when _listSize > 0 => UiText.Format("List item {0} of {1}", _positionInList, _listSize),
            ReadingBlockKind.TableCell when _block.Row > 0 => UiText.Format("Table cell, row {0}, column {1}", _block.Row, _block.Column),
            ReadingBlockKind.TableCell => UiText.Get("Table cell"),
            ReadingBlockKind.Figure => UiText.Get("Figure"),
            ReadingBlockKind.Caption => UiText.Get("Caption"),
            ReadingBlockKind.Footnote => UiText.Get("Footnote"),
            _ => null,
        };

        // A change of language is said so a screen reader can switch voices or tell the reader.
        if (_block.Language is not { Length: > 0 } language)
        {
            return role;
        }

        var spoken = UiText.Format("language {0}", PageSemanticAutomationPeer.DescribeLanguage(language));
        return role is null ? spoken : $"{role}, {spoken}";
    }

    /// <inheritdoc/>
    protected override int GetHeadingLevelCore() => _block.Kind == ReadingBlockKind.Heading ? _block.Level : 0;

    /// <inheritdoc/>
    protected override AutomationPeer? GetParentCore() => _owner;

    /// <inheritdoc/>
    protected override bool HasKeyboardFocusCore() => false;

    /// <inheritdoc/>
    protected override bool IsContentElementCore() => true;

    /// <inheritdoc/>
    protected override bool IsControlElementCore() => true;

    /// <inheritdoc/>
    protected override bool IsEnabledCore() => true;

    /// <inheritdoc/>
    protected override bool IsKeyboardFocusableCore() => false;

    /// <inheritdoc/>
    protected override void SetFocusCore()
    {
        // A block cannot take keyboard focus; the page canvas holds it.
    }

    /// <inheritdoc/>
    protected override bool ShowContextMenuCore() => false;

    /// <inheritdoc/>
    protected override bool TrySetParent(AutomationPeer? parent) => ReferenceEquals(parent, _owner);
}
