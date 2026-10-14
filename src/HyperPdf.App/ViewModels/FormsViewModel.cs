// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Forms.Scripting;
using HyperPdf.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// Form filling for one tab. Text fields open an editor placed over the field; check boxes and radio buttons toggle on
/// click; choices are picked from a menu. Every change goes to the document straight away.
/// </summary>
[DebuggerDisplay("FormsViewModel: Editing {Editing}")]
public sealed partial class FormsViewModel : ReactiveObject, IDisposable
{
    /// <summary>The most passes of recalculation, so fields that depend on each other in a loop still settle.</summary>
    private const int MaxCalculationPasses = 8;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Runs the open actions of each page that becomes the current page, until the tab closes.</summary>
    private readonly IDisposable _pageOpenings;

    /// <summary>Fields read for hit testing, reused.</summary>
    private readonly List<FormField> _scratch = [];

    /// <summary>The pages whose open actions have not run yet, in the order the pages opened; used on the UI thread only.</summary>
    private readonly Queue<int> _pagesOpened = [];

    /// <summary>The fields' scripts by name, read on first use.</summary>
    private Dictionary<string, FieldScripts>? _scripts;

    /// <summary>The fields with a calculation, in page order, read on first use.</summary>
    private List<FieldScripts>? _calculated;

    /// <summary>Initializes a new instance of the <see cref="FormsViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public FormsViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;

