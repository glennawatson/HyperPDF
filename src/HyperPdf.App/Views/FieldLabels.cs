// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;

namespace HyperPdf.App.Views;

/// <summary>
/// Ties form fields to the text labels beside them, so assistive technology knows which label belongs to which
/// field. A field with no name of its own is then named by its label.
/// </summary>
internal static class FieldLabels
{
    /// <summary>Links each field to its label.</summary>
    /// <param name="pairs">Each field and the label shown beside it.</param>
    internal static void Link(params ReadOnlySpan<(Control Field, Control Label)> pairs)
    {
        foreach (var (field, label) in pairs)
        {
            AutomationProperties.SetLabeledBy(field, label);
        }
    }
}
