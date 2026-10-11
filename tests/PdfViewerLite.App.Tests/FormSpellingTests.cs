// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests spell checking in form fields: marks, corrections, kept words and the setting that turns it off.</summary>
public sealed class FormSpellingTests
{
    /// <summary>Text with two misspelled words.</summary>
    private const string Typed = "Teh recieve";

    /// <summary>The misspelled words in the typed text.</summary>
    private const int TwoMistakes = 2;

    /// <summary>A place inside "recieve".</summary>
    private const int InsideSecondWord = 6;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1000;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Verifies misspelled words are found, corrected and kept, and nothing is checked once turned off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsCorrectsAndKeepsWords()
    {
        using var test = new TestServices();
        using var main = await OpenFormAsync(test);
        var forms = main.SelectedTab!.Forms;
        _ = forms.Activate(NameField(main.SelectedTab));
        forms.EditText = Typed;
        List<TextRange> found = [];

        forms.FindMisspelled(forms.EditText, found);
        await Assert.That(found.Count).IsEqualTo(TwoMistakes);
        var suggestions = forms.SuggestAt(InsideSecondWord, out var word);
        await Assert.That(suggestions).IsEquivalentTo(["receive", "relieve"]);

        SpellingFix fix = new(word, suggestions[0]);
        _ = await forms.CorrectCommand.Execute(fix).ToTask();
        await Assert.That(forms.EditText).IsEqualTo("Teh receive");

        _ = await forms.KeepSpellingCommand.Execute("Teh").ToTask();
        forms.FindMisspelled(forms.EditText, found);
        await Assert.That(found).IsEmpty();
        await Assert.That(test.Services.Settings.IgnoredWords).Contains("Teh");

        using (var preferences = new PreferencesViewModel(test.Services))
        {
            await Assert.That(preferences.CheckSpelling).IsTrue();
            preferences.CheckSpelling = false;
        }

        await Assert.That(test.Services.Settings.CheckSpelling).IsFalse();
        forms.FindMisspelled("recieve", found);
        await Assert.That(found).IsEmpty();
        await Assert.That(forms.SuggestAt(1, out _)).IsEmpty();
    }

    /// <summary>Verifies the editor marks misspelled words, tells screen readers about them, and offers corrections in its menu.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksWordsInTheEditor()
    {
        using var test = new TestServices();
        using var main = await OpenFormAsync(test);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            _ = tab.Forms.Activate(NameField(tab));
            tab.Forms.EditText = Typed;
            var underlines = window.GetVisualDescendants().OfType<SpellingUnderlines>().Single();
            var editor = window.GetVisualDescendants().OfType<TextBox>().Single(static t => t.Name == "FieldEditor");

            await Assert.That(await UiWait.UntilAsync(() => underlines.IsVisible && underlines.Ranges.Count == TwoMistakes)).IsTrue();
            await Assert.That(AutomationProperties.GetHelpText(editor)).StartsWith("2 possible spelling mistakes: Teh, recieve.");

            editor.CaretIndex = InsideSecondWord;
            ContextRequestedEventArgs request = new();
            editor.RaiseEvent(request);
            await Assert.That(request.Handled).IsTrue();

            tab.Forms.EditText = "the form";
            await Assert.That(await UiWait.UntilAsync(() => underlines.Ranges.Count == 0)).IsTrue();
            await Assert.That(AutomationProperties.GetHelpText(editor)).IsEqualTo(Descriptions.FormField);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Opens the test form.</summary>
    /// <param name="test">The test services.</param>
    /// <returns>The window's view model, showing the form.</returns>
    private static async Task<MainViewModel> OpenFormAsync(TestServices test)
    {
        var path = Path.Combine(test.Directory, "form.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateForm());
        var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        return main;
    }

    /// <summary>Finds the form's text field.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The field.</returns>
    private static FormField NameField(DocumentTabViewModel tab)
    {
        List<FormField> fields = [];
        ((IFormFiller)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(tab.TryGetDocument()!, typeof(IFormFiller))!).GetFields(0, fields);
        return fields.Single(static field => field.Kind == FormFieldKind.Text);
    }
}
