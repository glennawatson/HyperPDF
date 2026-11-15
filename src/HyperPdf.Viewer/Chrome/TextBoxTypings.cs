// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.FormFilling;
using HyperPdf.ViewModels.Markup;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Typing into a text box where it lies on the page. Enter starts a new line, and Control with Enter writes the text.</summary>
internal static class TextBoxTypings
{
    /// <summary>The width of a box that wraps nowhere, in points.</summary>
    private const float OpenWidth = 220F;

    /// <summary>The height of one line of the box, in points.</summary>
    private const float LineHeight = 20F;

    /// <summary>Makes what the window types into for a text box.</summary>
    /// <param name="forms">The form of the tab shown, which knows the spell checker and the words kept.</param>
    /// <param name="marks">The marking tools of the tab shown.</param>
    /// <param name="edit">The text box being typed.</param>
    /// <returns>The target. Its lifetime ends the follow of the text's spelling.</returns>
    internal static TypingTarget For(FormsViewModel forms, AnnotationsViewModel marks, TextEditSession edit)
    {
        // The spelling is checked again each time the text changes, and the latest check is what a press on a correction uses.
        var spelling = marks.WhenChanged(static m => m.EditingText)
            .Select(text => SpellingChecks.Of(forms, text ?? string.Empty))
            .ToReadOnlyState(SpellingCheck.None, static check => check);
        return new(
            edit.Page,
            new(edit.Location.X, edit.Location.Y, edit.WrapWidth > 0 ? edit.WrapWidth : OpenWidth, LineHeight),
            true,
            () => ReferenceEquals(marks.TextEdit, edit),
            () => marks.EditingText ?? string.Empty,
            text => marks.EditingText = text,
            marks.WhenChanged(static m => m.EditingText).AsSignal(),
            () => _ = marks.CommitText(),
            marks.CancelText,
            _ => _ = marks.CommitText(),
            () => spelling.Value.Labels,
            index => SpellingChecks.Correct(forms, spelling.Value, index, text => marks.EditingText = text),
            () => spelling.Value.Misspelled,
            spelling);
    }
}
