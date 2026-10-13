// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>
/// Tests the notice shown when a file declares requirements the viewer cannot meet (ISO 32000-2 12.11), and that a
/// document whose requirements stop its processing keeps forms and actions off.
/// </summary>
public sealed class RequirementNoticeTests
{
    /// <summary>A requirement for running scripts, which the viewer never does, with the default penalty of 100.</summary>
    private const string CriticalRequirements = "/Requirements [<< /S /EnableJavaScripts >> << /S /Navigation >>]";

    /// <summary>An optional requirement for page transitions, which the viewer does not play.</summary>
    private const string OptionalRequirements = "/Requirements [<< /S /Transitions /Penalty 0 >>]";

    /// <summary>A critical unmet requirement shows the notice, turns forms and actions off, and stays off after the notice is dismissed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CriticalRequirementKeepsFormsAndActionsOff()
    {
        using var test = new TestServices(new HyperPdfEngine());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("critical.pdf", FormRuntimeSamples.Create(string.Empty, CriticalRequirements))]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();

        await Assert.That(tab.Notices.RequirementNotice).Contains("Running scripts");
        await Assert.That(tab.Notices.RequirementNotice).Contains(RequirementNotices.StaticCopy);
        await Assert.That(tab.Notices.InteractionBlocked).IsTrue();
        await Assert.That(tab.Forms.HasForm).IsFalse();
        FormField clear = new(0, FormRuntimeSamples.ClearIndex, "Clear", FormFieldKind.PushButton, default, string.Empty, false, [], -1, false, false, false);
        await Assert.That(await tab.Forms.RunButtonAsync(clear)).IsEqualTo(FormActionResult.None);

        _ = await tab.Notices.ToggleRequirementDetailsCommand.Execute().ToTask();
        await Assert.That(tab.Notices.RequirementDetails).Contains("Running scripts");
        _ = await tab.Notices.DismissRequirementNoticeCommand.Execute().ToTask();
        await Assert.That(tab.Notices.RequirementNotice).IsNull();
        await Assert.That(tab.Notices.InteractionBlocked).IsTrue();
        await Assert.That(tab.Forms.HasForm).IsFalse();
    }

    /// <summary>An optional unmet requirement gives a calm note and keeps the form working.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptionalRequirementKeepsFormsOn()
    {
        using var test = new TestServices(new HyperPdfEngine());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("optional.pdf", FormRuntimeSamples.Create(string.Empty, OptionalRequirements))]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();

        await Assert.That(tab.Notices.RequirementNotice).Contains("Page transition effects");
        await Assert.That(tab.Notices.RequirementNotice).Contains(RequirementNotices.UsesFallback);
        await Assert.That(tab.Notices.InteractionBlocked).IsFalse();
        await Assert.That(tab.Forms.HasForm).IsTrue();
    }

    /// <summary>A file with no requirements shows no requirement notice.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoRequirementsShowNoNotice()
    {
        using var test = new TestServices(new HyperPdfEngine());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("plain.pdf", FormRuntimeSamples.Create(string.Empty))]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();

        await Assert.That(tab.Notices.RequirementNotice).IsNull();
        await Assert.That(tab.Notices.InteractionBlocked).IsFalse();
    }

    /// <summary>A save that removed requirements adds their notice after the other notes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveNoteIncludesRemovedRequirements()
    {
        var outcome = new DocumentSaveOutcome(true, null) { RequirementWarnings = ["Removed one.", "Removed two."] };

        await Assert.That(SaveText.Notice(outcome)).IsEqualTo("Removed one. Removed two.");
    }
}
