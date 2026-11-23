// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reactive;
using HyperPdf.Core.Reading;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Speech;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Reading;

/// <summary>
/// Reads a tab's text aloud a sentence at a time, preparing the next sentence while the current one plays so there are
/// no gaps. The sentence being read is softly marked on the page and the view follows it from page to page. Pausing
/// keeps the place; Previous and Next move a sentence at a time.
/// </summary>
[DebuggerDisplay("ReadAloudViewModel: Open={IsOpen}, Playing={IsPlaying}, Page={SpokenPage}")]
public sealed partial class ReadAloudViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>Bytes in a megabyte.</summary>
    private const double BytesPerMegabyte = 1024 * 1024;

    /// <summary>The most documents whose reading position is remembered.</summary>
    private const int MaxRememberedPositions = 200;

    /// <summary>The index of normal speed in <see cref="SpeedValues"/>.</summary>
    private const int NormalSpeedIndex = 2;

    /// <summary>How far from a click to look for the text to start reading from, in points.</summary>
    private const float ReadFromTolerance = 24F;

    /// <summary>How often the word mark moves.</summary>
    private static readonly TimeSpan WordInterval = TimeSpan.FromMilliseconds(90);

    /// <summary>The speeds offered.</summary>
    private static readonly double[] SpeedValues = [0.8, 0.9, 1, 1.1, 1.25, 1.5];

    /// <summary>The voices offered, in the order of <see cref="VoiceNames"/>: every voice of every engine.</summary>
    private static readonly SpeechVoiceChoice[] Offered = SpeechSetup.AllVoices;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>The subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Emits whether the voice is not downloading; enables Download Voice.</summary>
    private readonly IObservable<bool> _canDownloadVoice;

    /// <summary>The sentences of the page being read.</summary>
    private readonly List<SpeechSentence> _sentences = [];

    /// <summary>The text of the page being read, in reading order.</summary>
    private string _pageText = string.Empty;

    /// <summary>The page character index of each character of <see cref="_pageText"/>; -1 for added spaces.</summary>
    private int[] _map = [];

    /// <summary>The page whose sentences are loaded, or -1.</summary>
    private int _loadedPage = -1;

    /// <summary>The sentence being read on <see cref="SpokenPage"/>.</summary>
    private int _sentence;

    /// <summary>Whether the voices or speed are being set up, rather than chosen.</summary>
    private bool _listing;

    /// <summary>The page reading should start from, or -1 for the current page.</summary>
    private int _startPage = -1;

    /// <summary>The character reading should start from once the page is loaded; <see cref="int.MaxValue"/> means the last sentence.</summary>
    private int _startChar;

    /// <summary>Whether the next read, once it begins, stops at the end of its first page.</summary>
    private bool _pageOnlyNext;

    /// <summary>Whether the read in progress stops at the end of the page it began on.</summary>
    private bool _pageOnly;

    /// <summary>
    /// Stops the reading or download in progress, or <see langword="null"/> when none runs. The work owns and disposes its
    /// cancellation source; this is the source's Cancel, so <see cref="IsCurrentWork"/> can tell which work it stops.
    /// </summary>
    private Action? _stopWork;

    /// <summary>Whether reading is being cancelled only to start again.</summary>
    private bool _restarting;

    /// <summary>The resource identity of the displayed StatusText.</summary>
    private LocalizedMessage _localizedStatusText = LocalizedMessage.Literal(string.Empty);

    /// <summary>Initializes a new instance of the <see cref="ReadAloudViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The application services.</param>
    public ReadAloudViewModel(DocumentTabViewModel owner, AppServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(services);
        _owner = owner;
        _services = services;
        SpeedNames = BuildSpeedNames();
        VoiceNames = BuildVoiceNames();
        _listing = true;
        VoiceIndex = ChosenVoice();
        SpeedIndex = NearestSpeed(services.Settings.SpeechSpeed);
        _listing = false;

        _canDownloadVoice = this.WhenChanged(static vm => vm.IsDownloading).Select(static busy => !busy);
        _playPauseTextHelper = this.WhenChanged(static vm => vm.IsPlaying).Select(
            static playing => playing
                ? UiText
            .Get("Pause")
                : UiText.Get("Play")).ToProperty(this, static vm => vm.PlayPauseText);

        _subscriptions.Add(_playPauseTextHelper);
        _subscriptions.Add(Scope.Create(CancelWork));

        // Opening the bar starts reading from the current page; closing it stops and clears the mark.
        var openings = this.WhenChanged(static vm => vm.IsOpen).Skip(1);
        _subscriptions.Add(openings.Where(static open => open).SubscribeSafe(_ => Begin(), ErrorTrace.Write));
        _subscriptions.Add(openings.Where(static open => !open).SubscribeSafe(_ => Stop(), ErrorTrace.Write));

        // A voice or speed the reader picks is remembered and used at once; setting up the lists picks nothing. The filter
        // reads the view model's own flag: a constructor cannot hand 'this' to a state-passing operator.
        _subscriptions.Add(
            Signal.Merge(this.WhenChanged(static vm => vm.VoiceIndex).Skip(1), this.WhenChanged(static vm => vm.SpeedIndex).Skip(1))
            .Where(_ => !_listing).SubscribeSafe(_ => UseChosenVoiceAndSpeed(), ErrorTrace.Write));

        // Each move of the page, sentence or word being read moves the marks version on, which the views follow.
        _subscriptions.Add(
            Signal.Merge(
                this.WhenChanged(static vm => vm.SpokenPage).Skip(1).AsSignal(),
                this.WhenChanged(
                    static vm
                    => vm.SpokenRange).Skip(1).AsSignal(),
                this.WhenChanged(static vm => vm.SpokenWord).Skip(1)
                .AsSignal()).SubscribeSafe(_ => MarksVersion++, ErrorTrace.Write));
    }

    /// <summary>Gets or sets a value indicating whether the Read Aloud bar is shown. Opening starts reading from the current page.</summary>
    [Reactive]
    public partial bool IsOpen { get; set; }

    /// <summary>Gets a value indicating whether audio is being read.</summary>
    [Reactive]
    public partial bool IsPlaying { get; private set; }

    /// <summary>Gets a value indicating whether the voice must be downloaded or set up before reading.</summary>
    [Reactive]
    public partial bool NeedsVoice { get; private set; }

    /// <summary>Gets a value indicating whether the on-device voice can be downloaded from the bar.</summary>
    [Reactive]
    public partial bool CanDownloadVoice { get; private set; }

    /// <summary>Gets a value indicating whether the voice is downloading.</summary>
    [Reactive]
    public partial bool IsDownloading { get; private set; }

    /// <summary>Gets the download progress from 0 to 1.</summary>
    [Reactive]
    public partial double DownloadProgress { get; private set; }

    /// <summary>Gets the steady status, for example "Reading page 3 of 12" or "Paused".</summary>
    public string StatusText { get => _localizedStatusText.Value ?? string.Empty; private set => SetStatusText(LocalizedMessage.Literal(value)); }

    /// <summary>Gets the label of the play button: "Pause" while reading, otherwise "Play".</summary>
    [ObservableAsProperty]
    public partial string PlayPauseText { get; }

    /// <summary>Gets the question that shows the window of the reading aloud settings, which the window answers when it is closed.</summary>
    public Interaction<ReadAloudViewModel, RxVoid> ShowSettingsInteraction { get; } = new();

    /// <summary>Gets the names of every voice of every engine, for example "Kokoro: Heart, American English, warm".</summary>
    public string[] VoiceNames { get; }

    /// <summary>Gets the full name of the voice chosen, as the list of voices words it.</summary>
    public string VoiceName => (uint)VoiceIndex < (uint)VoiceNames.Length ? VoiceNames[VoiceIndex] : string.Empty;

    /// <summary>Gets the short name of the voice chosen, such as "Kokoro: Heart", for the button that opens the list of voices.</summary>
    public string VoiceShortName => (uint)VoiceIndex < (uint)Offered.Length ? $"{Offered[VoiceIndex].EngineName}: {Offered[VoiceIndex].Voice.Name}" : string.Empty;

    /// <summary>Gets the name of the speed chosen, for the button that opens the list of speeds.</summary>
    public string SpeedName => (uint)SpeedIndex < (uint)SpeedNames.Length ? SpeedNames[SpeedIndex] : string.Empty;

    /// <summary>Gets a value indicating whether the voice chosen is spoken by Azure, which needs a key and a region.</summary>
    public bool UsesAzure => _services.Settings.SpeechEngine == SpeechEngineChoice.Azure;

    /// <summary>
    /// Gets or sets a value indicating whether the page is dimmed away from the sentence being read. It is the same setting
    /// Focus Mode offers, kept here too so it can be turned on without opening Focus Mode.
    /// </summary>
    public bool DimRest
    {
        get => _services.Settings.FocusBand;
        set
        {
            if (_services.Settings.FocusBand == value)
            {
                return;
            }

            _services.Settings.FocusBand = value;
            _services.SaveSettings();
            this.RaisePropertyChanged();

            // Focus Mode shows the same switch, so it is told when it has been made.
            if (_owner.FocusModeIfMade is { } focus)
            {
                focus.FocusBand = value;
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether each word is marked as it is read, as well as its sentence.</summary>
    public bool MarkWords
    {
        get => _services.Settings.ReadAloudHighlight == ReadAloudHighlight.SentenceAndWord;
        set
        {
            if (MarkWords == value)
            {
                return;
            }

            _services.Settings.ReadAloudHighlight = value ? ReadAloudHighlight.SentenceAndWord : ReadAloudHighlight.Sentence;
            _services.SaveSettings();
            this.RaisePropertyChanged();
            if (_owner.FocusModeIfMade is { } focus)
            {
                focus.WordHighlight = value;
            }
        }
    }

    /// <summary>Gets or sets the key of the person's Azure Speech resource.</summary>
    public string AzureKey
    {
        get => _services.Settings.AzureSpeechKey;
        set
        {
            _services.Settings.AzureSpeechKey = value.Trim();
            _services.SaveSettings();
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets or sets the region of the person's Azure Speech resource, such as australiaeast.</summary>
    public string AzureRegion
    {
        get => _services.Settings.AzureSpeechRegion;
        set
        {
            _services.Settings.AzureSpeechRegion = value.Trim().ToLowerInvariant();
            _services.SaveSettings();
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets or sets the chosen voice's index in <see cref="VoiceNames"/>.</summary>
    [Reactive]
    public partial int VoiceIndex { get; set; }

    /// <summary>Gets the speeds' names.</summary>
    public string[] SpeedNames { get; }

    /// <summary>Gets or sets the chosen speed's index in <see cref="SpeedNames"/>.</summary>
    [Reactive]
    public partial int SpeedIndex { get; set; }

    /// <summary>Gets the page of the sentence being read, or -1.</summary>
    [Reactive]
    public partial int SpokenPage { get; private set; } = -1;

    /// <summary>Gets a number that changes each time the page, sentence or word being read changes, so views mark it again.</summary>
    [Reactive]
    public partial int MarksVersion { get; private set; }

    /// <summary>Gets the sentence being read, as a range of the page's reading text, for Focus Mode.</summary>
    [Reactive]
    public partial TextRange SpokenRange { get; private set; } = TextRange.None;

    /// <summary>Gets the word being read, roughly, when word highlighting is on.</summary>
    [Reactive]
    public partial TextRange SpokenWord { get; private set; } = TextRange.None;

    /// <summary>Gets the rectangles of the word being read, in page space.</summary>
    [Reactive]
    public partial PageRect[] SpokenWordBounds { get; private set; } = [];

    /// <summary>Gets the rectangles of the sentence being read, in page space.</summary>
    [Reactive]
    public partial PageRect[] SpokenBounds { get; private set; } = [];

    /// <summary>Gets a value indicating whether an Azure voice is chosen and its key or region has not been typed yet.</summary>
    private bool LacksAzureKey => UsesAzure && (_services.Settings.AzureSpeechKey.Length == 0 || _services.Settings.AzureSpeechRegion.Length == 0);

    /// <summary>Gets the speed chosen.</summary>
    private float Speed => (float)SpeedValues[Math.Clamp(SpeedIndex, 0, SpeedValues.Length - 1)];

    /// <summary>Starts reading at a character on a page, opening the bar.</summary>
    /// <param name="page">The page.</param>
    /// <param name="charIndex">The character to start from; reading begins with the sentence holding it.</param>
    public void StartAt(int page, int charIndex)
    {
        _startPage = Math.Max(0, page);
        _startChar = Math.Max(0, charIndex);
        if (IsOpen)
        {
            Begin();
        }
        else
        {
            IsOpen = true;
        }
    }

    /// <summary>Pauses reading, keeping the place.</summary>
    public void Pause()
    {
        CancelWork();
        IsPlaying = false;
        _services.SaveSettings();
        if (IsOpen && !NeedsVoice)
        {
            SetStatusText(LocalizedMessage.Get(UiText, "Paused"));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Sets the displayed StatusText while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetStatusText(LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedStatusText, message, nameof(StatusText));

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(StatusText));
        LocalisationRefresh.RefreshCore(_owner, visited);
        LocalisationRefresh.RefreshCore(_services, visited);
    }

    /// <summary>Describes a size in megabytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>For example "95 MB".</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static string Megabytes(long bytes) => UiText.Format("{0:0} MB", Math.Max(1, Math.Round(bytes / BytesPerMegabyte)));

    /// <summary>Finds the offered speed nearest a remembered one.</summary>
    /// <param name="speed">The speed.</param>
    /// <returns>The index in <see cref="SpeedValues"/>.</returns>
    internal static int NearestSpeed(double speed)
    {
        var nearest = NormalSpeedIndex;
        for (var i = 0; i < SpeedValues.Length; i++)
        {
            if (Math.Abs(SpeedValues[i] - speed) < Math.Abs(SpeedValues[nearest] - speed))
            {
                nearest = i;
            }
        }

        return nearest;
    }

    /// <summary>Gets a page's text in reading order, or as the engine gives it when the layout is unknown.</summary>
    /// <param name="document">The document.</param>
    /// <param name="reading">The reading order, if available.</param>
    /// <param name="page">The page.</param>
    /// <returns>The text and the page character index of each of its characters.</returns>
    internal static (string Text, int[] Map) LoadText(IDocument document, ReadingDocument? reading, int page)
    {
        if (reading is not null)
        {
            var flattened = ReadingDocument.Flatten(reading.GetPage(page), out var map);
            return (flattened, map);
        }

        var text = document.GetText(page, 0, document.GetCharacterCount(page));
        var identity = new int[text.Length];
        for (var i = 0; i < identity.Length; i++)
        {
            identity[i] = i;
        }

        return (text, identity);
    }

    /// <summary>Finds where a page character is in the reading text: the same character, or the nearest one read.</summary>
    /// <param name="map">The reading text's page character indices.</param>
    /// <param name="charIndex">The page character.</param>
    /// <returns>The offset in the reading text.</returns>
    internal static int ReadingOffset(int[] map, int charIndex)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < map.Length && bestDistance > 0; i++)
        {
            var distance = map[i] < 0 ? int.MaxValue : Math.Abs(map[i] - charIndex);
            if (distance >= bestDistance)
            {
                continue;
            }

            best = i;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>Finds the word a share of the way through a sentence.</summary>
    /// <param name="text">The page's reading text.</param>
    /// <param name="sentence">The sentence.</param>
    /// <param name="fraction">How far through it, from 0 to 1.</param>
    /// <returns>The word.</returns>
    internal static TextRange WordAt(string text, SpeechSentence sentence, double fraction)
    {
        var end = Math.Min(text.Length, sentence.Start + sentence.Length);
        var at = Math.Clamp(sentence.Start + (int)(fraction * sentence.Length), sentence.Start, Math.Max(sentence.Start, end - 1));
        while (at < end && char.IsWhiteSpace(text[at]))
        {
            at++;
        }

        var start = at;
        while (start > sentence.Start && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        var stop = at;
        while (stop < end && !char.IsWhiteSpace(text[stop]))
        {
            stop++;
        }

        return stop > start ? new(start, stop - start) : TextRange.None;
    }

    /// <summary>Finds the sentence holding a character.</summary>
    /// <param name="sentences">The sentences.</param>
    /// <param name="charIndex">The character.</param>
    /// <returns>The sentence index; the last sentence starting before the character.</returns>
    internal static int SentenceAt(List<SpeechSentence> sentences, int charIndex)
    {
        var found = 0;
        for (var i = 0; i < sentences.Count && sentences[i].Start <= charIndex; i++)
        {
            found = i;
        }

        return found;
    }

    /// <summary>Names every voice offered, with the engine that speaks it first.</summary>
    /// <returns>The names, in the order of <see cref="Offered"/>.</returns>
    private static string[] BuildVoiceNames()
    {
        var names = new string[Offered.Length];
        for (var i = 0; i < Offered.Length; i++)
        {
            names[i] = $"{Offered[i].EngineName}: {Offered[i].Voice.Name}, {Offered[i].Voice.Description}";
        }

        return names;
    }

    /// <summary>Names the speeds offered.</summary>
    /// <returns>For example "Normal speed" and "1.25× speed".</returns>
    private static string[] BuildSpeedNames()
    {
        var names = new string[SpeedValues.Length];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = i == NormalSpeedIndex ? UiText.Get("Normal speed") : UiText.Format("{0:0.##}× speed", SpeedValues[i]);
        }

        return names;
    }

    /// <summary>Forgets documents that have gone; if that is not enough, starts the list afresh.</summary>
    /// <param name="positions">The remembered positions.</param>
    private static void Prune(Dictionary<string, ReadingPosition> positions)
    {
        var gone = new List<string>();
        foreach (var path in positions.Keys)
        {
            if (!File.Exists(path))
            {
                gone.Add(path);
            }
        }

        foreach (var path in gone)
        {
            _ = positions.Remove(path);
        }

        if (positions.Count >= MaxRememberedPositions)
        {
            positions.Clear();
        }
    }

    /// <summary>Starts reading from the requested place, or the top of the current page.</summary>
    private void Begin()
    {
        Restart();
        RefreshVoices();
        _pageOnly = _pageOnlyNext;
        _pageOnlyNext = false;
        SpokenPage = _startPage >= 0 ? _startPage : Math.Max(0, _owner.CurrentPageIndex);
        if (_startPage < 0)
        {
            _startChar = 0;
        }

        if (_startPage < 0 && _services.Settings.ReadingPositions.TryGetValue(_owner.FilePath, out var resume) && resume.Page == SpokenPage)
        {
            // Carry on where reading stopped last time, when that is the page in view.
            _startChar = resume.Character;
        }

        _startPage = -1;
        _loadedPage = -1;
        _sentence = 0;
        Play();
    }

    /// <summary>Reads aloud from the sentence nearest a point, or the top of the page when there is no text near it.</summary>
    /// <param name="location">The page and point.</param>
    [ReactiveCommand]
    private void ReadFromPoint(PageLocation location)
    {
        var index = _owner.TryGetDocument()?.GetCharacterIndexAt(location.Page, location.Point, ReadFromTolerance) ?? -1;
        StartAt(location.Page, Math.Max(0, index));
    }

    /// <summary>Reads aloud from a character, such as the start of the selection.</summary>
    /// <param name="start">The character to start from.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadFromCharacter(CharacterPosition start) => StartAt(start.Page, start.Char);

    /// <summary>Reads the page in view, from its top, and stops at its end.</summary>
    [ReactiveCommand]
    private void ReadThisPage()
    {
        _pageOnlyNext = true;
        StartAt(Math.Max(0, _owner.CurrentPageIndex), 0);
    }

    /// <summary>Reads from the page in view to the end of the document.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadToEnd() => StartAt(Math.Max(0, _owner.CurrentPageIndex), 0);

    /// <summary>Plays or pauses; with the bar closed there is nothing to play or pause.</summary>
    [ReactiveCommand]
    private void PlayPause()
    {
        if (!IsOpen)
        {
            return;
        }

        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    /// <summary>Moves to the next sentence.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Next() => Step(1);

    /// <summary>Moves back a sentence.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Previous() => Step(-1);

    /// <summary>Chooses a voice by its name in <see cref="VoiceNames"/>, as a list of voices does.</summary>
    /// <param name="name">The voice's name.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ChooseVoice(string name) => VoiceIndex = Math.Max(0, Array.IndexOf(VoiceNames, name));

    /// <summary>Chooses a speed by its name in <see cref="SpeedNames"/>, as a list of speeds does.</summary>
    /// <param name="name">The speed's name.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ChooseSpeed(string name) => SpeedIndex = Math.Max(0, Array.IndexOf(SpeedNames, name));

    /// <summary>Turns the dimming of the page away from the sentence being read on or off.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ToggleDimRest() => DimRest = !DimRest;

    /// <summary>Shows the reading aloud settings: the voice, the speed, what is marked on the page and the Azure key. They apply as they are changed.</summary>
    /// <returns>A signal that ends when the window is closed.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> ShowSettings() => ShowSettingsInteraction.Handle(this).ToSignal();

    /// <summary>Opens or closes Read Aloud.</summary>
    /// <returns>Whether the bar is now open.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Toggle() => IsOpen = !IsOpen;

    /// <summary>Moves a sentence forwards or back, carrying on reading if it was.</summary>
    /// <param name="delta">+1 or -1.</param>
    private void Step(int delta)
    {
        var wasPlaying = IsPlaying;
        CancelWork();
        if (_loadedPage >= 0)
        {
            var target = _sentence + delta;
            if (target < 0 && SpokenPage > 0)
            {
                SpokenPage--;
                _loadedPage = -1;
                _startChar = int.MaxValue;
            }
            else if (target >= _sentences.Count && SpokenPage < _owner.PageCount - 1)
            {
                SpokenPage++;
                _loadedPage = -1;
                _startChar = 0;
            }
            else
            {
                _sentence = Math.Clamp(target, 0, Math.Max(0, _sentences.Count - 1));
                Highlight();
            }
        }

        if (wasPlaying)
        {
            Play();
        }
    }

    /// <summary>Stops reading and clears the mark.</summary>
    private void Stop()
    {
        CancelWork();
        IsPlaying = false;
        SpokenBounds = [];
        SpokenRange = TextRange.None;
        SpokenPage = -1;
        _loadedPage = -1;
        SetStatusText(LocalizedMessage.Literal(string.Empty));
        _services.SaveSettings();
    }

    /// <summary>Closes the bar.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => IsOpen = false;

    /// <summary>Starts the reading loop from the current place.</summary>
    private void Play()
    {
        if (LacksAzureKey)
        {
            // An Azure voice cannot start without its key, so the place the key is typed is opened and not only named.
            AskForAzureKey();
            return;
        }

        Restart();
        var work = new CancellationTokenSource();
        _stopWork = work.Cancel;
        _services.ClaimSpeech(this);
        _subscriptions.Add(Read(work).SubscribeSafe(static _ => { }, ErrorTrace.Write));
    }

    /// <summary>Cancels the reading in progress because new reading is about to start, so the bar does not show reading as stopped in between.</summary>
    private void Restart()
    {
        _restarting = true;
        try
        {
            CancelWork();
        }
        finally
        {
            _restarting = false;
        }
    }

    /// <summary>Cancels the reading or download in progress.</summary>
    private void CancelWork()
    {
        var stop = _stopWork;
        _stopWork = null;
        stop?.Invoke();
    }

    /// <summary>Determines whether a piece of work is the one in progress.</summary>
    /// <param name="work">The work's cancellation source.</param>
    /// <returns><see langword="true"/> when stopping now would stop this work.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsCurrentWork(CancellationTokenSource work) => ReferenceEquals(_stopWork?.Target, work);

    /// <summary>
    /// Remembers the chosen voice and speed, and carries on with them. The voice brings its engine with it. An Azure voice
    /// with no key yet opens the settings, where the key is typed, and reading starts once there is one.
    /// </summary>
    private void UseChosenVoiceAndSpeed()
    {
        var settings = _services.Settings;
        settings.SpeechSpeed = SpeedValues[Math.Clamp(SpeedIndex, 0, SpeedValues.Length - 1)];
        if ((uint)VoiceIndex < (uint)Offered.Length)
        {
            settings.SpeechEngine = Offered[VoiceIndex].Engine;
            settings.SpeechVoice = Offered[VoiceIndex].Voice.Id;
        }

        _services.SaveSettings();
        this.RaisePropertyChanged(nameof(VoiceName));
        this.RaisePropertyChanged(nameof(SpeedName));
        this.RaisePropertyChanged(nameof(UsesAzure));
        if (LacksAzureKey)
        {
            AskForAzureKey();
            return;
        }

        // A voice picked while the bar waits for a voice is tried at once, as one picked while reading is.
        if (IsOpen && (IsPlaying || NeedsVoice))
        {
            Play();
        }
    }

    /// <summary>
    /// Stops, says what the Azure voice needs and opens the settings where its key and region are typed. Reading starts when
    /// that window closes with both typed.
    /// </summary>
    private void AskForAzureKey()
    {
        CancelWork();
        SetPlaying(false);
        SetStatusText(LocalizedMessage.Get(UiText, "Enter your Azure Speech key and region in the Read Aloud settings, or choose a voice that runs on this computer."));
        _subscriptions.Add(ShowSettingsInteraction.Handle(this).ToSignal().SubscribeSafe(_ => PlayIfReady(), ErrorTrace.Write));
    }

    /// <summary>Starts reading when the bar is open and the Azure voice chosen now has its key, as after the settings window closes.</summary>
    private void PlayIfReady()
    {
        if (IsOpen && !LacksAzureKey)
        {
            Play();
        }
    }

    /// <summary>Finds the voice the settings remember: that voice of that engine, or the engine's first voice when the voice is not one it has.</summary>
    /// <returns>The index in <see cref="Offered"/>.</returns>
    private int ChosenVoice()
    {
        var settings = _services.Settings;
        var first = -1;
        for (var i = 0; i < Offered.Length; i++)
        {
            if (Offered[i].Engine != settings.SpeechEngine)
            {
                continue;
            }

            if (Offered[i].Voice.Id == settings.SpeechVoice)
            {
                return i;
            }

            first = first < 0 ? i : first;
        }

        return Math.Max(0, first);
    }

    /// <summary>Shows the voice the settings remember as the one chosen, when something else changed it, without that counting as a choice.</summary>
    private void RefreshVoices()
    {
        var chosen = ChosenVoice();
        if (chosen == VoiceIndex)
        {
            return;
        }

        _listing = true;
        VoiceIndex = chosen;
        _listing = false;
        this.RaisePropertyChanged(nameof(VoiceName));
    }

    /// <summary>Gets the voice an engine speaks with: the one remembered when the engine has it, otherwise the engine's first.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The voice's identifier.</returns>
    private string VoiceOf(ISpeechEngine engine)
    {
        var wanted = _services.Settings.SpeechVoice;
        foreach (var voice in engine.Voices)
        {
            if (voice.Id == wanted)
            {
                return wanted;
            }
        }

        return engine.Voices[0].Id;
    }

    /// <summary>Checks that the voice and sound output are ready, explaining what is needed when they are not.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns><see langword="true"/> when reading can start.</returns>
    private bool CheckReady(ISpeechEngine engine)
    {
        if (!engine.IsReady)
        {
            var onDevice = _services.Settings.SpeechEngine != SpeechEngineChoice.Azure;
            NeedsVoice = true;
            CanDownloadVoice = onDevice;
            SetStatusText(
                onDevice
                    ? LocalizedMessage.Concat(
                        LocalizedMessage.Format(
                            UiText,
                            "The natural voice runs on this computer and needs a one-time download of about {0}. ",
                            Megabytes(_services.Speech.MissingBytesFor(_services.Settings))),
                        LocalizedMessage.Get(UiText, "What you read is never sent anywhere."))
                    : LocalizedMessage.Get(UiText, "Enter your Azure Speech key and region in the Read Aloud settings, or choose a voice that runs on this computer."));
            return false;
        }

        NeedsVoice = false;
        CanDownloadVoice = false;
        if (!_services.Audio.IsAvailable)
        {
            SetStatusText(LocalizedMessage.Get(UiText, "No sound output was found. Reading aloud needs PipeWire or PulseAudio."));
            return false;
        }

        return true;
    }

    /// <summary>Reads until the end of the document, a pause or a stop, explaining any failure in the bar.</summary>
    /// <param name="work">Stops the reading.</param>
    /// <returns>A cold signal that ends when reading ends; unsubscribing, because reading was paused or stopped, keeps the place.</returns>
    private IObservable<RxVoid> Read(CancellationTokenSource work)
    {
        var engine = _services.GetSpeechEngine();
        if (!CheckReady(engine) || _owner.TryGetDocument() is not { } document)
        {
            SetPlaying(false);
            return Signal.Return(RxVoid.Default);
        }

        SetPlaying(true);

        // Pausing or stopping cancels the token, which unsubscribes the voice and the sound output straight away.
        return ReadSentences(document, engine, work.Token)
            .Select(_ => FinishedReading())
            .Catch<RxVoid, Exception>(ReadingStopped)
            .TakeUntil(work.Token)
            .Finally(() => EndRead(work));
    }

    /// <summary>Says reading reached the end of the document.</summary>
    /// <returns>The completion value.</returns>
    private RxVoid FinishedReading()
    {
        SetStatusText(LocalizedMessage.Get(UiText, "Finished reading."));
        SpokenBounds = [];
        return RxVoid.Default;
    }

    /// <summary>Says why reading stopped, or passes on a failure that is not expected.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that ends for a pause, a stop or an expected failure, or fails with the same error.</returns>
    private IObservable<RxVoid> ReadingStopped(Exception error)
    {
        if (error is OperationCanceledException)
        {
            // Paused or stopped: the place is kept.
            return Signal.Return(RxVoid.Default);
        }

        if (error is not (HttpRequestException or InvalidOperationException or IOException))
        {
            return Signal.Throw<RxVoid>(error);
        }

        SetStatusText(LocalizedMessage.Format(UiText, "Reading aloud stopped: {0}", error.Message));
        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Shows reading has ended, unless newer work has taken over.</summary>
    /// <param name="work">The reading that ended.</param>
    private void EndRead(CancellationTokenSource work)
    {
        if (IsCurrentWork(work))
        {
            _stopWork = null;
            SetPlaying(false);
        }
        else if (_stopWork is null && !_restarting)
        {
            SetPlaying(false);
        }

        work.Dispose();
    }

    /// <summary>Reads sentence after sentence, preparing the next while the current one plays.</summary>
    /// <param name="document">The document.</param>
    /// <param name="engine">The engine.</param>
    /// <param name="token">Stops the reading.</param>
    /// <returns>A cold signal that gives one value at the end of the document.</returns>
    private IObservable<RxVoid> ReadSentences(IDocument document, ISpeechEngine engine, CancellationToken token)
    {
        var voice = VoiceOf(engine);
        var speed = Speed;
        return Signal.Using(
            static () => new SpeechPrefetch(),
            prefetch => LazySequence.Concat(Sentences(document, engine, voice, speed, prefetch, token))
                .TakeWhile(static more => more)
                .Count()
                .Select(static _ => RxVoid.Default));
    }

    /// <summary>Makes the reading of each sentence, one at a time as the caller moves along, for as long as the caller asks.</summary>
    /// <param name="document">The document.</param>
    /// <param name="engine">The engine.</param>
    /// <param name="voice">The voice.</param>
    /// <param name="speed">The speed.</param>
    /// <param name="prefetch">The next sentence's audio.</param>
    /// <param name="token">Stops the reading.</param>
    /// <returns>A cold signal per sentence, giving <see langword="false"/> at the end of the document.</returns>
    private IEnumerable<IObservable<bool>> Sentences(IDocument document, ISpeechEngine engine, string voice, float speed, SpeechPrefetch prefetch, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            yield return Signal.Defer(() => LoadPage(document, token)
                .SwitchMap(more => more ? Speak(document, engine, voice, speed, prefetch, token) : Signal.Return(false)));
        }

        // Reading was paused or stopped, which is not the end of the document.
        yield return Signal.Throw<bool>(new OperationCanceledException(token));
    }

    /// <summary>Speaks the current sentence, with the next one prepared while it plays.</summary>
    /// <param name="document">The document.</param>
    /// <param name="engine">The engine.</param>
    /// <param name="voice">The voice.</param>
    /// <param name="speed">The speed.</param>
    /// <param name="prefetch">The next sentence's audio.</param>
    /// <param name="token">Stops the reading.</param>
    /// <returns>A cold signal giving <see langword="true"/> once the sentence is spoken.</returns>
    private IObservable<bool> Speak(IDocument document, ISpeechEngine engine, string voice, float speed, SpeechPrefetch prefetch, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        SetStatusText(LocalizedMessage.Format(UiText, "Reading page {0} of {1}", SpokenPage + 1, document.PageCount));

        return (prefetch.Prepared ?? engine.Synthesize(SentenceText(_sentence), voice, speed))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SwitchMap(audio => PlayPrepared(engine, voice, speed, prefetch, audio, token))
            .Select(_ => NextSentence(token));
    }

    /// <summary>Starts preparing the next sentence, marks the current one and plays it.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="voice">The voice.</param>
    /// <param name="speed">The speed.</param>
    /// <param name="prefetch">The next sentence's audio.</param>
    /// <param name="audio">The current sentence's audio.</param>
    /// <param name="token">Stops the reading.</param>
    /// <returns>A cold signal that ends once the sentence has played.</returns>
    private IObservable<RxVoid> PlayPrepared(ISpeechEngine engine, string voice, float speed, SpeechPrefetch prefetch, SpeechAudio audio, CancellationToken token)
    {
        if (_sentence + 1 < _sentences.Count)
        {
            prefetch.Begin(engine.Synthesize(SentenceText(_sentence + 1), voice, speed));
        }
        else
        {
            prefetch.Clear();
        }

        token.ThrowIfCancellationRequested();
        Highlight();
        return PlayTrackingWords(audio);
    }

    /// <summary>Moves on to the next sentence.</summary>
    /// <param name="token">Stops the reading.</param>
    /// <returns><see langword="true"/>, because reading goes on.</returns>
    private bool NextSentence(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _sentence++;
        return true;
    }

    /// <summary>Plays a sentence, moving the word mark along with it when word highlighting is on.</summary>
    /// <param name="clip">The sentence's audio.</param>
    /// <returns>A cold signal that ends once the sentence has played; unsubscribing stops the sound.</returns>
    /// <remarks>The voice gives no word timings, so the mark moves in proportion to the characters spoken.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> PlayTrackingWords(SpeechAudio clip) =>
        Signal.Defer(() =>
        {
            var tracking = TrackWords(clip);
            return _services.Audio.Play(clip)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Do(_ => EndTracking(tracking))
                .Finally(() => EndTracking(tracking));
        });

    /// <summary>Starts moving the word mark along with a sentence, when word highlighting is on.</summary>
    /// <param name="clip">The sentence's audio.</param>
    /// <returns>What stops the marking, or <see langword="null"/> when words are not marked.</returns>
    private IDisposable? TrackWords(SpeechAudio clip)
    {
        if (_services.Settings.ReadAloudHighlight != ReadAloudHighlight.SentenceAndWord || clip.Duration <= TimeSpan.Zero || _sentence >= _sentences.Count)
        {
            return null;
        }

        var sentence = _sentences[_sentence];
        var started = Stopwatch.GetTimestamp();
        var duration = clip.Duration;
        MarkWord(sentence, Math.Clamp(Stopwatch.GetElapsedTime(started) / duration, 0, 1));
        return Signal.Every(WordInterval, TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => MarkWord(sentence, Math.Clamp(Stopwatch.GetElapsedTime(started) / duration, 0, 1)), ErrorTrace.Write);
    }

    /// <summary>Stops marking words and clears the word mark.</summary>
    /// <param name="tracking">What stops the marking, or <see langword="null"/>.</param>
    private void EndTracking(IDisposable? tracking)
    {
        tracking?.Dispose();
        SpokenWord = TextRange.None;
        SpokenWordBounds = [];
    }

    /// <summary>Marks the word a share of the way through a sentence.</summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="fraction">How far through it, from 0 to 1.</param>
    private void MarkWord(SpeechSentence sentence, double fraction)
    {
        var word = WordAt(_pageText, sentence, fraction);
        if (word == SpokenWord || _owner.TryGetDocument() is not { } document)
        {
            return;
        }

        SpokenWord = word;
        var rects = new List<PageRect>();
        var runs = new List<ReadingCharacterRun>();
        ReadingDocument.GetRuns(_map, word.Start, word.Length, runs);
        foreach (var (start, count) in runs)
        {
            document.GetTextBounds(SpokenPage, start, count, rects);
        }

        SpokenWordBounds = [..rects];
    }

    /// <summary>Remembers where reading got to in this document, so it carries on there next time.</summary>
    private void RememberPosition()
    {
        if (_sentence >= _sentences.Count || _map.Length == 0)
        {
            return;
        }

        var start = _sentences[_sentence].Start;
        var character = 0;
        for (var i = start; i < _map.Length && character == 0; i++)
        {
            character = Math.Max(0, _map[i]);
        }

        var positions = _services.Settings.ReadingPositions;
        if (positions.Count >= MaxRememberedPositions && !positions.ContainsKey(_owner.FilePath))
        {
            Prune(positions);
        }

        positions[_owner.FilePath] = new(SpokenPage, character);
    }

    /// <summary>Shows whether reading is under way.</summary>
    /// <param name="playing">Whether reading.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetPlaying(bool playing) => IsPlaying = playing;

    /// <summary>Loads the sentences of <see cref="SpokenPage"/>, moving past pages without text.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels page preparation and extraction.</param>
    /// <returns>A cold signal giving <see langword="false"/> at the end of the document.</returns>
    private IObservable<bool> LoadPage(IDocument document, CancellationToken cancellationToken)
    {
        var state = new PageLoadState();
        return LazySequence.Concat(PageLoads(document, state, cancellationToken)).Count().Select(_ => state.More);
    }

    /// <summary>Makes the load of each page that has to be read before a sentence can be, one at a time as the caller moves along.</summary>
    /// <param name="document">The document.</param>
    /// <param name="state">Whether a page with sentences was found.</param>
    /// <param name="cancellationToken">Cancels page preparation and extraction.</param>
    /// <returns>A cold signal per page.</returns>
    private IEnumerable<IObservable<RxVoid>> PageLoads(IDocument document, PageLoadState state, CancellationToken cancellationToken)
    {
        while (_loadedPage != SpokenPage || _sentence >= _sentences.Count)
        {
            if (_loadedPage == SpokenPage)
            {
                // Finished this page.
                if (_pageOnly)
                {
                    state.More = false;
                    yield break;
                }

                SpokenPage++;
                _startChar = 0;
            }

            if (SpokenPage >= document.PageCount)
            {
                SpokenPage = document.PageCount - 1;
                _loadedPage = -1;
                state.More = false;
                yield break;
            }

            yield return LoadPageText(document, SpokenPage, cancellationToken);
        }
    }

    /// <summary>Prepares a page, reads its text on the pool and splits it into sentences.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">Cancels page preparation and extraction.</param>
    /// <returns>A cold signal that ends once the page's sentences are ready.</returns>
    private IObservable<RxVoid> LoadPageText(IDocument document, int page, CancellationToken cancellationToken)
    {
        var reading = _owner.GetReadingDocument();

        return document.PreparePage(page).ThrowWhenCancelled(cancellationToken)
            .SwitchMap(_ => Signal.Start(() => LoadText(document, reading, page), TaskPoolSequencer.Default))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(loaded =>
            {
                // A load that was already running when a newer read started must not overwrite the newer read's page, sentences and start.
                cancellationToken.ThrowIfCancellationRequested();
                (_pageText, _map) = loaded;
                return PageLoaded(page);
            });
    }

    /// <summary>Splits the page's text into sentences and goes to the sentence reading starts at.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The completion value.</returns>
    private RxVoid PageLoaded(int page)
    {
        _sentences.Clear();
        SentenceSplitter.Split(_pageText, _sentences);
        _loadedPage = page;
        _sentence = _startChar == int.MaxValue ? Math.Max(0, _sentences.Count - 1) : SentenceAt(_sentences, ReadingOffset(_map, _startChar));
        _startChar = 0;
        if (_sentences.Count > 0 && page != _owner.CurrentPageIndex)
        {
            _owner.ShowPage(page);
        }

        return RxVoid.Default;
    }

    /// <summary>Gets a sentence's text, prepared for the voice.</summary>
    /// <param name="index">The sentence.</param>
    /// <returns>The text.</returns>
    private string SentenceText(int index)
    {
        var sentence = _sentences[index];
        return SentenceSplitter.ToSpeech(_pageText.AsSpan(sentence.Start, sentence.Length));
    }

    /// <summary>Marks the sentence being read.</summary>
    private void Highlight()
    {
        if (_owner.TryGetDocument() is not { } document || _sentence >= _sentences.Count)
        {
            SpokenBounds = [];
            SpokenRange = TextRange.None;
            return;
        }

        var sentence = _sentences[_sentence];
        SpokenRange = new(sentence.Start, sentence.Length);
        RememberPosition();
        var rects = new List<PageRect>();
        var runs = new List<ReadingCharacterRun>();
        ReadingDocument.GetRuns(_map, sentence.Start, sentence.Length, runs);
        foreach (var (start, count) in runs)
        {
            document.GetTextBounds(SpokenPage, start, count, rects);
        }

        SpokenBounds = [..rects];
    }

    /// <summary>Downloads the on-device voice, then starts reading.</summary>
    /// <returns>A signal that ends once the voice is downloaded, and reading started, or the download failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canDownloadVoice))]
    private IObservable<RxVoid> DownloadVoice()
    {
        CancelWork();
        var work = new CancellationTokenSource();
        _stopWork = work.Cancel;
        IsDownloading = true;
        DownloadProgress = 0;
        SetStatusText(LocalizedMessage.Get(UiText, "Downloading the voice…"));
        var progress = new Progress<double>(fraction => DownloadProgress = fraction);

        return _services.Speech.DownloadVoice(_services.Settings, progress)
            .ThrowWhenCancelled(work.Token)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(_ => VoiceDownloaded())
            .Catch<RxVoid, Exception>(VoiceDownloadFailed)
            .Finally(() => EndDownload(work));
    }

    /// <summary>Starts reading once the voice is on this computer.</summary>
    /// <returns>The completion value.</returns>
    private RxVoid VoiceDownloaded()
    {
        IsDownloading = false;
        NeedsVoice = false;
        CanDownloadVoice = false;
        if (IsOpen)
        {
            Play();
        }

        return RxVoid.Default;
    }

    /// <summary>Says why the voice was not downloaded, or passes on a failure that is not expected.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that ends for an expected failure, or fails with the same error.</returns>
    private IObservable<RxVoid> VoiceDownloadFailed(Exception error)
    {
        switch (error)
        {
            case OperationCanceledException:
                {
                    SetStatusText(LocalizedMessage.Get(UiText, "The download stopped. Download Voice carries on where it left off."));
                    break;
                }

            case HttpRequestException:
                {
                    SetStatusText(LocalizedMessage.Format(UiText, "The voice could not be downloaded: {0}", error.Message));
                    break;
                }

            case IOException:
                {
                    SetStatusText(LocalizedMessage.Format(UiText, "The voice could not be saved: {0}", error.Message));
                    break;
                }

            default:
                {
                    return Signal.Throw<RxVoid>(error);
                }
        }

        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Shows the download has ended, unless newer work has taken over.</summary>
    /// <param name="work">The download that ended.</param>
    private void EndDownload(CancellationTokenSource work)
    {
        IsDownloading = false;
        if (IsCurrentWork(work))
        {
            _stopWork = null;
        }

        work.Dispose();
    }
}
