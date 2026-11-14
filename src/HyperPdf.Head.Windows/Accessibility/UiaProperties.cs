// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>Answers UI Automation property queries for a node.</summary>
internal static class UiaProperties
{
    /// <summary>The class name reported for every element.</summary>
    private const string ElementClassName = "HyperPdfElement";

    /// <summary>The framework name reported for every element.</summary>
    private const string FrameworkName = "HyperPDF";

    /// <summary>Reads one property of a node.</summary>
    /// <param name="propertyId">The UI Automation property id.</param>
    /// <param name="node">The node.</param>
    /// <returns>The value, or an empty variant when the property is not supplied.</returns>
    internal static UiaVariant Read(int propertyId, in AccessibleNode node) => propertyId switch
    {
        UiaIds.ControlTypeProperty => UiaVariant.FromInt32(UiaIds.ControlType(node.Role)),
        UiaIds.NameProperty => UiaVariant.FromString(node.Name),
        UiaIds.IsEnabledProperty => UiaVariant.FromBoolean((node.States & AccessibleStates.Disabled) == 0),
        UiaIds.HasKeyboardFocusProperty => UiaVariant.FromBoolean((node.States & AccessibleStates.Focused) != 0),
        UiaIds.IsKeyboardFocusableProperty => UiaVariant.FromBoolean(IsFocusable(node.Role)),
        UiaIds.AutomationIdProperty => UiaVariant.FromString(string.Create(CultureInfo.InvariantCulture, $"hyperpdf-{node.Id}")),
        UiaIds.ClassNameProperty => UiaVariant.FromString(ElementClassName),
        UiaIds.FrameworkIdProperty => UiaVariant.FromString(FrameworkName),
        UiaIds.IsControlElementProperty or UiaIds.IsContentElementProperty => UiaVariant.FromBoolean(true),
        _ => default,
    };

    /// <summary>Tells whether elements of a role can take keyboard focus.</summary>
    /// <param name="role">The role.</param>
    /// <returns><see langword="true"/> when the role is an interactive one.</returns>
    private static bool IsFocusable(AccessibleRole role) => role is not (AccessibleRole.Window or AccessibleRole.Group or AccessibleRole.Text);
}
