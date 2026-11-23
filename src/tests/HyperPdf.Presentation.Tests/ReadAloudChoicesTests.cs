// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Core.Settings;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Tests;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Presentation.Tests;

/// <summary>Tests the choices reading aloud offers: one list of every voice, the dimming of the page, and asking for an Azure key.</summary>
public sealed class ReadAloudChoicesTests
{
    /// <summary>The pages of the test document.</summary>
    private const int Pages = 2;

    /// <summary>The width of the part of the canvas in view.</summary>
    private const double ViewWidth = 800;

    /// <summary>The height of the part of the canvas in view.</summary>
    private const double ViewHeight = 600;

    /// <summary>The words every Kokoro voice's name starts with.</summary>
    private const string Kokoro = "Kokoro:";

    /// <summary>The words every Azure voice's name starts with.</summary>
    private const string Azure = "Azure:";

    /// <summary>Every voice of every engine is in the one list, each named with its engine.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryVoiceOfEveryEngineIsOffered()
    {
        using var test = new TestServices(new FallbackPlatform(), new FakeSpeech(true, true));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("voices.pdf", Pages)]);
        var names = main.SelectedTab!.ReadAloud.VoiceNames;

        await Assert.That(names.Length).IsEqualTo(SpeechSetup.AllVoices.Length);
        await Assert.That(names).Contains("MeloTTS: Australian, Australian English");
        await Assert.That(names).Contains("Kokoro: Heart, American English, warm");
        await Assert.That(names).Contains("Azure: Ava, American English, expressive");
    }

    /// <summary>Picking a voice picks the engine that speaks it, so the engine is never chosen by itself somewhere else.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PickingAVoicePicksItsEngine()
    {
        using var test = new TestServices(new FallbackPlatform(), new FakeSpeech(true, true));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("engine.pdf", Pages)]);
        var aloud = main.SelectedTab!.ReadAloud;
        var kokoro = Array.FindIndex(aloud.VoiceNames, static name => name.StartsWith(Kokoro, StringComparison.Ordinal));

        aloud.VoiceIndex = kokoro;

        await Assert.That(test.Services.Settings.SpeechEngine).IsEqualTo(SpeechEngineChoice.Kokoro);
        await Assert.That(test.Services.Settings.SpeechVoice).IsEqualTo(SpeechSetup.AllVoices[kokoro].Voice.Id);
        await Assert.That(aloud.VoiceShortName).IsEqualTo($"Kokoro: {SpeechSetup.AllVoices[kokoro].Voice.Name}");
        await Assert.That(aloud.UsesAzure).IsFalse();
    }

    /// <summary>An Azure voice picked before its key is typed opens the settings, where the key goes, and one picked after does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AzureVoiceWithoutAKeyOpensTheSettings()
    {
        using var test = new TestServices(new FallbackPlatform(), new FakeSpeech(true, true));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("azure.pdf", Pages)]);
        var aloud = main.SelectedTab!.ReadAloud;
        var asked = 0;
        using var answering = aloud.ShowSettingsInteraction.RegisterHandler(context =>
        {
            asked++;
            return Signal.Return(RxVoid.Default).Do(context.SetOutput);
        });
        var first = Array.FindIndex(aloud.VoiceNames, static name => name.StartsWith(Azure, StringComparison.Ordinal));

        aloud.VoiceIndex = first;
        var askedWithoutKey = asked;
        aloud.AzureKey = " key ";
        aloud.AzureRegion = " AustraliaEast ";
        aloud.VoiceIndex = first + 1;

        await Assert.That(aloud.UsesAzure).IsTrue();
        await Assert.That(askedWithoutKey).IsEqualTo(1);
        await Assert.That(asked).IsEqualTo(1);
        await Assert.That(test.Services.Settings.AzureSpeechKey).IsEqualTo("key");
        await Assert.That(test.Services.Settings.AzureSpeechRegion).IsEqualTo("australiaeast");
    }

    /// <summary>The page is dimmed away from the sentence being read without Focus Mode ever having been opened, and stops being dimmed when that is turned off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DimmingNeedsNoFocusMode()
    {
        using var test = new TestServices(new FallbackPlatform(), new FakeSpeech(true, true));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("dim.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var dimmed = new FakeMarksPainter();
        var plain = new FakeMarksPainter();

        tab.ReadAloud.DimRest = true;
        tab.ReadAloud.IsOpen = true;
        var marked = await TestWait.UntilAsync(() => tab.ReadAloud.SpokenBounds.Length > 0);
        state.DrawMarks(dimmed, tab, View());
        tab.ReadAloud.DimRest = false;
        state.DrawMarks(plain, tab, View());
        var focusMade = tab.FocusModeIfMade is not null;
        tab.ReadAloud.IsOpen = false;

        await Assert.That(marked).IsTrue();
        await Assert.That(focusMade).IsFalse();
        await Assert.That(dimmed.Dims).IsGreaterThan(0);
        await Assert.That(plain.Dims).IsEqualTo(0);
    }

    /// <summary>Makes the part of the canvas in view.</summary>
    /// <returns>The viewport.</returns>
    private static LayoutRect View() => new(0, 0, ViewWidth, ViewHeight);
}
