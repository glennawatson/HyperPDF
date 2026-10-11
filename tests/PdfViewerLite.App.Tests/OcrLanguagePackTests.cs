// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.VisualTree;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks the language flow of text recognition: English just works, other languages are offered in one click, and nothing asks the person to install anything.</summary>
public sealed class OcrLanguagePackTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The name of the generated scanned document.</summary>
    private const string ScanDocument = "scan.pdf";

    /// <summary>German's code.</summary>
    private const string German = "deu";

    /// <summary>The setting for English and German together.</summary>
    private const string EnglishAndGerman = "eng+deu";

    /// <summary>The side of the blank test scan, in pixels.</summary>
    private const int ScanSide = 200;

    /// <summary>A clearly read English scan is recognised the first time, with nothing downloaded and no question asked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EnglishWorksTheFirstTime()
    {
        var ocr = new FakeOcr(true);
        using var test = new TestServices(ocr);
        using var main = await OpenScanAsync(test);
        var tab = main.SelectedTab!;

        _ = await tab.TextRecognition.RecognizeCommand.Execute().ToTask();

        await Assert.That(tab.TextRecognition.IsLanguageBarOpen).IsFalse();
        await Assert.That(ocr.Downloads).IsEqualTo(0);
        await Assert.That(ocr.Created).IsEquivalentTo(["eng"]);
        await Assert.That(tab.Notice).IsEqualTo(TextRecognitionViewModel.Describe(1, 0, 1, false));
    }

    /// <summary>A first page that reads poorly is not written; the language bar asks, and one click downloads the language and recognises.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsksForTheLanguageThenDownloadsInOneClick()
    {
        var ocr = new FakeOcr(true) { Confidence = FakeOcr.Poor };
        using var test = new TestServices(ocr);
        using var main = await OpenScanAsync(test);
        var tab = main.SelectedTab!;
        var recognition = tab.TextRecognition;

        _ = await recognition.RecognizeCommand.Execute().ToTask();
        var asked = recognition.IsLanguageBarOpen;
        var question = recognition.LanguageBarText;
        var unchanged = !tab.HasUnsavedChanges && tab.Notice is null;
        recognition.LanguageIndex = IndexOf(German);
        var action = recognition.LanguageActionText;
        _ = await recognition.ConfirmLanguageCommand.Execute().ToTask();

        await Assert.That(asked).IsTrue();
        await Assert.That(question).StartsWith("The first scanned page does not read well as English, so nothing has been changed yet.");
        await Assert.That(unchanged).IsTrue();
        await Assert.That(action).IsEqualTo("Download German (about 2 MB)");
        await Assert.That(ocr.Downloads).IsEqualTo(1);
        await Assert.That(recognition.IsLanguageBarOpen).IsFalse();
        await Assert.That(test.Services.Settings.OcrLanguage).IsEqualTo(German);
        await Assert.That(ocr.Created[^1]).IsEqualTo(German);
        await Assert.That(tab.Notice).IsEqualTo(TextRecognitionViewModel.Describe(1, 0, 1, false));
    }

    /// <summary>Choosing a language that is already on this computer recognises straight away, without a download.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosingAnInstalledLanguageRecognisesAtOnce()
    {
        var ocr = new FakeOcr(true);
        using var test = new TestServices(ocr);
        using var main = await OpenScanAsync(test);
        var recognition = main.SelectedTab!.TextRecognition;

        _ = await recognition.ChooseLanguageCommand.Execute().ToTask();
        var open = recognition.IsLanguageBarOpen;
        var action = recognition.LanguageActionText;
        _ = await recognition.ConfirmLanguageCommand.Execute().ToTask();

        await Assert.That(open).IsTrue();
        await Assert.That(action).IsEqualTo("Recognise in English");
        await Assert.That(ocr.Downloads).IsEqualTo(0);
        await Assert.That(main.SelectedTab.Notice).IsEqualTo(TextRecognitionViewModel.Describe(1, 0, 1, false));
    }

    /// <summary>English and a downloaded language are gathered into one folder, because Tesseract reads a run's languages from one place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GathersShippedAndDownloadedLanguages()
    {
        var ocr = new FakeOcr(true);
        using var test = new TestServices(ocr);
        var setup = test.Services.Ocr;
        await setup.DownloadAsync([OcrLanguageCatalog.Find(German)!], new Progress<double>(), CancellationToken.None);
        string[] both = ["eng", German];
        var before = setup.FindLanguageData(EnglishAndGerman, setup.LanguageDirectory);

        setup.GatherLanguages(both);

        await Assert.That(before).IsNull();
        await Assert.That(setup.FindLanguageData(EnglishAndGerman, setup.LanguageDirectory)).IsEqualTo(setup.LanguageDirectory);
        await Assert.That(setup.PacksNeededFor(both, [])).IsEmpty();
    }

    /// <summary>When Tesseract cannot start, the notice says so without asking the person to install anything.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverAsksToInstall()
    {
        using var test = new TestServices(new FakeOcr(false));
        using var main = await OpenScanAsync(test);
        var tab = main.SelectedTab!;

        _ = await tab.TextRecognition.RecognizeCommand.Execute().ToTask();

        await Assert.That(tab.TextRecognition.IsLanguageBarOpen).IsFalse();
        await Assert.That(tab.Notice).IsEqualTo(OcrSetup.EngineUnavailableText);
        await Assert.That(tab.Notice!.Contains("install", StringComparison.OrdinalIgnoreCase)).IsFalse();
    }

    /// <summary>Preferences lists every language with English included; ticking German saves both; Download and Remove change the row.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreferencesChoosesDownloadsAndRemovesLanguages()
    {
        var ocr = new FakeOcr(true);
        using var test = new TestServices(ocr);
        using var preferences = new PreferencesViewModel(test.Services);
        var languages = preferences.OcrLanguages;
        var english = languages.Items[0];
        var german = languages.Items.First(static item => item.Pack.Code == German);
        var statusBefore = german.StatusText;

        german.IsSelected = true;
        var saved = test.Services.Settings.OcrLanguage;
        _ = await german.DownloadCommand.Execute().ToTask();
        var statusAfter = german.StatusText;
        var canRemove = german.CanRemove;
        _ = await german.RemoveCommand.Execute().ToTask();

        await Assert.That(languages.Items.Count).IsEqualTo(OcrLanguageCatalog.Packs.Count);
        await Assert.That(english.IsSelected).IsTrue();
        await Assert.That(english.StatusText).IsEqualTo("Included");
        await Assert.That(english.CanDownload).IsFalse();
        await Assert.That(statusBefore).IsEqualTo("Not downloaded, about 2 MB");
        await Assert.That(saved).IsEqualTo(EnglishAndGerman);
        await Assert.That(statusAfter).IsEqualTo("Downloaded");
        await Assert.That(canRemove).IsTrue();
        await Assert.That(languages.StatusText).IsEqualTo("German removed.");
        await Assert.That(german.CanDownload).IsTrue();
    }

    /// <summary>The document's language bar appears with a named language picker and a single action button with a tooltip.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentShowsTheLanguageBar()
    {
        using var test = new TestServices(new FakeOcr(true));
        using var main = await OpenScanAsync(test);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var bar = view.FindControl<Border>("LanguageBar")!;
            var hiddenBefore = !bar.IsVisible;

            _ = await main.SelectedTab!.TextRecognition.ChooseLanguageCommand.Execute().ToTask();
            var shown = await UiWait.UntilAsync(() => bar.IsVisible);
            var button = view.FindControl<Button>("LanguageActionButton")!;
            var box = view.FindControl<ComboBox>("LanguageBox")!;

            await Assert.That(hiddenBefore).IsTrue();
            await Assert.That(shown).IsTrue();
            await Assert.That(button.Content).IsEqualTo("Recognise in English");
            await Assert.That(ToolTip.GetTip(button) as string).IsEqualTo(Descriptions.RecognizeInLanguage);
            await Assert.That(box.SelectedIndex).IsEqualTo(0);
            await Assert.That(Avalonia.Automation.AutomationProperties.GetName(box)).IsEqualTo("Document language");
            await Assert.That(string.Join(", ", AccessibilityTests.Unnamed(view))).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The Preferences window shows a row for each language, with a named Download button and tooltip.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreferencesShowsLanguages()
    {
        using var test = new TestServices(new FakeOcr(true));
        var window = new PreferencesWindow { ViewModel = new(test.Services) };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<OcrLanguageItemView>().Any());
            var rows = window.GetVisualDescendants().OfType<OcrLanguageItemView>().ToList();
            var german = rows[IndexOf(German)];
            var download = german.FindControl<Button>("DownloadLanguageButton")!;
            var english = rows[0].FindControl<CheckBox>("UseLanguageBox")!;

            await Assert.That(english.Content).IsEqualTo("English");
            await Assert.That(english.IsChecked == true).IsTrue();
            await Assert.That(download.IsVisible).IsTrue();
            await Assert.That(ToolTip.GetTip(download) as string).IsEqualTo(Descriptions.DownloadLanguage);
            await Assert.That(Avalonia.Automation.AutomationProperties.GetName(download)).IsEqualTo("Download language pack");
            await Assert.That(window.FindControl<TextBlock>("OcrNote")!.Text!).StartsWith("English is included.");
        }
        finally
        {
            window.Close();
            window.ViewModel?.Dispose();
        }
    }

    /// <summary>Opens a one page blank scan, which has no text.</summary>
    /// <param name="test">The services.</param>
    /// <returns>The main view model with the scan open.</returns>
    private static async Task<MainViewModel> OpenScanAsync(TestServices test)
    {
        var path = Path.Combine(test.Directory, ScanDocument);
        var white = new byte[ScanSide * ScanSide];
        Array.Fill(white, byte.MaxValue);
        await File.WriteAllBytesAsync(path, TestPdf.CreateScan(white, ScanSide, ScanSide));
        var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        return main;
    }

    /// <summary>Gets a language's place in the language list.</summary>
    /// <param name="code">The language code.</param>
    /// <returns>The index.</returns>
    private static int IndexOf(string code)
    {
        for (var i = 0; i < OcrLanguageCatalog.Packs.Count; i++)
        {
            if (OcrLanguageCatalog.Packs[i].Code == code)
            {
                return i;
            }
        }

        return -1;
    }
}
