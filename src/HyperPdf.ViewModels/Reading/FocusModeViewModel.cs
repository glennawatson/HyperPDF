// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Reading;
using HyperPdf.Core.Settings;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Reading;

/// <summary>
/// Focus Mode for a tab: the document's text in reading order, reflowed into one calm column with the size, spacing,
/// width, colour and typeface the person chooses. Switching between it and the pages keeps the place, Read Aloud is
/// followed and marked, and Read from Here starts reading at a paragraph.
/// </summary>
[DebuggerDisplay("FocusModeViewModel: On={IsOn}")]
public sealed partial class FocusModeViewModel : ReactiveObject, IDisposable
{
    /// <summary>The smallest text size.</summary>
    private const double MinFontSize = 12;

    /// <summary>The largest text size.</summary>
    private const double MaxFontSize = 32;

    /// <summary>How much Larger and Smaller change the text size.</summary>
    private const double FontStep = 1;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>The subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The page shown at the top of the view.</summary>
    private int _topPage;

    /// <summary>How far down the top page the view is.</summary>
    private double _topFraction;

    /// <summary>The page last marked as being read.</summary>
    private int _markedPage = -1;

    /// <summary>The <see cref="TabReading.TextKey"/> the pages were made for.</summary>
    private long _builtFor = -1;

    /// <summary>Initializes a new instance of the <see cref="FocusModeViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The application services.</param>
    public FocusModeViewModel(DocumentTabViewModel owner, AppServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(services);
        _owner = owner;
        _services = services;

        // The settings seed the properties before anything listens, so loading them neither saves nor reacts.
        var settings = services.Settings;
        FontSize = settings.FocusFontSize;
        LineSpacing = settings.FocusLineSpacing;
        ParagraphSpacing = settings.FocusParagraphSpacing;
        TextWidth = settings.FocusTextWidth;
        PageColour = (int)settings.FocusPageColour;
        Font = (int)settings.FocusFont;
        FocusBand = settings.FocusBand;
        WordHighlight = settings.ReadAloudHighlight == ReadAloudHighlight.SentenceAndWord;

        // What is read aloud is marked as it moves on, and again when the band is turned on or off.
        _subscriptions.Add(Signal.Merge(
                owner.ReadAloud.WhenChanged(static reader => reader.MarksVersion).Skip(1).Select(static _ => RxVoid.Default),
                this.WhenChanged(static x => x.FocusBand).Skip(1).Select(static _ => RxVoid.Default))
            .SubscribeSafe(_ => MarkSpoken(), ErrorTrace.Write));

        // Opening shows the place being viewed; closing returns to the pages at the place read.
        var switches = this.WhenChanged(static x => x.IsOn).Skip(1);
        _subscriptions.Add(switches.Where(static on => on).SubscribeSafe(_ => Open(), ErrorTrace.Write));
        _subscriptions.Add(switches.Where(static on => !on).SubscribeSafe(_ => ReturnToPages(), ErrorTrace.Write));

        // Text that changes while Focus Mode is open (a reload, recognised text, moved pages) is shown at once.
        _subscriptions.Add(Signal.Merge(
                owner.Reading.WhenChanged(static reading => reading.Version).Skip(1),
                owner.WhenChanged(static tab => tab.LayoutVersion).Skip(1))
            .WithLatestFrom(this.WhenChanged(static x => x.IsOn), static (_, on) => on)
            .Where(static on => on)
            .SubscribeSafe(_ => Open(), ErrorTrace.Write));

        // Each setting is saved as it changes; the setters have already kept it in range.
        _subscriptions.Add(this.WhenChanged(static x => x.FontSize).Skip(1)
            .SubscribeSafe(size => Save(size, static (saved, value) => saved.FocusFontSize = value), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.LineSpacing).Skip(1)
            .SubscribeSafe(spacing => Save(spacing, static (saved, value) => saved.FocusLineSpacing = value), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.ParagraphSpacing).Skip(1)
            .SubscribeSafe(spacing => Save(spacing, static (saved, value) => saved.FocusParagraphSpacing = value), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.TextWidth).Skip(1)
            .SubscribeSafe(width => Save(width, static (saved, value) => saved.FocusTextWidth = (int)value), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.PageColour).Skip(1)
            .SubscribeSafe(colour => Save(colour, static (saved, value) => saved.FocusPageColour = (FocusPageColour)value), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.Font).Skip(1)
            .SubscribeSafe(font => Save(font, static (saved, value) => saved.FocusFont = (FocusFont)value), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.FocusBand).Skip(1)
            .SubscribeSafe(on => Save(on, static (saved, value) => saved.FocusBand = value), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.WordHighlight).Skip(1)
            .SubscribeSafe(
                on => Save(on, static (saved, value) => saved.ReadAloudHighlight = value ? ReadAloudHighlight.SentenceAndWord : ReadAloudHighlight.Sentence),
                ErrorTrace.Write));
    }

    /// <summary>Gets the page colour choices.</summary>
    public static IReadOnlyList<string> PageColourOptions { get; } = ["Match the pages", "Soft paper", "Sage", "Calm night", "White"];

    /// <summary>Gets the typeface choices.</summary>
    public static IReadOnlyList<string> FontOptions { get; } = ["Sans serif", "Serif"];

    /// <summary>Gets or sets a value indicating whether Focus Mode is shown instead of the pages.</summary>
    [Reactive]
    public partial bool IsOn { get; set; }

    /// <summary>Gets the pages, created when Focus Mode first opens.</summary>
    [Reactive]
    public partial IReadOnlyList<FocusPageViewModel> Pages { get; private set; } = [];

