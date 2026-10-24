// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>Reads the properties of an element that UI Automation asks for, from the element's node.</summary>
internal static class UiaProperties
{
    /// <summary>The name of the class UI Automation reports for every element.</summary>
    private const string ClassName = "HyperPdf";

    /// <summary>The name of the framework UI Automation reports for every element.</summary>
    private const string FrameworkName = "HyperPDF";

    /// <summary>The localized control type of a heading.</summary>
    private const string HeadingControlType = "heading";

    /// <summary>The localized control type of a landmark.</summary>
    private const string LandmarkControlType = "landmark";

    /// <summary>The orientation number of a horizontal element.</summary>
    private const int HorizontalOrientation = 1;

    /// <summary>The orientation number of a vertical element.</summary>
    private const int VerticalOrientation = 2;

    /// <summary>The readers, by property number.</summary>
    private static readonly Dictionary<int, PropertyReader> Readers = new()
    {
        [(int)UiaPropertyId.Name] = static node => ComVariant.FromString(node.Name),
        [(int)UiaPropertyId.ControlType] = static node => ComVariant.FromInt32((int)UiaRoles.ControlTypeOf(node.Role)),
        [(int)UiaPropertyId.LocalizedControlType] = LocalizedControlType,
        [(int)UiaPropertyId.HelpText] = Description,
        [(int)UiaPropertyId.FullDescription] = Description,
        [(int)UiaPropertyId.AutomationId] = static node => ComVariant.FromString(node.Id.ToString(CultureInfo.InvariantCulture)),
        [(int)UiaPropertyId.ClassName] = static _ => ComVariant.FromString(ClassName),
        [(int)UiaPropertyId.FrameworkId] = static _ => ComVariant.FromString(FrameworkName),
        [(int)UiaPropertyId.ProcessId] = static _ => ComVariant.FromInt32(Environment.ProcessId),
        [(int)UiaPropertyId.NativeWindowHandle] = static node => ComVariant.FromInt32((int)node.WindowNumber),
        [(int)UiaPropertyId.IsEnabled] = static node => State(node, AccessibleStates.Enabled),
        [(int)UiaPropertyId.IsKeyboardFocusable] = static node => State(node, AccessibleStates.Focusable),
        [(int)UiaPropertyId.HasKeyboardFocus] = static node => State(node, AccessibleStates.Focused),
        [(int)UiaPropertyId.IsRequiredForForm] = static node => State(node, AccessibleStates.Required),
        [(int)UiaPropertyId.WindowIsModal] = static node => State(node, AccessibleStates.Modal),
        [(int)UiaPropertyId.IsOffscreen] = static node => ComVariant.FromBoolean(!Has(node, AccessibleStates.Showing)),
        [(int)UiaPropertyId.IsControlElement] = static _ => ComVariant.FromBoolean(true),
        [(int)UiaPropertyId.IsContentElement] = static _ => ComVariant.FromBoolean(true),
        [(int)UiaPropertyId.IsPassword] = static _ => ComVariant.FromBoolean(false),
        [(int)UiaPropertyId.IsDialog] = static node => ComVariant.FromBoolean(node.Role == AccessibleRole.Dialog),
        [(int)UiaPropertyId.Orientation] = Orientation,
        [(int)UiaPropertyId.HeadingLevel] = HeadingLevel,
        [(int)UiaPropertyId.LandmarkType] = LandmarkType,
        [(int)UiaPropertyId.LocalizedLandmarkType] = LocalizedLandmarkType,
        [(int)UiaPropertyId.PositionInSet] = static node => node.PositionInSet > 0 ? ComVariant.FromInt32(node.PositionInSet) : default,
        [(int)UiaPropertyId.SizeOfSet] = static node => node.SetSize > 0 ? ComVariant.FromInt32(node.SetSize) : default,
        [(int)UiaPropertyId.ToggleState] = static node => ComVariant.FromInt32((int)ToggleOf(node)),
        [(int)UiaPropertyId.ExpandCollapseState] = static node => ComVariant.FromInt32((int)ExpandOf(node)),
        [(int)UiaPropertyId.SelectionItemIsSelected] = static node => State(node, AccessibleStates.Selected),
        [(int)UiaPropertyId.ValueValue] = static node => node.Text is { } text ? ComVariant.FromString(text.Value) : default,
        [(int)UiaPropertyId.ValueIsReadOnly] = static node => ComVariant.FromBoolean(!Has(node, AccessibleStates.Editable)),
    };