        // Each page that becomes the current page runs its open actions. A page that opens while the actions of an
        // earlier page still run waits its turn in the queue instead of being skipped: it is queued first, and the
        // running command drains the queue.
        _pageOpenings = owner.WhenChanged(static tab => tab.CurrentPageIndex)
            .Skip(1)
            .WhereWith(this, static (forms, _) => forms.Actions is not null)
            .DoWith(this, static (forms, page) => forms._pagesOpened.Enqueue(page))
            .InvokeCommand(this, static forms => forms.PageOpenedCommand);
    }

    /// <summary>Gets the latest change filling in the form made to a page; the tab redraws the page it names.</summary>
    [Reactive]
    public partial PageChange LastPageChange { get; private set; }

    /// <summary>Gets a value indicating whether the document has a fillable form.</summary>
    public bool HasForm => Filler?.HasForm == true;

    /// <summary>
    /// Gets the question asked before a form's Import Data action loads a file: the file name the form asked for in,
    /// <see langword="true"/> out to go on.
    /// </summary>
    public Interaction<string, bool> ConfirmImportInteraction { get; } = new();

    /// <summary>
    /// Gets the file chooser for a form's Import Data action: the file name the form asked for in, the path the reader
    /// chose out, or <see langword="null"/> to cancel. The form never picks the file.
    /// </summary>
    public Interaction<string, string?> ChooseImportFileInteraction { get; } = new();

    /// <summary>Gets the text field being edited, or <see langword="null"/>.</summary>
    [Reactive]
    public partial FormField? Editing { get; private set; }

    /// <summary>Gets the field with the keyboard focus, from Tab and Shift+Tab, or <see langword="null"/>.</summary>
    [Reactive]
    public partial FormField? Focused { get; private set; }

    /// <summary>Gets or sets the text in the open editor.</summary>
    [Reactive]
    public partial string EditText { get; set; } = string.Empty;

    /// <summary>Gets the document's form filler, or <see langword="null"/>.</summary>
    private IFormFiller? Filler => _owner.Notices.InteractionBlocked ? null : DocumentFeature<IFormFiller>.Get(_owner.TryGetDocument());

    /// <summary>
    /// Gets the document's form actions, or <see langword="null"/>. A document whose requirements stop its processing
    /// (ISO 32000-2 §12.11.6) has no actions, no buttons and no form filling.
    /// </summary>
    private IFormActions? Actions => _owner.Notices.InteractionBlocked ? null : DocumentFeature<IFormActions>.Get(_owner.TryGetDocument());

    /// <summary>Finds the fillable field under a point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The field, or <see langword="null"/>.</returns>
    public FormField? HitTest(int page, PagePoint point)
    {
        if (Filler is not { HasForm: true } filler)
        {
            return null;
        }

        _scratch.Clear();
        filler.GetFields(page, _scratch);
        foreach (var field in _scratch)
        {
            if (!field.IsReadOnly && field.Kind != FormFieldKind.Unknown && field.Bounds.Contains(point))
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Acts on a click on a field: edit text, toggle a box or radio button. Choices are handled by the view's menu.</summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when handled here.</returns>
    public bool Activate(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        Focused = field;
        switch (field.Kind)
        {
            case FormFieldKind.Text:
                {
                    Commit();
                    EditText = field.Value;
                    Editing = field;
                    return true;
                }

            case FormFieldKind.CheckBox:
                {
                    Refresh(field, Filler?.SetChecked(field.PageIndex, field.Index, !field.IsChecked) == true);
                    Recalculate();
                    return true;
                }

            case FormFieldKind.RadioButton:
                {
                    Refresh(field, Filler?.SetChecked(field.PageIndex, field.Index, true) == true);
                    Recalculate();
                    return true;
                }

            case FormFieldKind.PushButton:
                {
                    return StartButton(field);
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>
    /// Runs the action of a push button, and the actions after it: reset, hide, show or hide layers and named page moves.
    /// Scripts are never run, and form data is never sent. Fields that change are drawn again and recalculated.
    /// </summary>
    /// <param name="field">The button.</param>
    /// <returns>What happened; <see cref="FormActionResult.None"/> when the document cannot run actions or the button has none.</returns>
    public async Task<FormActionResult> RunButtonAsync(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (Actions is not
            {
            } actions)
        {
            return FormActionResult.None;
        }

        FormActionResult result;
        try
        {
            result = await actions.RunWidgetActionAsync(field.PageIndex, field.Index, new TabFormActionHost(_owner, true), _owner.LifetimeToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
        {
            // The tab was closed, or its document was closed under the action; there is nothing left to redraw.
            return FormActionResult.None;
        }

        if ((result & (FormActionResult.Changed | FormActionResult.LayersChanged)) != 0)
        {
            RefreshAllPages();
            Recalculate();
        }

        return result;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _pageOpenings.Dispose();

    /// <summary>Runs the actions a page does when it opens.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>What happened.</returns>
    public async Task<FormActionResult> RunPageOpenedAsync(int pageIndex)
    {
        if (Actions is not
            {
            } actions)
        {
            return FormActionResult.None;
        }

        FormActionResult result;
        try
        {
            result = await actions.RunPageOpenedAsync(pageIndex, new TabFormActionHost(_owner, false), _owner.LifetimeToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
        {
            // The tab was closed, or its document was closed under the action; there is nothing left to redraw.
            return FormActionResult.None;
        }

        if ((result & (FormActionResult.Changed | FormActionResult.LayersChanged)) != 0)
        {
            RefreshAllPages();
        }

        return result;
    }

    /// <summary>Picks a choice of a combo or list box.</summary>
    /// <param name="field">The field.</param>
    /// <param name="option">The option index.</param>
    public void Choose(FormField field, int option)
    {
        ArgumentNullException.ThrowIfNull(field);
        Refresh(field, Filler?.SelectOption(field.PageIndex, field.Index, option) == true);
        Recalculate();
    }

    /// <summary>Writes the open editor's text into its field and closes the editor.</summary>
    public void Commit()
    {
        if (Editing is not { } field)
        {
            return;
        }

        var scripts = ScriptsFor(field.Name);
        if (scripts is not null && Problem(scripts, EditText) is { } problem)
        {
            // Keep the editor open so the value can be corrected; the message stays until dismissed.
            _owner.Notice = problem;
            return;
        }

        Editing = null;
        var text = scripts is null ? EditText : FormScriptEngine.Format(scripts.Format, EditText);
        if (string.Equals(field.Value, text, StringComparison.Ordinal))
        {
            return;
        }

        Refresh(field, Filler?.SetText(field.PageIndex, field.Index, text) == true);
        Recalculate();
    }

    /// <summary>Closes the editor without changing the field.</summary>
    public void Cancel() => Editing = null;

    /// <summary>Clears the keyboard focus from a field, as Escape does on the page.</summary>
    /// <returns><see langword="true"/> when a field had the focus.</returns>
    public bool ClearFocus()
    {
        var had = Focused is not null;
        Focused = null;
        return had;
    }

    /// <summary>Commits the editor and opens the next text field, in page order, as Tab does.</summary>
    /// <returns>The next field, or <see langword="null"/> when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FormField? CommitAndMoveNext() => CommitAndMove(false);

    /// <summary>
    /// Commits the editor and moves to the next or previous fillable field in page order, as Tab and Shift+Tab do: a
    /// text field opens its editor, other fields take the keyboard focus. Past the last field nothing is focused, so
    /// Tab can leave the pages.
    /// </summary>
    /// <param name="backwards">Whether to move to the previous field.</param>
    /// <returns>The field moved to, the refused field when its value was not accepted, or <see langword="null"/>.</returns>
    public FormField? CommitAndMove(bool backwards)
    {
        var current = Editing ?? Focused;
        Commit();
        if (Editing is { } refused)
        {
            // The value was refused; stay on the field so it can be corrected.
            return refused;
        }

        var next = FindNeighbour(current, backwards);
        Focus(next);
        return next;
    }

    /// <summary>Gives a field the keyboard focus: a text field opens its editor; other fields show a focus ring.</summary>
    /// <param name="field">The field, or <see langword="null"/> to clear the focus.</param>
    public void Focus(FormField? field)
    {
        Focused = field;
        if (field?.Kind == FormFieldKind.Text)
        {
            _ = Activate(field);
        }
    }

    /// <summary>Acts on the focused field as Space does: ticks a check box or chooses a radio button.</summary>
    /// <returns><see langword="true"/> when the field took the key.</returns>
    public bool PressFocused()
    {
        if (Focused is not { Kind: FormFieldKind.CheckBox or FormFieldKind.RadioButton or FormFieldKind.PushButton } focused || FindCurrent(focused) is not { } field)
        {
            return false;
        }

        var handled = Activate(field);
        Focused = FindCurrent(field);
        return handled;
    }

    /// <summary>
    /// Recalculates the calculated fields from the others, as the form's calculate scripts ask, repeating until
    /// nothing changes so a total of totals settles. Results are formatted as each field's format script asks.
    /// </summary>
    public void Recalculate()
    {
        if (Filler is not { } filler || LoadScripts() is not { Count: > 0 } calculated)
        {
            return;
        }

        for (var pass = 0; pass < MaxCalculationPasses; pass++)
        {
            var values = ReadValues(filler);
            var changed = false;
            foreach (var scripts in calculated)
            {
                if (!FormScriptEngine.TryCalculate(scripts.Calculate, name => values.GetValueOrDefault(name), out var result))
                {
                    continue;
                }

                var text = FormScriptEngine.Format(scripts.Format, FormNumbers.Plain(result));
                if (string.Equals(values.GetValueOrDefault(scripts.Name), text, StringComparison.Ordinal) || !filler.SetText(scripts.PageIndex, scripts.Index, text))
                {
                    continue;
                }

                values[scripts.Name] = text;
                changed = true;
                LastPageChange = LastPageChange.Next(scripts.PageIndex);
            }

            if (!changed)
            {
                return;
            }
        }
    }

    /// <summary>Determines whether a field can be moved to with the keyboard and filled in.</summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> for fields that are not read-only and that the viewer fills.</returns>
    private static bool IsFillable(FormField field) =>
        !field.IsReadOnly && field.Kind is FormFieldKind.Text or FormFieldKind.CheckBox or FormFieldKind.RadioButton or FormFieldKind.ComboBox or FormFieldKind.ListBox or FormFieldKind.PushButton;

    /// <summary>Adds the fillable fields of one page in the page's tab order, then any the order does not list.</summary>
    /// <param name="all">The fillable fields so far.</param>
    /// <param name="fields">The fields of the page, in widget order.</param>
    /// <param name="sequence">The widget indexes in tab order; empty when the page names none.</param>
    private static void AddInTabOrder(List<FormField> all, List<FormField> fields, List<int> sequence)
    {
        var added = new HashSet<int>();
        foreach (var index in sequence)
        {
            var field = fields.Find(candidate => candidate.Index == index);
            if (field is not null && IsFillable(field) && added.Add(index))
            {
                all.Add(field);
            }
        }

        foreach (var field in fields)
        {
            if (IsFillable(field) && added.Add(field.Index))
            {
                all.Add(field);
            }
        }
    }

    /// <summary>Explains why a typed value is refused by its field's keystroke or validate script.</summary>
    /// <param name="scripts">The field's scripts.</param>
    /// <param name="text">The typed value.</param>
    /// <returns>The explanation, or <see langword="null"/> when the value is accepted.</returns>
    private static string? Problem(FieldScripts scripts, string text)
    {
        if (!FormScriptEngine.Accepts(scripts.Keystroke, text))
        {
            return scripts.Keystroke.Function switch
            {
                FormScriptFunction.Date => $"Type a date like {FormScriptEngine.Format(scripts.Keystroke, "2026-03-07")}.",
                FormScriptFunction.Time => $"Type a time like {FormScriptEngine.Format(scripts.Keystroke, "13:45")}.",
                FormScriptFunction.Special => "Type the number with the right count of digits.",
                _ => "Type a number.",
            };
        }

        return FormScriptEngine.Validate(scripts.Validate, text, out var message) ? null : message;
    }

    /// <summary>Picks a choice of a combo or list box.</summary>
    /// <param name="choice">The field and option.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ChooseOption(FormChoice choice) => Choose(choice.Field, choice.Option);

    /// <summary>Finds the fillable field after or before one, in page order, or the first or last when there is none.</summary>
    /// <param name="current">The current field, or <see langword="null"/>.</param>
    /// <param name="backwards">Whether to look backwards.</param>
    /// <returns>The field, or <see langword="null"/> past either end.</returns>
    private FormField? FindNeighbour(FormField? current, bool backwards)
    {
        var all = FillableFields();
        var at = current is null ? -1 : all.FindIndex(field => field.PageIndex == current.PageIndex && field.Index == current.Index);
        if (current is null || at < 0)
        {
            return all.Count == 0 ? null : all[backwards ? ^1 : 0];
        }

        var next = at + (backwards ? -1 : 1);
        return (uint)next < (uint)all.Count ? all[next] : null;
    }

    /// <summary>Lists every field that can be filled in, in page order.</summary>
    /// <returns>The fields.</returns>
    private List<FormField> FillableFields()
    {
        var all = new List<FormField>();
        if (Filler is not { HasForm: true } filler)
        {
            return all;
        }

        var order = DocumentFeature<IFormOrder>.Get(_owner.TryGetDocument());
        var sequence = new List<int>();
        for (var page = 0; page < _owner.PageCount; page++)
        {
            _scratch.Clear();
            filler.GetFields(page, _scratch);
            sequence.Clear();
            order?.GetTabOrder(page, sequence);
            AddInTabOrder(all, _scratch, sequence);
        }

        return all;
    }

    /// <summary>Reads a field again, with its current value.</summary>
    /// <param name="field">The field.</param>
    /// <returns>The field as it is now, or <see langword="null"/>.</returns>
    private FormField? FindCurrent(FormField field)
    {
        if (Filler is not { } filler)
        {
            return null;
        }

        _scratch.Clear();
        filler.GetFields(field.PageIndex, _scratch);
        foreach (var candidate in _scratch)
        {
            if (candidate.Index == field.Index)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Reads every field's value by name.</summary>
    /// <param name="filler">The form.</param>
    /// <returns>The values.</returns>
    private Dictionary<string, string> ReadValues(IFormFiller filler)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var page = 0; page < _owner.PageCount; page++)
        {
            _scratch.Clear();
            filler.GetFields(page, _scratch);
            foreach (var field in _scratch)
            {
                _ = values.TryAdd(field.Name, field.Kind is FormFieldKind.CheckBox or FormFieldKind.RadioButton && !field.IsChecked ? string.Empty : field.Value);
            }
        }

        return values;
    }

    /// <summary>Gets a field's scripts by name.</summary>
    /// <param name="name">The field's name.</param>
    /// <returns>The scripts, or <see langword="null"/> when it has none HyperPDF runs.</returns>
    private FieldScripts? ScriptsFor(string name)
    {
        _ = LoadScripts();
        return _scripts?.GetValueOrDefault(name);
    }

    /// <summary>Reads the form's scripts once, returning the calculated fields.</summary>
    /// <returns>The calculated fields, or <see langword="null"/> when the document has no scripts.</returns>
    private List<FieldScripts>? LoadScripts()
    {
        if (_scripts is not null)
        {
            return _calculated;
        }

        _scripts = [with(StringComparer.Ordinal)];
        _calculated = [];
        if (DocumentFeature<IFormScriptSource>.Get(_owner.TryGetDocument()) is not
            {
            } source)
        {
            return _calculated;
        }

        var all = new List<FieldScripts>();
        for (var page = 0; page < _owner.PageCount; page++)
        {
            source.GetScripts(page, all);
        }

        foreach (var scripts in all)
        {
            _ = _scripts.TryAdd(scripts.Name, scripts);
            if (scripts.Calculate.Function != FormScriptFunction.Unknown)
            {
                _calculated.Add(scripts);
            }
        }

        OrderCalculations(_calculated);
        return _calculated;
    }

    /// <summary>Puts the calculated fields in the order the form lists them (<c>/CO</c>); fields it does not list follow in page order.</summary>
    /// <param name="calculated">The calculated fields in page order, reordered in place.</param>
    private void OrderCalculations(List<FieldScripts> calculated)
    {
        if (calculated.Count < 2 || DocumentFeature<IFormOrder>.Get(_owner.TryGetDocument()) is not
            {
            } source)
        {
            return;
        }

        var listed = new List<string>();
        source.GetCalculationOrder(listed);
        if (listed.Count == 0)
        {
            return;
        }

        var position = new Dictionary<string, int>(listed.Count, StringComparer.Ordinal);
        foreach (var name in listed)
        {
            _ = position.TryAdd(name, position.Count);
        }

        // List.Sort is not stable, so the page order is kept as the tie-break.
        var pageOrder = new Dictionary<FieldScripts, int>(calculated.Count, ReferenceEqualityComparer.Instance);
        foreach (var scripts in calculated)
        {
            pageOrder[scripts] = pageOrder.Count;
        }

        calculated.Sort((a, b) =>
        {
            var byListed = position.GetValueOrDefault(a.Name, int.MaxValue).CompareTo(position.GetValueOrDefault(b.Name, int.MaxValue));
            return byListed != 0 ? byListed : pageOrder[a].CompareTo(pageOrder[b]);
        });
    }

    /// <summary>Starts a push button's action. The action runs in the background so the click returns at once.</summary>
    /// <param name="field">The button.</param>
    /// <returns><see langword="true"/> when the document can run the button's action.</returns>
    private bool StartButton(FormField field)
    {
        Focused = field;
        if (Actions is null || RunButtonCommand is not ICommand command || !command.CanExecute(field))
        {
            return false;
        }

        command.Execute(field);
        return true;
    }

    /// <summary>Runs a push button's action.</summary>
    /// <param name="field">The button.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task RunButton(FormField field) => await RunButtonAsync(field).ConfigureAwait(true);

    /// <summary>Runs the actions of the pages that became the current page, in the order they opened, until none wait.</summary>
    /// <param name="pageIndex">The zero based page index that started the run; it is already queued.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task PageOpened(int pageIndex)
    {
        _ = pageIndex;
        while (_pagesOpened.TryDequeue(out var page))
        {
            _ = await RunPageOpenedAsync(page).ConfigureAwait(true);
        }
    }

    /// <summary>Redraws every page, after an action changed fields, hid fields or switched layers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RefreshAllPages() => LastPageChange = LastPageChange.Next(PageChange.AllPages);

    /// <summary>Redraws the page of a changed field.</summary>
    /// <param name="field">The field.</param>
    /// <param name="changed">Whether it changed.</param>
    private void Refresh(FormField field, bool changed)
    {
        if (changed)
        {
            LastPageChange = LastPageChange.Next(field.PageIndex);
        }
    }
}
