// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using HyperPdf.App.Controls;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>
/// Gives a control one plain-English explanation, shown as its tooltip and given to screen readers as its help text.
/// Setting both from one value keeps what people see and what they hear the same.
/// </summary>
public static class ControlHelp
{
    /// <summary>The explanation shown as the tooltip and read out as the accessible help text.</summary>
    public static readonly AttachedProperty<string?> TextProperty = AvaloniaProperty.RegisterAttached<Control, string?>("Text", typeof(ControlHelp));

    /// <summary>Initializes static members of the <see cref="ControlHelp"/> class, copying each new explanation to the tooltip and the help text.</summary>
    /// <remarks>The property lasts as long as the app, and so does this subscription, so it is never disposed.</remarks>
    static ControlHelp() =>
        _ = TextProperty.ClassChanges
            .Pick(static change => change.Sender is Control control ? new HelpChange(control, change.NewValue.GetValueOrDefault()) : default(HelpChange?))
            .SubscribeSafe(static change => Apply(change.Control, change.Text), ErrorTrace.Write);

    /// <summary>Gets a control's explanation.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The explanation, or <see langword="null"/>.</returns>
    public static string? GetText(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(TextProperty);
    }

    /// <summary>Sets a control's explanation, which becomes its tooltip and its accessible help text.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The explanation.</param>
    public static void SetText(Control control, string? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        _ = control.SetValue(TextProperty, value);
    }

    /// <summary>Shows an explanation as a control's tooltip and gives it to the platform's accessibility API.</summary>
    /// <param name="control">The control.</param>
    /// <param name="text">The explanation.</param>
    private static void Apply(Control control, string? text)
    {
        ToolTip.SetTip(control, text);
        AutomationProperties.SetHelpText(control, text);
    }

    /// <summary>A control and the explanation it was given.</summary>
    /// <param name="Control">The control.</param>
    /// <param name="Text">The explanation.</param>
    private readonly record struct HelpChange(Control Control, string? Text);
}
