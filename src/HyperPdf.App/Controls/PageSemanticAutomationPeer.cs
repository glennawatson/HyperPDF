// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation.Peers;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reading;

namespace HyperPdf.App.Controls;

/// <summary>
/// One item of the shown page for assistive technology that the reading blocks leave out: an empty table cell, an artifact, a form
/// field with its state, or an annotation with its description. An artifact is kept out of the content view so a screen reader
/// skips it, and an item in optional content that is switched off is reported as off screen.
/// </summary>
[DebuggerDisplay("PageSemanticAutomationPeer: {Item.Kind}: {Item.Name}")]
internal class PageSemanticAutomationPeer : AutomationPeer
{
    /// <summary>The page canvas' peer, which gives the item its place on the screen.</summary>
    private readonly PageCanvasAutomationPeer _owner;

    /// <summary>The page the item is on.</summary>
    private readonly int _page;

    /// <summary>The item's place among the page's items.</summary>
    private readonly int _index;

    /// <summary>Initializes a new instance of the <see cref="PageSemanticAutomationPeer"/> class.</summary>
    /// <param name="owner">The page canvas' peer.</param>
    /// <param name="page">The page the item is on.</param>
    /// <param name="index">The item's place among the page's items.</param>
    /// <param name="item">The item.</param>
    internal PageSemanticAutomationPeer(PageCanvasAutomationPeer owner, int page, int index, PageSemanticItem item)
    {
        _owner = owner;
        _page = page;
        _index = index;
        Item = item;
    }

    /// <summary>Gets the item this peer describes.</summary>
    internal PageSemanticItem Item { get; }

    /// <summary>Puts an item's role, description, value, state, formatting and language into words.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The words, or <see langword="null"/> when the item has none.</returns>
    internal static string? Describe(PageSemanticItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var words = new StringBuilder();
        Append(words, item.Role);
        Append(words, item.Kind == PageSemanticKind.EmptyCell && item.Row > 0 ? UiText.Format("row {0}, column {1}", item.Row, item.Column) : null);
        Append(words, item.Kind == PageSemanticKind.EmptyCell ? UiText.Get("empty") : null);
        Append(words, item.Description);
        Append(words, item.Value is { Length: > 0 } value ? UiText.Format("value {0}", value) : null);
        Append(words, item.State);
        Append(words, item.RichText is { Length: > 0 } styles ? UiText.Format("formatted text, {0}", styles) : null);
        Append(words, item.Language is { Length: > 0 } language ? UiText.Format("language {0}", DescribeLanguage(language)) : null);
        Append(words, item.IsHidden ? UiText.Get("hidden") : null);
        return words.Length == 0 ? null : words.ToString();
    }

    /// <summary>Gives a language tag as its name in English when the system knows it, such as "French (France)" for "fr-FR".</summary>
    /// <param name="tag">The language tag.</param>
    /// <returns>The name, or the tag itself.</returns>
    internal static string DescribeLanguage(string tag)
    {
        try
        {
            return CultureInfo.GetCultureInfo(tag).DisplayName is { Length: > 0 } name ? name : tag;
        }
        catch (CultureNotFoundException)
        {
            return tag;
        }
    }

    /// <summary>Gets the control type a form field or annotation role is reported as.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The control type.</returns>
    protected static AutomationControlType ControlTypeOf(PageSemanticItem item) => item.Kind switch
    {
        PageSemanticKind.EmptyCell => AutomationControlType.DataItem,
        PageSemanticKind.FormField => item.Role switch
        {
            "check box" => AutomationControlType.CheckBox,
            "radio button" => AutomationControlType.RadioButton,
            "combo box" => AutomationControlType.ComboBox,
            "list box" => AutomationControlType.List,
            "text field" => AutomationControlType.Edit,
            "button" => AutomationControlType.Button,
            _ => AutomationControlType.Custom,
        },
        PageSemanticKind.Annotation when item.Role == "link" => AutomationControlType.Hyperlink,
        _ => AutomationControlType.Text,
    };

    /// <inheritdoc/>
    protected override void BringIntoViewCore()
    {
        // The page canvas is scrolled by the reader, and an item has no control to scroll to.
    }

    /// <inheritdoc/>
    protected override string? GetAcceleratorKeyCore() => null;

    /// <inheritdoc/>
    protected override string? GetAccessKeyCore() => null;

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => ControlTypeOf(Item);

    /// <inheritdoc/>
    protected override string? GetAutomationIdCore() => string.Create(CultureInfo.InvariantCulture, $"PageItem{_page}.{_index}");

    /// <inheritdoc/>
    protected override Rect GetBoundingRectangleCore()
    {
        var owner = _owner.GetBoundingRectangle();
        var rect = Item.Bounds == default(PageRect) ? default : _owner.GetCanvasRect(_page, Item.Bounds);
        return new(owner.X + rect.X, owner.Y + rect.Y, rect.Width, rect.Height);
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => [];

    /// <inheritdoc/>
    protected override string GetClassNameCore() => nameof(PageSemanticAutomationPeer);

    /// <inheritdoc/>
    protected override AutomationPeer? GetLabeledByCore() => null;

    /// <inheritdoc/>
    protected override string? GetNameCore() => Item.Name;

    /// <inheritdoc/>
    protected override string? GetHelpTextCore() => Describe(Item);

    /// <inheritdoc/>
    protected override AutomationPeer? GetParentCore() => _owner;

    /// <inheritdoc/>
    protected override bool HasKeyboardFocusCore() => false;

    /// <inheritdoc/>
    protected override bool IsContentElementCore() => Item.Kind != PageSemanticKind.Artifact && !Item.IsHidden;

    /// <inheritdoc/>
    protected override bool IsControlElementCore() => IsContentElementCore();

    /// <inheritdoc/>
    protected override bool IsEnabledCore() => true;

    /// <inheritdoc/>
    protected override bool IsKeyboardFocusableCore() => false;

    /// <inheritdoc/>
    protected override bool IsOffscreenCore() => Item.IsHidden;

    /// <inheritdoc/>
    protected override void SetFocusCore()
    {
        // An item cannot take keyboard focus; the page canvas holds it.
    }

    /// <inheritdoc/>
    protected override bool ShowContextMenuCore() => false;

    /// <inheritdoc/>
    protected override bool TrySetParent(AutomationPeer? parent) => ReferenceEquals(parent, _owner);

    /// <summary>Adds a phrase to a comma separated list when it has words.</summary>
    /// <param name="words">The list.</param>
    /// <param name="phrase">The phrase, or <see langword="null"/>.</param>
    private static void Append(StringBuilder words, string? phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return;
        }

        _ = words.Append(words.Length == 0 ? string.Empty : ", ").Append(phrase);
    }
}
