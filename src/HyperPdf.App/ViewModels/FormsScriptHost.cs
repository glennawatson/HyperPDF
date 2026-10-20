// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Forms.Scripting;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The form as a sandboxed script sees it: the current values of the fields, with the changes a script makes written to the
/// document through the form filler. It remembers the pages it changed so the tab can redraw them.
/// </summary>
/// <param name="filler">The form filler.</param>
/// <param name="visibility">Shows and hides fields, or <see langword="null"/> when the document cannot.</param>
/// <param name="fields">The first widget of each field, by name.</param>
/// <param name="values">The current values by name; kept up to date as scripts change them.</param>
/// <param name="lockedNames">The names of the fields scripts made read-only; shared with the tab.</param>
/// <param name="tell">Shows the reader a message, as a calm notice.</param>
[DebuggerDisplay("FormsScriptHost: {fields.Count} fields")]
internal sealed class FormsScriptHost(
    IFormFiller filler,
    IFormVisibility? visibility,
    Dictionary<string, FormField> fields,
    Dictionary<string, string> values,
    HashSet<string> lockedNames,
    Action<string> tell) : IFormScriptHost
{
    /// <summary>Gets the pages whose fields a script changed.</summary>
    internal HashSet<int> ChangedPages { get; } = [];

    /// <summary>Gets a value indicating whether a script showed or hid a field, which can change every page.</summary>
    internal bool DisplayChanged { get; private set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetFieldValue(string fieldName) => values.GetValueOrDefault(fieldName);

    /// <inheritdoc/>
    public void SetFieldValue(string fieldName, string value)
    {
        if (!fields.TryGetValue(fieldName, out var field) || !filler.SetText(field.PageIndex, field.Index, value))
        {
            return;
        }

        values[fieldName] = value;
        _ = ChangedPages.Add(field.PageIndex);
    }

    /// <inheritdoc/>
    public void SetFieldDisplay(string fieldName, FormFieldDisplay display)
    {
        // A field that shows on screen but not in print stays shown, and one that shows only in print is hidden here:
        // the viewer draws pages for the screen, and does not change what the form prints.
        var hide = display is FormFieldDisplay.Hidden or FormFieldDisplay.NoView;
        DisplayChanged |= visibility?.SetHidden(fieldName, hide) == true;
    }

    /// <inheritdoc/>
    public void SetFieldReadOnly(string fieldName, bool readOnly) => _ = readOnly ? lockedNames.Add(fieldName) : lockedNames.Remove(fieldName);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Tell(string message) => tell(message);
}
