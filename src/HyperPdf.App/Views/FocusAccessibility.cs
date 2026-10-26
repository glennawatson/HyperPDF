// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using HyperPdf.Core.Reading;

namespace HyperPdf.App.Views;

/// <summary>
/// Tells assistive technology what each Focus Mode block is: a heading and its level, a list item and its place in
/// the list, a table cell, a figure, a caption or a footnote, so a screen reader can move by structure.
/// </summary>
internal static class FocusAccessibility
{
    /// <summary>Describes a block's text element for assistive technology.</summary>
    /// <param name="text">The block's text element.</param>
    /// <param name="block">The block.</param>
    internal static void Describe(TextBlock text, FocusBlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(block);
        AutomationProperties.SetControlTypeOverride(text, ControlType(block.Kind));
        AutomationProperties.SetHeadingLevel(text, block.Level);
        AutomationProperties.SetPositionInSet(text, block.PositionInList);
        AutomationProperties.SetSizeOfSet(text, block.ListSize);
        AutomationProperties.SetHelpText(text, Description(block, OperatingSystem.IsMacOS()));
    }

    /// <summary>Gets the control type a block is reported as.</summary>
    /// <param name="kind">The block's kind.</param>
    /// <returns>The control type.</returns>
    internal static AutomationControlType ControlType(ReadingBlockKind kind) => kind switch
    {
        ReadingBlockKind.ListItem => AutomationControlType.ListItem,
        ReadingBlockKind.TableCell => AutomationControlType.DataItem,
        ReadingBlockKind.Figure => AutomationControlType.Image,
        _ => AutomationControlType.Text,
    };

    /// <summary>
    /// Gets the spoken description of a block's role. The app's own Linux and Windows bridges send headings and list positions
    /// from the heading level and set properties; Avalonia's macOS bridge passes on only the description, so there it says them
    /// too.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="describeStructure">Whether to put the heading level and list position into words.</param>
    /// <returns>The description, or <see langword="null"/> for plain paragraphs.</returns>
    internal static string? Description(FocusBlockViewModel block, bool describeStructure) => block.Kind switch
    {
        ReadingBlockKind.Heading when describeStructure => UiText.Format("Heading level {0}", block.Level),
        ReadingBlockKind.ListItem when describeStructure && block.ListSize > 0 => UiText.Format("List item {0} of {1}", block.PositionInList, block.ListSize),
        ReadingBlockKind.TableCell when describeStructure && block.Row > 0 => UiText.Format("Table cell, row {0}, column {1}", block.Row, block.Column),
        ReadingBlockKind.TableCell when describeStructure => UiText.Get("Table cell"),
        ReadingBlockKind.Figure => UiText.Get("Figure"),
        ReadingBlockKind.Caption => UiText.Get("Caption"),
        ReadingBlockKind.Footnote => UiText.Get("Footnote"),
        _ => null,
    };
}