    /// <summary>A function that reads one property of an element.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The value, or an empty variant when the element has none.</returns>
    private delegate ComVariant PropertyReader(AccessibleNode node);

    /// <summary>Reads a property of an element.</summary>
    /// <param name="propertyId">The property, a <see cref="UiaPropertyId"/>.</param>
    /// <param name="node">The element.</param>
    /// <returns>The value, or an empty variant when the provider has none.</returns>
    internal static ComVariant Read(int propertyId, AccessibleNode node) => Readers.TryGetValue(propertyId, out var reader) ? reader(node) : default;

    /// <summary>Gets the toggle state an element's flags stand for.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The state.</returns>
    internal static ToggleState ToggleOf(AccessibleNode node)
    {
        if (Has(node, AccessibleStates.Indeterminate))
        {
            return ToggleState.Indeterminate;
        }

        return Has(node, AccessibleStates.Checked) ? ToggleState.On : ToggleState.Off;
    }

    /// <summary>Gets the open or closed state an element's flags stand for.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The state.</returns>
    internal static ExpandCollapseState ExpandOf(AccessibleNode node)
    {
        if (!Has(node, AccessibleStates.Expandable))
        {
            return ExpandCollapseState.LeafNode;
        }

        return Has(node, AccessibleStates.Expanded) ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
    }

    /// <summary>Checks whether an element has a state.</summary>
    /// <param name="node">The element.</param>
    /// <param name="flag">The state.</param>
    /// <returns>Whether it has it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Has(AccessibleNode node, AccessibleStates flag) => (node.States & flag) != 0;

    /// <summary>Reads whether a flag is set.</summary>
    /// <param name="node">The element.</param>
    /// <param name="flag">The flag.</param>
    /// <returns>A Boolean variant.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ComVariant State(AccessibleNode node, AccessibleStates flag) => ComVariant.FromBoolean(Has(node, flag));

    /// <summary>Reads the description, when there is one.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The description, or an empty variant.</returns>
    private static ComVariant Description(AccessibleNode node) => node.Description.Length > 0 ? ComVariant.FromString(node.Description) : default;

    /// <summary>Reads the localized control type of headings and landmarks, which have no control type of their own.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The name, or an empty variant when the standard one is right.</returns>
    private static ComVariant LocalizedControlType(AccessibleNode node) =>
        node.Role switch
        {
            AccessibleRole.Heading => ComVariant.FromString(HeadingControlType),
            AccessibleRole.Landmark => ComVariant.FromString(LandmarkControlType),
            _ => default,
        };

    /// <summary>Reads which way an element lies.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The orientation, or an empty variant when it has none.</returns>
    private static ComVariant Orientation(AccessibleNode node)
    {
        if (Has(node, AccessibleStates.Horizontal))
        {
            return ComVariant.FromInt32(HorizontalOrientation);
        }

        return Has(node, AccessibleStates.Vertical) ? ComVariant.FromInt32(VerticalOrientation) : default;
    }

    /// <summary>Reads the level of a heading.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The level, or an empty variant for anything else.</returns>
    private static ComVariant HeadingLevel(AccessibleNode node) => node.HeadingLevel > 0 ? ComVariant.FromInt32(UiaRoles.HeadingLevelNumber(node.HeadingLevel)) : default;

    /// <summary>Reads the kind of a landmark.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The kind, or an empty variant for anything else.</returns>
    private static ComVariant LandmarkType(AccessibleNode node) => node.LandmarkType.Length > 0 ? ComVariant.FromInt32(UiaRoles.LandmarkNumber(node.LandmarkType)) : default;

    /// <summary>Reads the spoken name of a landmark's kind.</summary>
    /// <param name="node">The element.</param>
    /// <returns>The name, or an empty variant for anything else.</returns>
    private static ComVariant LocalizedLandmarkType(AccessibleNode node) => node.LandmarkType.Length > 0 ? ComVariant.FromString(node.LandmarkType) : default;
}
