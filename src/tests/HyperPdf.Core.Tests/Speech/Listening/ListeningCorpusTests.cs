// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdf.Core.Speech;
using HyperPdf.Core.Tests.Speech.Melo;
using HyperPdf.Speech.English;
using HyperPdf.Speech.Kokoro;
using HyperPdf.Speech.Melo;

namespace HyperPdf.Core.Tests.Speech.Listening;

/// <summary>
/// Checks Read Aloud against the listening corpus: written English is said the way a person would say it, the
/// voice knows the words of real passages, and a long session stays even in loudness, pace and pauses.
/// </summary>
[NotInParallel(nameof(KokoroRealModelTests))]
public sealed class ListeningCorpusTests : IDisposable
{
    /// <summary>The Kokoro voice used for the long session.</summary>
    private const string Voice = "af_heart";

    /// <summary>The MeloTTS voice used for the long session: Australian, the default.</summary>
    private const string MeloVoice = "EN-AU";

    /// <summary>The most words in a passage the voice may have to spell out letter by letter.</summary>
    private const int MaxGuessedWords = 2;

    /// <summary>The largest spread of sentence loudness, in decibels, that still sounds even.</summary>
    private const double MaxLevelSpread = 3;

    /// <summary>The largest spread of speaking rate, as a share of the average, that still sounds even.</summary>
    private const double MaxRateSpread = 0.2;

    /// <summary>The longest silence allowed inside a sentence, in seconds; longer sounds like a fault.</summary>
    private const double MaxGap = 1;

    /// <summary>The longest silence allowed before or after a sentence, in seconds.</summary>
    private const double MaxEdgeSilence = 0.8;

    /// <summary>The shortest pause between sentences, in seconds; shorter runs sentences together.</summary>
    private const double MinPause = 0.4;

    /// <summary>The longest pause between sentences, in seconds; longer breaks the flow.</summary>
    private const double MaxPause = 1.2;

    /// <summary>The loudest sample allowed; above full scale the sound clips.</summary>
    private const float MaxPeak = 1F;

    /// <summary>The slowest pace allowed for synthesis, as seconds of work per second of speech.</summary>
    private const double MaxRealTimeFactor = 1;

    /// <summary>How much slower the second half of a session may synthesize than the first.</summary>
    private const double MaxSlowdown = 1.5;

    /// <summary>Splits a session into its first and second halves.</summary>
    private const int Halves = 2;

    /// <summary>The variable naming a folder to save the session's audio in, for listening by ear.</summary>
    private const string SaveVariable = "HYPERPDF_LISTENING_DIR";

    /// <summary>The hold on the voices, so no other test process synthesizes while this test does.</summary>
    private IDisposable? _voices;

    /// <summary>Waits until no other test process is synthesizing, then holds the voices for this test.</summary>
    /// <returns>A task.</returns>
    [Before(Test)]
    public async Task HoldVoicesAsync() => _voices = await RealTimeGate.EnterAsync();

    /// <inheritdoc/>
    public void Dispose() => _voices?.Dispose();

    /// <summary>Written English is turned into the words a reader would say.</summary>
    /// <param name="item">The case.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(ListeningCorpus), nameof(ListeningCorpus.AllCases))]
    public async Task SaysItAsAPersonWould(ListeningCase item)
    {
        var spoken = TextNormalizer.Normalize(item.Text, item.British);
        var missing = item.Expect.Where(e => !spoken.Contains(e, StringComparison.Ordinal)).ToList();
        var present = (item.Forbid ?? []).Where(f => spoken.Contains(f, StringComparison.Ordinal)).ToList();

        await Assert.That(string.Join(" | ", missing)).IsEqualTo(string.Empty).Because($"said as \"{spoken}\"");
        await Assert.That(string.Join(" | ", present)).IsEqualTo(string.Empty).Because($"said as \"{spoken}\"");
    }

    /// <summary>The voice knows nearly every word of each passage, so it never spells ordinary words out.</summary>
    /// <param name="passage">The passage.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(ListeningCorpus), nameof(ListeningCorpus.AllPassages))]
    public async Task KnowsTheWords(ListeningPassage passage)
    {
        await KokoroModelFixture.EnsureAsync();
        foreach (var british in (bool[])[false, true])
        {
            var files = KokoroModel.LexiconFiles(british);
            var dictionaries = files.Select(static f => File.ReadAllBytes(Path.Combine(KokoroModelFixture.Directory, f))).ToArray();
            var phonemizer = new EnglishPhonemizer(PronunciationLexicon.Load(dictionaries), british) { RecordsGuesses = true };

            _ = phonemizer.Phonemize(TextNormalizer.Normalize(passage.Text, british));

            await Assert.That(phonemizer.GuessedWords.Count).IsLessThanOrEqualTo(MaxGuessedWords)
                .Because($"{(british ? "British" : "American")} voice spells out: {string.Join(", ", phonemizer.GuessedWords)}");
        }
    }

