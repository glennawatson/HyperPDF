// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Views;
using HyperPdf.Core.Flattening;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the Save Flattened Copy window's state: options, saving, refusing, cancelling and failing.</summary>
public sealed class FlattenCopyViewModelTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The passes needed to arrange existing translated choice controls.</summary>
    private const int CultureRenderPasses = 3;

    /// <summary>The document name.</summary>
    private const string SourceName = "form.pdf";

    /// <summary>The ending of the temporary file a copy is written to.</summary>
    private const string TemporarySuffix = ".flatten";

    /// <summary>The longest wait for a run to start.</summary>
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    /// <summary>A completed report changes language without saving again or replacing its document state.</summary>
    /// <returns>A task.</returns>
    [Test]
    [Category("Localisation")]
    public async Task CompletedReport_ChangesLanguageAndRetainsResult()
    {
        var previous = UiText.UICulture;
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var source = test.CreateDocument(SourceName, TestPdf.Create(Pages));
        await TestServices.OpenAndWaitAsync(main, [source]);
        var document = main.SelectedTab!.TryGetDocument();
        var writer = new FakeFlattenCopyWriter();
        using var vm = new FlattenCopyViewModel(writer, source);
        var destination = Path.Combine(test.Directory, "retained-report.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));
        var window = new FlattenCopyWindow { ViewModel = vm };
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            _ = await vm.SaveCommand.Execute().ToTask();
            window.Show();
            var result = vm.Result;
            var selected = vm.SelectedScope;
            foreach (var language in new[] { "fr", "de", "zh-Hans", "ar", "es", "pt", "en" })
            {
                InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(language));
                LocalisationRefresh.Refresh(vm);
                CP.Localisation.Avalonia.LocalisationWindows.Refresh(window);
                await UiWait.PumpAsync(CultureRenderPasses);
                await AssertChoiceLabelsAsync(window, vm);
                await Assert.That(vm.ResultText).Contains(UiText.Format("Saved as {0}. Your open file was not changed.", Path.GetFileName(destination)));
                await Assert.That(vm.StatusText).IsEqualTo(UiText.Get("Done. The copy is saved."));
                await Assert.That(ReferenceEquals(vm.Result, result)).IsTrue();
                await Assert.That(ReferenceEquals(vm.SelectedScope, selected)).IsTrue();
                await Assert.That(vm.SavedPath).IsEqualTo(destination);
                await Assert.That(ReferenceEquals(main.SelectedTab.TryGetDocument(), document)).IsTrue();
                await Assert.That(writer.Calls).IsEqualTo(1);
            }
        }
        finally
        {
            window.Close();
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>The window starts with the suggested options and a name after the open file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartsWithTheSuggestedOptions()
    {
        using var test = new TestServices();
        using var vm = Create(test, new());

        using (Assert.Multiple())
        {
            await Assert.That(vm.SuggestedName).IsEqualTo("form (flattened).pdf");
            await Assert.That(vm.StatusText).StartsWith("Ready.");
            await Assert.That(vm.Options).IsEqualTo(FlattenCopyOptions.Default);
        }
    }

    /// <summary>The chosen options reach the writer, and the summary counts what was flattened.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PassesTheChosenOptionsAndSummarises()
    {
        using var test = new TestServices();
        var fake = new FakeFlattenCopyWriter();
        using var vm = Create(test, fake);
        vm.SelectedScope = vm.Scopes[0];
        vm.SelectedLook = vm.Looks[1];
        vm.TurnPagesUpright = true;
        var destination = Path.Combine(test.Directory, "kept.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.LastOptions).IsEqualTo(new(FlattenCopyScope.FormFields, FlattenCopyLook.Print, true));
            await Assert.That(File.Exists(destination)).IsTrue();
            await Assert.That(File.Exists(Temporary(destination))).IsFalse();
            await Assert.That(vm.SavedPath).IsEqualTo(destination);
            await Assert.That(vm.StatusText).IsEqualTo("Done. The copy is saved.");
            await Assert.That(vm.ResultText).Contains("3 form fields are now part of the page");
            await Assert.That(vm.ResultText).Contains("2 comment, highlight or other marks are now part of the page");
            await Assert.That(vm.ResultText).Contains("The form was removed");
            await Assert.That(vm.ResultText).Contains("• 1 annotation was marked hidden");
            await Assert.That(vm.ResultText).DoesNotContain("12.5.3");
        }
    }

    /// <summary>A signed document is refused: nothing is saved and the reason is in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusalSavesNothingAndExplains()
    {
        using var test = new TestServices();
        var fake = new FakeFlattenCopyWriter { Problems = [new("The document is signed, so no copy was made.", "12.8")] };
        using var vm = Create(test, fake);
        var destination = Path.Combine(test.Directory, "refused.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(Temporary(destination))).IsFalse();
            await Assert.That(vm.SavedPath).IsNull();
            await Assert.That(vm.StatusText).Contains("could not be made");
            await Assert.That(vm.ResultText).Contains("so no file was saved");
            await Assert.That(vm.ResultText).Contains("• The document is signed");
        }
    }

    /// <summary>A destination that is the open file is refused, and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverOverwritesTheOpenFile()
    {
        using var test = new TestServices();
        var fake = new FakeFlattenCopyWriter();
        using var vm = Create(test, fake);
        var source = Path.Combine(test.Directory, SourceName);
        var before = await File.ReadAllBytesAsync(source);
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(source));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Calls).IsEqualTo(0);
            await Assert.That(vm.Error).Contains("replace the file you have open");
            await Assert.That(await File.ReadAllBytesAsync(source)).IsEquivalentTo(before);
        }
    }

    /// <summary>Cancel stops the run and keeps no file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelStopsTheRunAndKeepsNoFile()
    {
        using var test = new TestServices();
        var fake = new FakeFlattenCopyWriter { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = Create(test, fake);
        var destination = Path.Combine(test.Directory, "cancelled.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        var run = vm.SaveCommand.Execute().ToTask();
        await fake.Started.Task.WaitAsync(StartTimeout);
        await Assert.That(vm.IsRunning).IsTrue();
        _ = await vm.CancelCommand.Execute().ToTask();
        _ = await run;

        using (Assert.Multiple())
        {
            await Assert.That(vm.IsRunning).IsFalse();
            await Assert.That(vm.StatusText).IsEqualTo("Cancelled. No file was saved.");
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(Temporary(destination))).IsFalse();
        }
    }

    /// <summary>A failed run says what went wrong in words and keeps no file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailureIsExplained()
    {
        using var test = new TestServices();
        var fake = new FakeFlattenCopyWriter { Failure = new InvalidDataException("The file is damaged.") };
        using var vm = Create(test, fake);
        var destination = Path.Combine(test.Directory, "failed.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(vm.Error).IsEqualTo("Could not save the copy: The file is damaged.");
            await Assert.That(vm.StatusText).IsEqualTo("The copy was not saved.");
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(Temporary(destination))).IsFalse();
        }
    }

    /// <summary>On HyperPDF the command is on, opens the window, and a real flattened copy reopens with the same pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RealCopyReopensWithTheSamePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(SourceName, Pages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        await Assert.That(tab.Copies.Flatten.CanRun).IsTrue();
        await Assert.That(tab.Copies.Flatten.MenuText).IsEqualTo("Save Flat_tened Copy…");
        var destination = Path.Combine(test.Directory, "flattened.pdf");
        FlattenCopyResult? result = null;
        using var handler = tab.Copies.Flatten.Interaction.RegisterHandler(async context =>
        {
            using var picker = context.Input.SaveInteraction.RegisterHandler(pick => pick.SetOutput(destination));
            _ = await context.Input.SaveCommand.Execute().ToTask();
            result = context.Input.Result;
            context.SetOutput(RxVoid.Default);
        });

        _ = await tab.Copies.Flatten.RunCommand.Execute().ToTask();

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Flattened).IsTrue().Because(string.Join("; ", result.Problems.Select(static p => p.Description)));
        await TestServices.OpenAndWaitAsync(main, [destination]);
        await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab!.IsLoaded)).IsTrue();
        await Assert.That(main.Tabs.Single(t => t.FilePath == destination).Source.PageSizes.Length).IsEqualTo(Pages);
    }

    /// <summary>Checks the actual displayed choice retains live translated text.</summary>
    /// <param name="window">The existing copy window.</param>
    /// <param name="model">Its unchanged request.</param>
    /// <returns>A task.</returns>
    private static async Task AssertChoiceLabelsAsync(FlattenCopyWindow window, FlattenCopyViewModel model)
    {
        var list = window.FindControl<ListBox>("ScopeList")!;
        var labels = list.GetVisualDescendants().OfType<TextBlock>().Select(static label => label.Text).ToArray();
        await Assert.That(labels).Contains(model.SelectedScope!.Name);
        await Assert.That(labels).Contains(model.SelectedScope.Description);
        var selected = list.ContainerFromItem(model.SelectedScope)!;
        await Assert.That(Avalonia.Automation.AutomationProperties.GetName(selected)).Contains(model.SelectedScope.Name);
    }

    /// <summary>Gets the temporary file a copy is written to before it is moved into place.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The temporary path.</returns>
    private static string Temporary(string destination) => $"{destination}{TemporarySuffix}";

    /// <summary>Creates a window view model over a real source file.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="writer">The writer.</param>
    /// <returns>The view model.</returns>
    private static FlattenCopyViewModel Create(TestServices test, FakeFlattenCopyWriter writer) =>
        new(writer, test.CreateDocument(SourceName, TestPdf.Create(Pages)));
}
