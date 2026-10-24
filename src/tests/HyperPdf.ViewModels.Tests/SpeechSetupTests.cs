// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;
using HyperPdf.Speech.Kokoro;
using HyperPdf.Speech.Melo;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the engine choice: MeloTTS by default with its Australian voice first, Kokoro when chosen, each with its own files.</summary>
public sealed class SpeechSetupTests
{
    /// <summary>Kokoro's place in the engine list.</summary>
    private const int KokoroIndex = 1;

    /// <summary>Azure's place in the engine list.</summary>
    private const int AzureIndex = 2;

    /// <summary>The engines offered.</summary>
    private const int EngineCount = 3;

    /// <summary>New settings read aloud with MeloTTS, offering the Australian voice first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultsToMeloTts()
    {
        var settings = new AppSettings();
        using var engine = SpeechSetup.CreateEngineFor(settings, Path.GetTempPath());

        await Assert.That(engine).IsTypeOf<MeloEngine>();
        await Assert.That(engine.Voices[0].Id).IsEqualTo("EN-AU");
        await Assert.That(SpeechSetup.FilesFor(settings)).IsEqualTo(MeloModel.Files);
    }

    /// <summary>Choosing Kokoro reads with Kokoro and downloads Kokoro's files.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KokoroCanBeChosen()
    {
        var settings = new AppSettings { SpeechEngine = SpeechEngineChoice.Kokoro };
        using var engine = SpeechSetup.CreateEngineFor(settings, Path.GetTempPath());

        await Assert.That(engine).IsTypeOf<KokoroEngine>();
        await Assert.That(SpeechSetup.FilesFor(settings)).IsEqualTo(KokoroModel.Files);
    }

    /// <summary>Preferences offers MeloTTS, Kokoro and Azure in that order and saves the choice.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreferencesOffersEachEngine()
    {
        using var test = new TestServices();
        var preferences = new PreferencesViewModel(test.Services);

        var first = preferences.SpeechEngine;
        preferences.SpeechEngine = KokoroIndex;
        var kokoro = test.Services.Settings.SpeechEngine;
        preferences.SpeechEngine = AzureIndex;

        await Assert.That(PreferencesViewModel.SpeechEngineOptions.Count).IsEqualTo(EngineCount);
        await Assert.That(first).IsEqualTo(0);
        await Assert.That(kokoro).IsEqualTo(SpeechEngineChoice.Kokoro);
        await Assert.That(preferences.UsesAzure).IsTrue();
    }
}