    /// <summary>Gets the place the view was last asked to bring into view; the view scrolls each time it changes.</summary>
    [Reactive]
    public partial FocusScrollRequest ScrollTarget { get; private set; }

    /// <summary>Gets or sets the text size in points; values outside the range are clamped.</summary>
    public double FontSize
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, MinFontSize, MaxFontSize));
    }

    /// <summary>Gets or sets the line spacing, as a multiple of the text size.</summary>
    [Reactive]
    public partial double LineSpacing { get; set; }

    /// <summary>Gets or sets the space between paragraphs, as a multiple of the text size.</summary>
    [Reactive]
    public partial double ParagraphSpacing { get; set; }

    /// <summary>Gets or sets the text column width, in characters; fractions are rounded.</summary>
    public double TextWidth
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Round(value));
    }

    /// <summary>Gets or sets the page colour's index in <see cref="PageColourOptions"/>; values outside the options are clamped.</summary>
    public int PageColour
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, 0, PageColourOptions.Count - 1));
    }

    /// <summary>Gets or sets the typeface's index in <see cref="FontOptions"/>; values outside the options are clamped.</summary>
    public int Font
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, 0, FontOptions.Count - 1));
    }

    /// <summary>Gets or sets a value indicating whether text away from what is being read aloud is dimmed.</summary>
    [Reactive]
    public partial bool FocusBand { get; set; }

    /// <summary>Gets or sets a value indicating whether the word being read aloud is marked as well as the sentence.</summary>
    [Reactive]
    public partial bool WordHighlight { get; set; }

    /// <summary>Records the place at the top of the view, so switching back to the pages keeps it.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="fraction">How far down the page.</param>
    public void ReportTop(int pageIndex, double fraction)
    {
        _topPage = Math.Max(0, pageIndex);
        _topFraction = Math.Clamp(fraction, 0, 1);
    }

    /// <summary>Asks the view to bring a place into view; a newer request replaces one that has not finished.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="fraction">How far down the page, from 0 to 1.</param>
    public void ScrollTo(int pageIndex, double fraction) => ScrollTarget = ScrollTarget.Next(pageIndex, fraction);

    /// <summary>Starts reading aloud at a block.</summary>
    /// <param name="block">The block.</param>
    [ReactiveCommand]
    public void ReadFromHere(FocusBlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _owner.ReadAloud.StartAt(block.PageIndex, Math.Max(0, block.FirstCharacter));
    }

    /// <summary>Gets the share of a page's height a box's top is at.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="top">The box's top in points.</param>
    /// <returns>From 0 to 1.</returns>
    public double FractionOf(int pageIndex, float top)
    {
        var sizes = _owner.Source.PageSizes;
        return pageIndex >= 0 && pageIndex < sizes.Length && sizes[pageIndex].Height > 0 ? Math.Clamp(top / sizes[pageIndex].Height, 0, 1) : 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Creates the pages if needed and brings the place being viewed into view.</summary>
    private void Open()
    {
        // Pages keep what they loaded, so they are made again whenever the text may differ: after a reload, a change of
        // pages or text recognition, as well as when the page count changes.
        var key = _owner.Reading.TextKey;
        if (Pages.Count != _owner.PageCount || _builtFor != key)
        {
            var pages = new FocusPageViewModel[_owner.PageCount];
            for (var i = 0; i < pages.Length; i++)
            {
                pages[i] = new(i, $"Page {i + 1}", _owner.GetReadingDocument);
            }

            _builtFor = key;
            Pages = pages;
        }

        var position = _owner.Position;
        ReportTop(position.PageIndex, position.OffsetFraction);
        ScrollTo(position.PageIndex, position.OffsetFraction);
    }

    /// <summary>Shows the pages again at the place that was at the top of Focus Mode.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReturnToPages() => _owner.NavigateTo(_topPage, null, _topFraction);

    /// <summary>Marks what is being read aloud, and follows it to a new page.</summary>
    private void MarkSpoken()
    {
        var reader = _owner.ReadAloud;
        if (_markedPage >= 0 && _markedPage < Pages.Count && _markedPage != reader.SpokenPage)
        {
            Pages[_markedPage].Mark(TextRange.None, TextRange.None, false);
        }

        _markedPage = reader.SpokenPage;
        if (_markedPage < 0 || _markedPage >= Pages.Count)
        {
            return;
        }

        var page = Pages[_markedPage];
        page.Mark(reader.SpokenRange, reader.SpokenWord, FocusBand);
        if (IsOn && reader.IsPlaying && _markedPage != _topPage)
        {
            ScrollTo(_markedPage, 0);
        }
    }

    /// <summary>Writes one setting and saves the settings.</summary>
    /// <typeparam name="T">The setting's type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="write">Writes the value into the settings.</param>
    private void Save<T>(T value, Action<AppSettings, T> write)
    {
        write(_services.Settings, value);
        _services.SaveSettings();
    }

    /// <summary>Switches Focus Mode on or off.</summary>
    /// <returns>Whether it is now on.</returns>
    [ReactiveCommand]
    private bool Toggle() => IsOn = !IsOn;

    /// <summary>Makes the text larger.</summary>
    /// <returns>The new size.</returns>
    [ReactiveCommand]
    private double Larger() => FontSize = Math.Min(MaxFontSize, FontSize + FontStep);

    /// <summary>Makes the text smaller.</summary>
    /// <returns>The new size.</returns>
    [ReactiveCommand]
    private double Smaller() => FontSize = Math.Max(MinFontSize, FontSize - FontStep);
}
