// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="ReadAloudViewModel"/> with a fake voice.</summary>
public sealed class ReadAloudViewModelTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The fastest speed's index.</summary>
    private const int FastestSpeed = 5;

    /// <summary>The fastest speed.</summary>
    private const float Fastest = 1.5F;

    /// <summary>How close two speeds must be to count as the same.</summary>
    private const float SpeedTolerance = 0.01F;

    /// <summary>Halfway through a sentence.</summary>
    private const double Middle = 0.5;

    /// <summary>A speed between two offered ones.</summary>
    private const double BetweenSpeeds = 1.3;

    /// <summary>The index of 1.25× speed.</summary>
    private const int BriskSpeed = 4;

    /// <summary>The index of normal speed.</summary>
    private const int NormalSpeed = 2;

    /// <summary>The length of each test sentence.</summary>
    private const int SentenceLength = 10;

    /// <summary>The start of the second test sentence.</summary>
    private const int SecondStart = 11;

    /// <summary>The start of the third test sentence.</summary>
    private const int ThirdStart = 22;

    /// <summary>A character inside the second test sentence.</summary>
    private const int InsideSecond = 15;

    /// <summary>The bytes in 95 MB.</summary>
    private const long NinetyFiveMegabytes = 95L << 20;

    /// <summary>The status once the document has been read.</summary>
    private const string Finished = "Finished reading.";

    /// <summary>Opening Read Aloud reads every sentence of every page in order, then says it has finished.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsTheWholeDocument()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("read.pdf", Pages)]);
        var reader = main.SelectedTab!.ReadAloud;

        reader.IsOpen = true;

        await Assert.That(await UiWait.UntilAsync(() => reader.StatusText == Finished)).IsTrue();
        var spoken = test.Speech.Spoken.ToArray();
        await Assert.That(spoken.Length).IsGreaterThanOrEqualTo(Pages);
        await Assert.That(spoken[0].Text).StartsWith("Page 1");
        await Assert.That(Array.Exists(spoken, static s => s.Text.Contains(TestPdf.Sentence, StringComparison.Ordinal))).IsTrue();
        await Assert.That(Array.Exists(spoken, static s => s.Text.StartsWith("Page 2", StringComparison.Ordinal))).IsTrue();
        await Assert.That(test.Speech.Played).IsEqualTo(spoken.Length);
        await Assert.That(reader.IsPlaying).IsFalse();
        await Assert.That(reader.SpokenBounds.Count).IsEqualTo(0);
    }

    /// <summary>While a sentence plays it is marked on its page; pausing keeps the place and closing clears the mark.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksPausesAndCloses()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new Services.FallbackPlatform(), speech);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("mark.pdf", Pages)]);
        var reader = main.SelectedTab!.ReadAloud;

        reader.IsOpen = true;
        await Assert.That(await UiWait.UntilAsync(() => reader.SpokenBounds.Count > 0)).IsTrue();
        await Assert.That(reader.SpokenPage).IsEqualTo(0);
        await Assert.That(reader.IsPlaying).IsTrue();
        await Assert.That(reader.PlayPauseText).IsEqualTo("Pause");

        _ = await reader.PlayPauseCommand.Execute().ToTask();
        await Assert.That(await UiWait.UntilAsync(() => !reader.IsPlaying)).IsTrue();
        await Assert.That(reader.StatusText).IsEqualTo("Paused");
        await Assert.That(reader.SpokenBounds.Count).IsGreaterThan(0);

        reader.IsOpen = false;
        await Assert.That(reader.SpokenBounds.Count).IsEqualTo(0);
        await Assert.That(reader.SpokenPage).IsEqualTo(-1);
    }

    /// <summary>Changing the speed or voice is used for the next sentence and remembered.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemembersVoiceAndSpeed()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("speed.pdf", 1)]);
        var reader = main.SelectedTab!.ReadAloud;

        reader.SpeedIndex = FastestSpeed;
        reader.VoiceIndex = 1;
        reader.IsOpen = true;

        await Assert.That(await UiWait.UntilAsync(() => reader.StatusText == Finished)).IsTrue();
        var first = test.Speech.Spoken.ToArray()[0];
        await Assert.That(first.Speed).IsEqualTo(Fastest);
        await Assert.That(first.Voice).IsEqualTo("fake_two");
        await Assert.That(test.Services.Settings.SpeechVoice).IsEqualTo("fake_two");
        await Assert.That(test.Services.Settings.SpeechSpeed).IsEqualTo((double)Fastest);
    }

    /// <summary>Without the voice, the bar asks before downloading it, then reads once it has.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsksBeforeDownloadingTheVoice()
    {
        var speech = new FakeSpeech(false, false);
        using var test = new TestServices(new Services.FallbackPlatform(), speech);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("voice.pdf", 1)]);
        var reader = main.SelectedTab!.ReadAloud;

        reader.IsOpen = true;

        await Assert.That(await UiWait.UntilAsync(() => reader.NeedsVoice)).IsTrue();
        await Assert.That(reader.CanDownloadVoice).IsTrue();
        await Assert.That(reader.StatusText).Contains("one-time download");
        await Assert.That(speech.Downloads).IsEqualTo(0);
        await Assert.That(speech.Spoken.IsEmpty).IsTrue();

        _ = await reader.DownloadVoiceCommand.Execute().ToTask();

        await Assert.That(await UiWait.UntilAsync(() => reader.StatusText == Finished)).IsTrue();
        await Assert.That(speech.Downloads).IsEqualTo(1);
        await Assert.That(reader.NeedsVoice).IsFalse();
        await Assert.That(speech.Spoken.IsEmpty).IsFalse();
    }

    /// <summary>Only one tab reads at a time: starting another pauses the first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OneTabReadsAtATime()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new Services.FallbackPlatform(), speech);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("first.pdf", 1), test.CreateDocument("second.pdf", 1)]);
        await main.Tabs[0].EnsureLoadedAsync(CancellationToken.None);
        var first = main.Tabs[0].ReadAloud;
        var second = main.Tabs[1].ReadAloud;

        first.IsOpen = true;
        await Assert.That(await UiWait.UntilAsync(() => first.IsPlaying)).IsTrue();
        second.IsOpen = true;

        await Assert.That(await UiWait.UntilAsync(() => !first.IsPlaying && second.IsPlaying)).IsTrue();
        second.IsOpen = false;
    }

    /// <summary>Reading resumes at the sentence it stopped on, the next time Read Aloud opens on that page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResumesWhereItStopped()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new Services.FallbackPlatform(), speech);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("resume.pdf", Pages)]);
        var reader = main.SelectedTab!.ReadAloud;
        reader.IsOpen = true;
        await Assert.That(await UiWait.UntilAsync(() => !reader.SpokenRange.IsEmpty)).IsTrue();
        var first = reader.SpokenRange;

        _ = await reader.NextCommand.Execute().ToTask();
        await Assert.That(await UiWait.UntilAsync(() => reader.SpokenRange != first && !reader.SpokenRange.IsEmpty)).IsTrue();
        var second = reader.SpokenRange;
        reader.IsOpen = false;
        reader.IsOpen = true;

        await Assert.That(await UiWait.UntilAsync(() => !reader.SpokenRange.IsEmpty)).IsTrue();
        await Assert.That(reader.SpokenRange).IsEqualTo(second);
        reader.IsOpen = false;
    }

    /// <summary>Changing the speed while reading carries on from the same sentence at the new speed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangingSpeedKeepsTheSentence()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new Services.FallbackPlatform(), speech);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("speed-change.pdf", Pages)]);
        var reader = main.SelectedTab!.ReadAloud;
        reader.IsOpen = true;
        await Assert.That(await UiWait.UntilAsync(() => !reader.SpokenRange.IsEmpty)).IsTrue();
        var sentence = reader.SpokenRange;
        var spokenBefore = speech.Spoken.ToArray()[0].Text;

        reader.SpeedIndex = FastestSpeed;

        await Assert.That(await UiWait.UntilAsync(() => speech.Spoken.ToArray().Any(static s => s.Speed > Fastest - SpeedTolerance))).IsTrue();
        var fast = speech.Spoken.ToArray().First(static s => s.Speed > Fastest - SpeedTolerance);
        await Assert.That(fast.Text).IsEqualTo(spokenBefore);
        await Assert.That(reader.SpokenRange).IsEqualTo(sentence);
        reader.IsOpen = false;
    }

    /// <summary>With word marking on, the word being read is marked on the page as the sentence plays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksTheWord()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new Services.FallbackPlatform(), speech);
        test.Services.Settings.ReadAloudHighlight = Core.Settings.ReadAloudHighlight.SentenceAndWord;
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("words.pdf", 1)]);
        var reader = main.SelectedTab!.ReadAloud;

        reader.IsOpen = true;

        await Assert.That(await UiWait.UntilAsync(() => !reader.SpokenWord.IsEmpty && reader.SpokenWordBounds.Count > 0)).IsTrue();
        await Assert.That(reader.SpokenWord.Start).IsGreaterThanOrEqualTo(reader.SpokenRange.Start);
        await Assert.That(reader.SpokenWord.End).IsLessThanOrEqualTo(reader.SpokenRange.End);
        reader.IsOpen = false;
    }

    /// <summary>The word a share of the way through a sentence is found, even at the edges.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsWords()
    {
        const string Text = "The quick brown fox.";
        var sentence = new Core.Speech.SpeechSentence(0, Text.Length);

        await Assert.That(Text.Substring(ReadAloudViewModel.WordAt(Text, sentence, 0).Start, ReadAloudViewModel.WordAt(Text, sentence, 0).Length)).IsEqualTo("The");
        await Assert.That(Text.Substring(ReadAloudViewModel.WordAt(Text, sentence, Middle).Start, ReadAloudViewModel.WordAt(Text, sentence, Middle).Length)).IsEqualTo("brown");
        await Assert.That(Text.Substring(ReadAloudViewModel.WordAt(Text, sentence, 1).Start, ReadAloudViewModel.WordAt(Text, sentence, 1).Length)).IsEqualTo("fox.");
    }

    /// <summary>Speeds are matched to the nearest offered and sentences found from a character.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsSpeedAndSentence()
    {
        await Assert.That(ReadAloudViewModel.NearestSpeed(BetweenSpeeds)).IsEqualTo(BriskSpeed);
        await Assert.That(ReadAloudViewModel.NearestSpeed(1)).IsEqualTo(NormalSpeed);
        List<Core.Speech.SpeechSentence> sentences = [new(0, SentenceLength), new(SecondStart, SentenceLength), new(ThirdStart, SentenceLength)];
        await Assert.That(ReadAloudViewModel.SentenceAt(sentences, InsideSecond)).IsEqualTo(1);
        await Assert.That(ReadAloudViewModel.SentenceAt(sentences, 0)).IsEqualTo(0);
        await Assert.That(ReadAloudViewModel.Megabytes(NinetyFiveMegabytes)).IsEqualTo("95 MB");
    }
}
