// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
using HyperPdf.ViewModels.FormFilling;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Typing into a form field where it lies on the page.</summary>
internal static class FormFieldTypings
{
    /// <summary>Makes what the window types into for a form field.</summary>
    /// <param name="forms">The form of the tab shown.</param>
    /// <param name="formField">The field being filled in.</param>
    /// <returns>The target. Its lifetime ends the follow of the text's spelling.</returns>
    internal static TypingTarget For(FormsViewModel forms, FormField formField)
    {
        // The spelling of the word at the end is checked again each time the text changes, and the latest check is what a press on a correction uses.
        var spelling = forms.WhenChanged(static f => f.EditText).Select(text => SpellingChecks.Of(forms, text)).ToReadOnlyState(SpellingCheck.None, static check => check);
        return new(
            formField.PageIndex,
            formField.Bounds,
            false,
            () => ReferenceEquals(forms.Editing, formField),
            () => forms.EditText,
            text => forms.EditText = text,
            forms.WhenChanged(static f => f.EditText).AsSignal(),
            forms.Commit,
            forms.Cancel,
            backwards => _ = forms.CommitAndMove(backwards),
            () => spelling.Value.Labels,
            index => SpellingChecks.Correct(forms, spelling.Value, index, text => forms.EditText = text),
            () => spelling.Value.Misspelled,
            spelling);
    }
}
