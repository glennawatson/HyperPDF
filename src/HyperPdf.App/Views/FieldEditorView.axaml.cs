// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Forms;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>The editor placed over the form field being filled in, with the marks under its misspelled words.</summary>
[DebuggerDisplay("FieldEditorView: {ViewModel}")]
public sealed partial class FieldEditorView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>The smallest editor font size.</summary>
    private const double MinFieldFontSize = 10;

    /// <summary>The editor font size as a share of the field height.</summary>
    private const double FieldFontShare = 0.6;

    /// <summary>Halves the room around a comb letter, to centre it in its box.</summary>
    private const double HalfCell = 0.5;

    /// <summary>The misspelled words in the field being edited, reused between checks.</summary>
    private readonly List<Core.Reading.TextRange> _misspelled = [];

    /// <summary>Initializes a new instance of the <see cref="FieldEditorView"/> class.</summary>
    public FieldEditorView()
    {
        InitializeComponent();
        FieldSpelling.Editor = FieldEditor;
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.Forms.EditText, static v => v.FieldEditor.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Forms.Editing).SubscribeSafe(ShowFieldEditor, OnError));
            disposables.Add(FieldEditor.KeyDowns.Escape.InjectLatest(tabs).SubscribeSafe(static tab => tab.Forms.Cancel(), OnError));
            disposables.Add(FieldEditor.KeyDowns.Tab.MarkHandled().Inject(tabs).SubscribeSafe(MoveToNextField, OnError));

            // ShowFieldEditor sets AcceptsReturn from the field, so a single-line field commits on Enter.
            disposables.Add(FieldEditor.KeyDowns.Enter
                .Where(static args => args.Source is TextBox { AcceptsReturn: false })
                .InjectLatest(tabs)
                .SubscribeSafe(static tab => tab.Forms.Commit(), OnError));
            disposables.Add(FieldEditor.LostFocuses
                .Where(static _ => !FocusAnnouncementRepair.IsRepeating)
                .InjectLatest(tabs)
                .SubscribeSafe(static tab => tab.Forms.Commit(), OnError));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Forms.EditText, static v => v.ViewModel!.Forms.KeptWordsVersion, static (text, _) => text)
                .SubscribeSafe(UpdateSpelling, OnError));
            disposables.Add(FieldEditor.TemplateScrolls.SubscribeSafe(_ => FieldSpelling.InvalidateVisual(), OnError));
            disposables.Add(FieldEditor.ContextRequests.Inject(tabs).SubscribeSafe(OfferCorrections, OnError));
        });
    }

    /// <summary>Gets or sets the pages the editor sits over, which place it on the field.</summary>
    internal PageCanvas? Pages { get; set; }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Describes a field and the possible spelling mistakes in it, for screen readers.</summary>
    /// <param name="text">The field's text.</param>
    /// <param name="misspelled">The misspelled words.</param>
    /// <returns>The description.</returns>
    private static string DescribeSpelling(string text, List<Core.Reading.TextRange> misspelled)
    {
        if (misspelled.Count == 0)
        {
            return Descriptions.FormField;
        }

        var words = new List<string>(misspelled.Count);
        foreach (var range in misspelled)
        {
            words.Add(text.Substring(range.Start, range.Length));
        }

        var count = misspelled.Count == 1 ? "1 possible spelling mistake" : string.Create(CultureInfo.CurrentCulture, $"{misspelled.Count} possible spelling mistakes");
        return string.Create(CultureInfo.CurrentCulture, $"{count}: {string.Join(", ", words)}. Open the menu on a word for corrections. {Descriptions.FormField}");
    }

    /// <summary>Determines whether a word is one of the marked words.</summary>
    /// <param name="misspelled">The marked words.</param>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> when marked.</returns>
    private static bool Contains(List<Core.Reading.TextRange> misspelled, Core.Reading.TextRange word)
    {
        foreach (var range in misspelled)
        {
            if (range == word)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Marks the misspelled words in the field being edited, and says how many there are to screen readers.</summary>
    /// <param name="text">The field's text.</param>
    private void UpdateSpelling(string text)
    {
        if (ViewModel is not { } tab || (tab.Forms.Editing is null && _misspelled.Count == 0))
        {
            // No field is being edited and nothing is marked, so there is nothing to check or clear.
            return;
        }

        tab.Forms.FindMisspelled(text ?? string.Empty, _misspelled);
        FieldSpelling.Show(_misspelled);
        AutomationProperties.SetHelpText(FieldEditor, DescribeSpelling(text ?? string.Empty, _misspelled));
    }

    /// <summary>Offers corrections for a misspelled word when the editor's menu is asked for, keeping the normal menu otherwise.</summary>
    /// <param name="request">The request and the tab being edited.</param>
    private void OfferCorrections(Injected<ContextRequestedEventArgs, DocumentTabViewModel> request)
    {
        var (e, tab) = request;
        if (FieldEditor.Text is not { Length: > 0 })
        {
            return;
        }

        var index = FieldEditor.CaretIndex;
        if (SpellingUnderlines.FindPresenter(FieldEditor) is { } presenter && e.TryGetPosition(presenter, out var point))
        {
            index = presenter.TextLayout.HitTestPoint(point).TextPosition;
        }

        var suggestions = tab.Forms.SuggestAt(index, out var word);
        if (word.IsEmpty || !Contains(_misspelled, word))
        {
            return;
        }

        var misspelled = FieldEditor.Text.Substring(word.Start, word.Length);
        List<Control> items = [];
        foreach (var suggestion in suggestions)
        {
            items.Add(new MenuItem { Header = suggestion, Command = tab.Forms.CorrectCommand, CommandParameter = new SpellingFix(word, suggestion) });
        }

        if (items.Count == 0)
        {
            items.Add(new MenuItem { Header = "No suggestions", IsEnabled = false });
        }

        items.Add(new Separator());
        items.Add(new MenuItem { Header = "_Keep This Spelling", Command = tab.Forms.KeepSpellingCommand, CommandParameter = misspelled });
        new ContextMenu { ItemsSource = items }.Open(FieldEditor);
        e.Handled = true;
    }

    /// <summary>Places the editor over a field and focuses it, or hides it.</summary>
    /// <param name="field">The field being edited, or <see langword="null"/>.</param>
    private void ShowFieldEditor(FormField? field)
    {
        if (field is null || Pages is not { } pages)
        {
            FieldEditor.IsVisible = false;
            FieldSpelling.IsVisible = false;
            _ = Pages?.Focus();
            return;
        }

        var rect = pages.GetCanvasRect(field.PageIndex, field.Bounds);
        Canvas.SetLeft(this, rect.X);
        Canvas.SetTop(this, rect.Y);
        Width = rect.Width;
        Height = rect.Height;
        FieldEditor.AcceptsReturn = field.IsMultiline && !field.IsComb;
        AutomationProperties.SetName(FieldEditor, string.IsNullOrWhiteSpace(field.Name) ? "Form field" : field.Name);
        FieldEditor.FontSize = field.FontSize > 0 ? field.FontSize * pages.PageScale : Math.Max(MinFieldFontSize, rect.Height * FieldFontShare);
        FieldEditor.MaxLength = field.MaxLength;
        SpaceCombLetters(field, rect.Width);
        FieldEditor.IsVisible = true;
        FieldSpelling.IsVisible = true;
        _ = Signal.Return(RxVoid.Default).ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => FocusFieldEditor(), OnError);
    }

    /// <summary>Focuses the form field editor and selects its text.</summary>
    private void FocusFieldEditor()
    {
        _ = FieldEditor.Focus();
        FieldEditor.SelectAll();
    }

    /// <summary>Spreads a comb field's letters one to a box, in a fixed width font; other fields type normally.</summary>
    /// <param name="field">The field.</param>
    /// <param name="width">The field's width on the canvas.</param>
    private void SpaceCombLetters(FormField field, double width)
    {
        if (!field.IsComb)
        {
            FieldEditor.ClearValue(TemplatedControl.LetterSpacingProperty);
            FieldEditor.ClearValue(TemplatedControl.FontFamilyProperty);
            FieldEditor.ClearValue(TextBox.PaddingProperty);
            return;
        }

        var font = PageFonts.Get(Core.Text.StandardFontFamilies.Mono);
        var cell = width / field.MaxLength;
        var advance = new FormattedText("0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(font), FieldEditor.FontSize, null).WidthIncludingTrailingWhitespace;
        FieldEditor.FontFamily = font;
        FieldEditor.LetterSpacing = cell - advance;
        FieldEditor.Padding = new((cell - advance) * HalfCell, 0, 0, 0);
    }

    /// <summary>Moves to the next field on Tab, or the previous one on Shift+Tab.</summary>
    /// <param name="press">The key press and the tab being edited.</param>
    private void MoveToNextField(Injected<KeyEventArgs, DocumentTabViewModel> press)
    {
        var (args, tab) = press;
        if (tab.Forms.CommitAndMove((args.KeyModifiers & KeyModifiers.Shift) != 0) is not { } next || next.Kind == FormFieldKind.Text)
        {
            return;
        }

        // The editor closes for a box or list, so the pages take the keys that fill it in.
        tab.NavigateTo(new(next.PageIndex, next.Bounds, 0));
        Pages?.InvalidateVisual();
    }
}