    /// <summary>
    /// Every passage, read sentence by sentence by the real voice, is clean: no clipping, no stray gaps and no long
    /// silences around sentences.
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(ListeningCorpus), nameof(ListeningCorpus.AllPassages))]
    public async Task ReadsCleanly(ListeningPassage passage)
    {
        await KokoroModelFixture.EnsureAsync();
        using var engine = new KokoroEngine(KokoroModelFixture.Directory);
        await AssertReadsCleanly(engine, Voice, passage);
    }

    /// <summary>Every passage, read sentence by sentence by the MeloTTS voice, is clean.</summary>
    /// <param name="passage">The passage.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(ListeningCorpus), nameof(ListeningCorpus.AllPassages))]
    public async Task MeloReadsCleanly(ListeningPassage passage)
    {
        await MeloModelFixture.EnsureAsync();
        using var engine = new MeloEngine(MeloModelFixture.Directory);
        await AssertReadsCleanly(engine, MeloVoice, passage);
    }

    /// <summary>MeloTTS knows nearly every word of each passage, so it rarely has to guess.</summary>
    /// <param name="passage">The passage.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(ListeningCorpus), nameof(ListeningCorpus.AllPassages))]
    public async Task MeloKnowsTheWords(ListeningPassage passage)
    {
        await MeloModelFixture.EnsureAsync();
        var frontEnd = MeloFrontEnd.Load(MeloModelFixture.Directory);

        frontEnd.Prepare(MeloEngine.Normalize(passage.Text, true), new());

        await Assert.That(frontEnd.GuessedWords).IsLessThanOrEqualTo(MaxGuessedWords);
    }

    /// <summary>
    /// A long session stays restful: loudness and pace stay even from the first sentence to the last, and synthesis
    /// keeps ahead of playback without slowing down as it goes.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongSessionStaysEven()
    {
        await KokoroModelFixture.EnsureAsync();
        using var engine = new KokoroEngine(KokoroModelFixture.Directory);
        await AssertStaysEven(engine, Voice, "long-session.wav");
    }

    /// <summary>A long session read by the MeloTTS voice stays restful.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeloLongSessionStaysEven()
    {
        await MeloModelFixture.EnsureAsync();
        using var engine = new MeloEngine(MeloModelFixture.Directory);
        await AssertStaysEven(engine, MeloVoice, "long-session-melo.wav");
    }

    /// <summary>Checks a passage reads cleanly: no clipping, no stray gaps and no long silences around sentences.</summary>
    /// <param name="engine">The voice.</param>
    /// <param name="voice">The voice id.</param>
    /// <param name="passage">The passage.</param>
    /// <returns>A task.</returns>
    private static async Task AssertReadsCleanly(ISpeechEngine engine, string voice, ListeningPassage passage)
    {
        var session = await ReadAsync(engine, voice, passage.Text);
        var report = string.Join(Environment.NewLine, session.Sentences.Select(static s => $"{s.Measurement}  {s.Text}"));

        await Assert.That(session.Sentences.TrueForAll(static s => s.Measurement.IsFinite)).IsTrue();
        await Assert.That(session.Sentences.Max(static s => s.Measurement.Peak)).IsLessThanOrEqualTo(MaxPeak).Because(report);
        await Assert.That(session.Sentences.Max(static s => s.Measurement.LongestGap)).IsLessThanOrEqualTo(MaxGap).Because(report);
        await Assert.That(session.Sentences.Max(static s => Math.Max(s.Measurement.LeadingSilence, s.Measurement.TrailingSilence))).IsLessThanOrEqualTo(MaxEdgeSilence).Because(report);
    }

    /// <summary>Checks a long session stays even in loudness, pace and pauses, and keeps ahead of playback.</summary>
    /// <param name="engine">The voice.</param>
    /// <param name="voice">The voice id.</param>
    /// <param name="fileName">The file the session is saved as, when saving is asked for.</param>
    /// <returns>A task.</returns>
    private static async Task AssertStaysEven(ISpeechEngine engine, string voice, string fileName)
    {
        var passage = ListeningCorpus.Instance.Passages.Single(static p => p.Category == "long-session");
        var session = await ReadAsync(engine, voice, passage.Text);
        Save(session, fileName);
        var levels = session.Sentences.Select(static s => s.Measurement.Level).ToList();
        var rates = session.Sentences.Select(static s => s.Measurement.Rate).ToList();
        var half = session.Sentences.Count / Halves;
        var firstHalf = RealTimeFactor(session.Sentences.Take(half));
        var secondHalf = RealTimeFactor(session.Sentences.Skip(half));
        var report = string.Create(
            CultureInfo.InvariantCulture,
            $"level spread {Spread(levels):0.00} dB, rate spread {Spread(rates) / rates.Average():P0}, real-time factor {firstHalf:0.00} then {secondHalf:0.00}");

        await Assert.That(Spread(levels)).IsLessThanOrEqualTo(MaxLevelSpread).Because(report);
        await Assert.That(Spread(rates) / rates.Average()).IsLessThanOrEqualTo(MaxRateSpread).Because(report);
        await Assert.That(RealTimeFactor(session.Sentences)).IsLessThan(MaxRealTimeFactor).Because(report);
        await Assert.That(Pauses(session).All(static p => p is >= MinPause and <= MaxPause)).IsTrue().Because(report);
        await Assert.That(secondHalf).IsLessThanOrEqualTo(firstHalf * MaxSlowdown).Because(report);
    }

    /// <summary>Reads a passage sentence by sentence, as Read Aloud does, timing each one.</summary>
    /// <param name="engine">The voice.</param>
    /// <param name="voice">The voice id.</param>
    /// <param name="text">The passage.</param>
    /// <returns>The session.</returns>
    private static async Task<ListeningSession> ReadAsync(ISpeechEngine engine, string voice, string text)
    {
        var sentences = new List<SpeechSentence>();
        SentenceSplitter.Split(text, sentences);
        var spoken = new List<SpokenSentence>(sentences.Count);
        foreach (var sentence in sentences)
        {
            var words = SentenceSplitter.ToSpeech(text.AsSpan(sentence.Start, sentence.Length));
            var started = Stopwatch.GetTimestamp();
            var audio = await engine.SynthesizeAsync(words, voice, 1, CancellationToken.None);
            var work = Stopwatch.GetElapsedTime(started);
            var letters = words.Count(char.IsLetterOrDigit);
            spoken.Add(new(words, audio, SpeechMeasurement.Measure(audio, letters), work));
        }

        return new(spoken);
    }

    /// <summary>Gets the pause between each sentence and the next: one's trailing silence and the next one's leading silence.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The pauses in seconds.</returns>
    private static IEnumerable<double> Pauses(ListeningSession session)
    {
        for (var i = 1; i < session.Sentences.Count; i++)
        {
            yield return session.Sentences[i - 1].Measurement.TrailingSilence + session.Sentences[i].Measurement.LeadingSilence;
        }
    }

    /// <summary>Gets the spread of values: their standard deviation.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The standard deviation.</returns>
    private static double Spread(List<double> values)
    {
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }

    /// <summary>Gets seconds of synthesis per second of speech.</summary>
    /// <param name="sentences">The sentences.</param>
    /// <returns>The factor.</returns>
    private static double RealTimeFactor(IEnumerable<SpokenSentence> sentences)
    {
        var work = 0.0;
        var speech = 0.0;
        foreach (var sentence in sentences)
        {
            work += sentence.Work.TotalSeconds;
            speech += sentence.Audio.Duration.TotalSeconds;
        }

        return speech == 0 ? double.PositiveInfinity : work / speech;
    }

    /// <summary>Saves the session as a WAV file when a folder is named, so it can be listened to.</summary>
    /// <param name="session">The session.</param>
    /// <param name="fileName">The file's name.</param>
    private static void Save(ListeningSession session, string fileName)
    {
        if (Environment.GetEnvironmentVariable(SaveVariable) is not { Length: > 0 } folder)
        {
            return;
        }

        _ = Directory.CreateDirectory(folder);
        var samples = session.Sentences.SelectMany(static s => s.Audio.Samples).ToArray();
        WaveFile.Write(Path.Combine(folder, fileName), samples, session.Sentences[0].Audio.SampleRate);
    }
}
