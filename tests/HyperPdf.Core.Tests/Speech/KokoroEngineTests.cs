// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Speech.Kokoro;

namespace HyperPdf.Core.Tests.Speech;

/// <summary>Tests for <see cref="KokoroEngine"/> and <see cref="KokoroModel"/> that need no voice files.</summary>
public sealed class KokoroEngineTests
{
    /// <summary>Kokoro's output sample rate.</summary>
    private const int KokoroHertz = 24_000;

    /// <summary>The American voice used in tests.</summary>
    private const string Heart = "af_heart";

    /// <summary>The pad tokens at each end of the input.</summary>
    private const int Padding = 2;

    /// <summary>Double speed, which the stand-in multiplies the tokens by.</summary>
    private const float Double = 2F;

    /// <summary>The floats in a voice pack: 510 rows of 256.</summary>
    private const int VoicePackFloats = 510 * 256;

    /// <summary>The word the stand-in dictionary knows.</summary>
    private const string Greeting = "Hello";

    /// <summary>The engines made and disposed while calls run.</summary>
    private const int DisposeRounds = 20;

    /// <summary>The words in the long text, enough to keep the phonemizer busy for a few milliseconds.</summary>
    private const int LongTextWords = 20_000;

    /// <summary>The engine is not ready until its files are installed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsNotReadyWithoutFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kokoro-{Guid.NewGuid():N}");
        using var engine = new KokoroEngine(directory);

        await Assert.That(engine.IsReady).IsFalse();
        await Assert.That(engine.Voices.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Runs the whole on-device path against a stand-in model whose waveform is the padded token ids times the speed:
    /// dictionary, phonemes, tokens, voice style, ONNX Runtime and the output tensor.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpeaksThroughOnnxRuntime()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kokoro-{Guid.NewGuid():N}");
        try
        {
            InstallStandIn(directory);
            using var engine = new KokoroEngine(directory);
            List<long> tokens = [];
            KokoroVocabulary.Tokenize(engine.Phonemize(Greeting, Heart), tokens);

            var audio = await engine.SynthesizeAsync(Greeting, Heart, Double, CancellationToken.None);

            await Assert.That(engine.IsReady).IsTrue();
            await Assert.That(tokens.Count).IsGreaterThan(0);
            await Assert.That(audio.SampleRate).IsEqualTo(KokoroHertz);
            await Assert.That(audio.Samples.Length).IsEqualTo(tokens.Count + Padding);
            await Assert.That(audio.Samples[0]).IsEqualTo(0F);
            await Assert.That(audio.Samples[1]).IsEqualTo(tokens[0] * Double);
            await Assert.That(audio.Samples[^1]).IsEqualTo(0F);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>The ONNX session is freed when the engine is disposed while another call holds the gate, so it never outlives the engine.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposeFreesTheSessionWhileAnotherCallRuns(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kokoro-{Guid.NewGuid():N}");
        try
        {
            InstallStandIn(directory);
            var leaked = 0;
            var longText = string.Join(' ', Enumerable.Repeat(Greeting, LongTextWords));
            for (var round = 0; round < DisposeRounds; round++)
            {
                var engine = new KokoroEngine(directory);
                _ = await engine.SynthesizeAsync(Greeting, Heart, 1F, cancellationToken);
                var phonemize = Task.Run(() => PhonemizeIfOpen(engine, longText), cancellationToken);
                var synthesize = Task.Run(() => SynthesizeIfOpenAsync(engine, cancellationToken), cancellationToken);

                // The long text keeps the gate held while the engine is disposed.
                await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
                var dispose = Task.Run(engine.Dispose, cancellationToken);
                await Task.WhenAll(phonemize, synthesize, dispose);
                if (engine.IsSessionLoaded)
                {
                    leaked++;
                }
            }

            await Assert.That(leaked).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Every voice has a file to download and the model's files are all listed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsEveryFile()
    {
        foreach (var voice in KokoroModel.Voices)
        {
            var name = KokoroModel.VoiceFile(voice.Id);
            var listed = false;
            foreach (var file in KokoroModel.Files)
            {
                listed |= file.LocalName == name;
            }

            await Assert.That(listed).IsTrue();
        }

        await Assert.That(KokoroModel.SampleRate).IsEqualTo(KokoroHertz);
    }

    /// <summary>British voices are recognised from their id.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosesTheAccent()
    {
        await Assert.That(KokoroModel.IsBritish("bf_emma")).IsTrue();
        await Assert.That(KokoroModel.IsBritish(Heart)).IsFalse();
        await Assert.That(KokoroModel.LexiconFiles(true)[0]).IsEqualTo("gb_gold.json");
    }

    /// <summary>Phonemizes, unless the engine was disposed first.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="text">The text.</param>
    private static void PhonemizeIfOpen(KokoroEngine engine, string text)
    {
        try
        {
            _ = engine.Phonemize(text, Heart);
        }
        catch (ObjectDisposedException)
        {
            // The engine was disposed before the call took the gate.
        }
    }

    /// <summary>Speaks, unless the engine was disposed first.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    private static async Task SynthesizeIfOpenAsync(KokoroEngine engine, CancellationToken cancellationToken)
    {
        try
        {
            _ = await engine.SynthesizeAsync(Greeting, Heart, 1F, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            // The engine was disposed before the call took the gate.
        }
    }

    /// <summary>Lays out the voice folder with the stand-in model, silent voice packs and a tiny dictionary.</summary>
    /// <param name="directory">The folder.</param>
    private static void InstallStandIn(string directory)
    {
        _ = Directory.CreateDirectory(Path.Combine(directory, "voices"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Speech", "Fixtures", "kokoro-stand-in.onnx"), Path.Combine(directory, KokoroModel.ModelFile));
        var silence = new byte[VoicePackFloats * sizeof(float)];
        foreach (var voice in KokoroModel.Voices)
        {
            File.WriteAllBytes(Path.Combine(directory, KokoroModel.VoiceFile(voice.Id)), silence);
        }

        foreach (var british in (bool[])[false, true])
        {
            var files = KokoroModel.LexiconFiles(british);
            File.WriteAllText(Path.Combine(directory, files[0]), """{"hello": "həlˈO"}""");
            File.WriteAllText(Path.Combine(directory, files[1]), "{}");
        }
    }
}
