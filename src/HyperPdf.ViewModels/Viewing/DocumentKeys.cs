// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Forms;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>What each key does in the document view, which every render head follows alike.</summary>
public static class DocumentKeys
{
    /// <summary>Handles the keys that change the speed or stop while auto-scrolling.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    public static bool HandleAutoScroll(DocumentTabViewModel tab, ViewerKey key, ViewerModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (!tab.PageTools.IsAutoScrolling || (modifiers & ~ViewerModifiers.Shift) != 0)
        {
            return false;
        }

        switch (key)
        {
            case ViewerKey.Up or ViewerKey.Plus:
            {
                _ = tab.PageTools.ChangeAutoScrollSpeed(1);
                return true;
            }

            case ViewerKey.Down or ViewerKey.Minus:
            {
                _ = tab.PageTools.ChangeAutoScrollSpeed(-1);
                return true;
            }

            case ViewerKey.Escape:
            {
                tab.PageTools.IsAutoScrolling = false;
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Handles Enter (finish) and Escape (clear) while measuring.</summary>
    /// <param name="measure">The measurement state.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key was used and the view should redraw.</returns>
    public static bool HandleMeasure(MeasureViewModel measure, ViewerKey key)
    {
        ArgumentNullException.ThrowIfNull(measure);
        if (!measure.IsOn)
        {
            return false;
        }

        switch (key)
        {
            case ViewerKey.Enter:
            {
                measure.Finish();
                return true;
            }

            case ViewerKey.Escape:
            {
                measure.Clear();
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Handles the placing keys while a signature or initials mark is being placed.</summary>
    /// <typeparam name="TState">What the move needs, such as the page canvas.</typeparam>
    /// <param name="placing">The tab's Fill &amp; Sign state.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <param name="state">What the move needs.</param>
    /// <param name="move">Moves the mark by a distance on screen, in points.</param>
    /// <returns><see langword="true"/> when the key was used and the view should redraw.</returns>
    public static bool HandlePlacement<TState>(FillAndSignViewModel placing, ViewerKey key, ViewerModifiers modifiers, TState state, Func<TState, float, float, bool> move)
    {
        ArgumentNullException.ThrowIfNull(placing);
        ArgumentNullException.ThrowIfNull(move);
        if (placing.Placement is null || (modifiers & ~ViewerModifiers.Shift) != 0)
        {
            return false;
        }

        var step = ScreenStep.FromArrow(key, modifiers);
        return key switch
        {
            ViewerKey.Left or ViewerKey.Right or ViewerKey.Up or ViewerKey.Down => move(state, step.X, step.Y),
            ViewerKey.Plus => placing.Resize(SignatureMarkLayout.ResizeStep),
            ViewerKey.Minus => placing.Resize(1 / SignatureMarkLayout.ResizeStep),
            ViewerKey.PageDown => placing.MoveToPage(1),
            ViewerKey.PageUp => placing.MoveToPage(-1),

            // Enter is used even when placing fails, as the failure is shown as a notice.
            ViewerKey.Enter => Commit(placing),
            ViewerKey.Escape => placing.Cancel(),
            _ => false,
        };
    }

    /// <summary>Handles Tab and Shift+Tab between form fields, Space on the focused box, Enter on a focused list, and Escape.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns>What the key did.</returns>
    public static FormKeyResult HandleForm(DocumentTabViewModel tab, ViewerKey key, ViewerModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab.Forms is not { HasForm: true } forms)
        {
            return default;
        }

        var focused = forms.Focused;
        return key switch
        {
            ViewerKey.Tab when focused is not null || tab.FillAndSign.IsActive => MoveField(tab, forms, (modifiers & ViewerModifiers.Shift) != 0),
            ViewerKey.Space when focused is not null => Redraw(forms.PressFocused()),
            ViewerKey.Enter when focused is { Kind: FormFieldKind.ComboBox or FormFieldKind.ListBox } => new(FormKeyOutcome.OpenChoices, focused),
            ViewerKey.Escape when focused is not null => Redraw(forms.ClearFocus()),
            _ => default,
        };
    }

    /// <summary>Goes to the first or last page on Home or End, unless the text cursor owns those keys.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key moved to a page and the view should redraw.</returns>
    public static bool HandleEdgePage(DocumentTabViewModel tab, ViewerKey key)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab.IsCaretMode || tab.PageCount <= 0 || key is not (ViewerKey.Home or ViewerKey.End))
        {
            return false;
        }

        tab.GoToPage(key == ViewerKey.Home ? 0 : tab.PageCount - 1);
        return true;
    }

    /// <summary>Edits the picked text box on Enter or F2, as a spreadsheet edits its cell.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns><see langword="true"/> when editing started.</returns>
    public static bool EditPickedText(AnnotationsViewModel annotations, ViewerKey key, ViewerModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        return key is ViewerKey.Enter or ViewerKey.F2
            && modifiers == ViewerModifiers.None
            && annotations.Selected is { Kind: AnnotationKind.TextBox } picked
            && annotations.EditText(picked);
    }

    /// <summary>Removes the picked annotation on Delete.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when an annotation was removed.</returns>
    public static bool DeleteSelected(AnnotationsViewModel annotations, ViewerKey key)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        if (key != ViewerKey.Delete || annotations.Selected is not { } selected)
        {
            return false;
        }

        annotations.Delete(selected);
        return true;
    }

    /// <summary>Places the signature mark; Enter is used even when placing fails, as the failure is shown as a notice.</summary>
    /// <param name="placing">The tab's Fill &amp; Sign state.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool Commit(FillAndSignViewModel placing)
    {
        _ = placing.Commit();
        return true;
    }

    /// <summary>Moves the field focus, scrolling the field into view.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="forms">The form state.</param>
    /// <param name="backwards">Whether to move to the previous field.</param>
    /// <returns>A redraw when a field took the focus, and otherwise nothing.</returns>
    private static FormKeyResult MoveField(DocumentTabViewModel tab, FormsViewModel forms, bool backwards)
    {
        if (forms.CommitAndMove(backwards) is not { } field)
        {
            return default;
        }

        tab.NavigateTo(field.PageIndex, field.Bounds, 0);
        return new(FormKeyOutcome.Redraw, null);
    }

    /// <summary>Turns whether a change was made into a redraw or nothing.</summary>
    /// <param name="changed">Whether the key changed the form.</param>
    /// <returns>The result.</returns>
    private static FormKeyResult Redraw(bool changed) => changed ? new(FormKeyOutcome.Redraw, null) : default;
}
