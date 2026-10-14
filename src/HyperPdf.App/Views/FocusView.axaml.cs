// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>
/// Focus Mode: the document's text in reading order in one calm, centred column. The settings become resources the
/// styles use, pages are virtualised, and the view reports the place at its top so switching back keeps it.
/// </summary>
[DebuggerDisplay("FocusView: {ViewModel}")]
public sealed partial class FocusView : ReactiveUI.Avalonia.ReactiveUserControl<FocusModeViewModel>
{
    /// <summary>A character's average width as a share of the text size, to turn a width in characters into pixels.</summary>
    private const double CharacterWidth = 0.5;

    /// <summary>A heading's size as a multiple of the text size.</summary>
    private const double HeadingScale = 1.35;

    /// <summary>Captions and footnotes as a multiple of the text size.</summary>
    private const double SmallScale = 0.85;

    /// <summary>The list indent as a multiple of the text size.</summary>
    private const double ItemIndent = 1.2;

    /// <summary>Headings are spaced a little tighter than body text.</summary>
    private const double HeadingSpacingShare = 0.8;

    /// <summary>Half the paragraph space, after headings and between list items.</summary>
    private const double HalfSpace = 0.5;

    /// <summary>The red share of perceived brightness.</summary>
    private const double RedWeight = 0.299;

    /// <summary>The green share of perceived brightness.</summary>
    private const double GreenWeight = 0.587;

    /// <summary>The blue share of perceived brightness.</summary>
    private const double BlueWeight = 0.114;

    /// <summary>The opacity of secondary text.</summary>
    private const byte MutedAlpha = 0xA6;

    /// <summary>The space kept above the place scrolled to.</summary>
    private const double ScrollMargin = 12;

    /// <summary>How many times a scroll aligns the place again after the layout changes under it.</summary>
    private const int MaxAlignAttempts = 6;

    /// <summary>How far the scroll position may move, in pixels, and still count as settled.</summary>
    private const double AlignTolerance = 0.5;

    /// <summary>The luminance above which text on the page is dark.</summary>
    private const double LightPage = 0.55;

    /// <summary>How long a scroll waits for a layout pass before it carries on.</summary>
    private static readonly TimeSpan LayoutWait = TimeSpan.FromMilliseconds(50);

    /// <summary>A serif typeface, with fallbacks found on each platform.</summary>
    private static readonly FontFamily SerifFamily = new("Noto Serif, Source Serif 4, DejaVu Serif, Georgia, Cambria, Times New Roman, serif");

    /// <summary>The page and text colours of each page colour choice after the first, which follows the pages.</summary>
    private static readonly FocusColours[] PageColours =
    [
        new(Color.FromRgb(0xF4, 0xEC, 0xD8), Color.FromRgb(0x3B, 0x3A, 0x36)),
        new(Color.FromRgb(0xE3, 0xEB, 0xDD), Color.FromRgb(0x2F, 0x3A, 0x2F)),
        new(Color.FromRgb(0x2A, 0x28, 0x26), Color.FromRgb(0xD8, 0xD2, 0xC8)),
        new(Colors.White, Color.FromRgb(0x1A, 0x1A, 0x1A)),
    ];

    /// <summary>The Focus Mode the view shows. Events read it from here, not from a field.</summary>
    private readonly IObservable<FocusModeViewModel?> _focus;

    /// <summary>Initializes a new instance of the <see cref="FocusView"/> class.</summary>
    public FocusView()
    {
        InitializeComponent();
        _focus = this.WhenChanged(static v => v.ViewModel);
        PageList.ItemTemplate = new FuncDataTemplate<FocusPageViewModel>((_, _) => new FocusPageView { FocusMode = ViewModel });
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(_focus.BindTo(this, static v => v.Toolbar.ViewModel));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Pages, static v => v.PageList.ItemsSource));
            disposables.Add(this.WhenChanged(
                    static v => v.ViewModel!.FontSize,
                    static v => v.ViewModel!.LineSpacing,
                    static v => v.ViewModel!.ParagraphSpacing,
                    static v => v.ViewModel!.TextWidth,
                    static v => v.ViewModel!.PageColour,
                    static v => v.ViewModel!.Font,
                    static (_, _, _, _, _, _) => RxVoid.Default)
                .Merge(this.Events().ResourcesChanged.AsVoid)
                .InjectLatest(_focus)
                .SubscribeSafe(ApplySettings, ErrorTrace.Write));

            // Each new scroll target of the Focus Mode shown is brought into view; the target it already had is not.
            disposables.Add(_focus
                .SwitchMap(static focus => focus is null ? Signal.Never<FocusScrollRequest>() : focus.WhenChanged(static mode => mode.ScrollTarget).Skip(1))
                .Inject(_focus)

                // A newer request cancels the older scroll, which may still be waiting for its page to load.
                .SwitchAsync(ScrollToAsync)
                .SubscribeSafe(static _ => { }, ErrorTrace.Write));
            disposables.Add(this.WhenChanged(static v => v.FocusScroller.Offset).InjectLatest(_focus).SubscribeSafe(ReportTop, ErrorTrace.Write));
        });
    }

    /// <summary>Gets the page views currently realised, top to bottom.</summary>
    /// <returns>The views.</returns>
    internal List<FocusPageView> PageViews()
    {
        var views = new List<FocusPageView>();
        foreach (var visual in PageList.GetVisualDescendants())
        {
            if (visual is FocusPageView view)
            {
                views.Add(view);
            }
        }

        return views;
    }

    /// <summary>Gets a colour's relative brightness.</summary>
    /// <param name="colour">The colour.</param>
    /// <returns>From 0 (black) to 1 (white).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Luminance(Color colour) => ((RedWeight * colour.R) + (GreenWeight * colour.G) + (BlueWeight * colour.B)) / byte.MaxValue;

    /// <summary>
    /// Turns the settings into the resources the styles use. They are set on the inner panel, not this control, so
    /// changing them does not re-trigger this control's own resource observable.
    /// </summary>
    /// <param name="focus">The Focus Mode whose settings are applied.</param>
    private void ApplySettings(FocusModeViewModel focus)
    {
        var size = focus.FontSize;
        var paragraph = size * focus.ParagraphSpacing;
        Root.Resources["FocusFontSize"] = size;
        Root.Resources["FocusLineHeight"] = size * focus.LineSpacing;
        Root.Resources["FocusParagraphMargin"] = new Thickness(0, 0, 0, paragraph);
        Root.Resources["FocusHeadingSize"] = size * HeadingScale;
        Root.Resources["FocusHeadingLineHeight"] = size * HeadingScale * Math.Max(1, focus.LineSpacing * HeadingSpacingShare);
        Root.Resources["FocusHeadingMargin"] = new Thickness(0, paragraph, 0, paragraph * HalfSpace);
        Root.Resources["FocusItemMargin"] = new Thickness(size * ItemIndent, 0, 0, paragraph * HalfSpace);
        Root.Resources["FocusSmallSize"] = size * SmallScale;
        Root.Resources["FocusFontFamily"] = focus.Font == 1 ? SerifFamily : FontFamily.Default;
        PageList.MaxWidth = focus.TextWidth * size * CharacterWidth;

        var (page, text) = PageColour(focus.PageColour);

        Root.Resources["FocusBackground"] = new SolidColorBrush(page);
        Root.Resources["FocusForeground"] = new SolidColorBrush(text);
        Root.Resources["FocusMutedForeground"] = new SolidColorBrush(Color.FromArgb(MutedAlpha, text.R, text.G, text.B));
    }

    /// <summary>Gets the page and text colours of a choice; the first follows the app's page colour.</summary>
    /// <param name="choice">The choice.</param>
    /// <returns>The colours.</returns>
    private FocusColours PageColour(int choice)
    {
        if (choice > 0 && choice <= PageColours.Length)
        {
            return PageColours[choice - 1];
        }

        var paper = this.TryFindResource("AppPaperBrush", out var found) && found is ISolidColorBrush brush ? brush.Color : Colors.White;
        return new(paper, Luminance(paper) > LightPage ? Color.FromRgb(0x22, 0x22, 0x22) : Color.FromRgb(0xE4, 0xE0, 0xD8));
    }

    /// <summary>Brings a place into view: the page, then the block nearest the place, at the top.</summary>
    /// <param name="scroll">The place and the Focus Mode that has it.</param>
    /// <param name="token">Cancelled when a newer request replaces this one.</param>
    /// <returns>A task.</returns>
    private async Task<RxVoid> ScrollToAsync(Injected<FocusScrollRequest, FocusModeViewModel> scroll, CancellationToken token)
    {
        var (request, focus) = scroll;
        if (request.PageIndex < 0 || request.PageIndex >= focus.Pages.Count)
        {
            return RxVoid.Default;
        }

        await focus.Pages[request.PageIndex].LoadAsync().ConfigureAwait(true);

        // Pages near the view load as they come into range and change height, so the place is aligned again after each
        // layout pass, a bounded number of times, until it stops moving. A newer request owns the position once it arrives.
        var moved = true;
        for (var attempt = 0; attempt < MaxAlignAttempts && moved; attempt++)
        {
            await NextLayout().ToTask(token).ConfigureAwait(true);
            moved = !token.IsCancellationRequested && AlignTo(request, focus);
        }

        return RxVoid.Default;
    }

    /// <summary>Scrolls so the block nearest a place is at the top of the view.</summary>
    /// <param name="request">The place.</param>
    /// <param name="focus">The Focus Mode that has it.</param>
    /// <returns>Whether the scroll position changed, so the place may have moved again.</returns>
    private bool AlignTo(FocusScrollRequest request, FocusModeViewModel focus)
    {
        if (FindPageView(request.PageIndex) is not { } pageView || FocusScroller.Content is not Visual content)
        {
            return false;
        }

        Control target = pageView;
        foreach (var block in pageView.BlockViews())
        {
            if (block.ViewModel is { } model && focus.FractionOf(model.PageIndex, model.Bounds.Top) <= request.Fraction + double.Epsilon)
            {
                target = block;
            }
        }

        if (target.TranslatePoint(default, content) is not { } point)
        {
            return false;
        }

        var before = FocusScroller.Offset;
        FocusScroller.Offset = new(before.X, Math.Max(0, point.Y - ScrollMargin));
        return Math.Abs(FocusScroller.Offset.Y - before.Y) > AlignTolerance;
    }

    /// <summary>Creates a signal that fires once when the next layout pass ends.</summary>
    /// <returns>The signal; a short timer ends it when no layout pass is pending.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> NextLayout() =>
        Signal.Merge(
                this.Events().LayoutUpdated.Select(static _ => RxVoid.Default),
                Signal.Timer(LayoutWait, RxSchedulers.MainThreadScheduler).Select(static _ => RxVoid.Default))
            .Take(1);

    /// <summary>Finds a realised page view.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The view, or <see langword="null"/>.</returns>
    private FocusPageView? FindPageView(int pageIndex)
    {
        foreach (var view in PageViews())
        {
            if (view.ViewModel?.PageIndex == pageIndex)
            {
                return view;
            }
        }

        return null;
    }

    /// <summary>Reports the block at the top of the view, so switching back to the pages keeps the place.</summary>
    /// <param name="focus">The Focus Mode to tell.</param>
    private void ReportTop(FocusModeViewModel focus)
    {
        foreach (var page in PageViews())
        {
            if (page.TranslatePoint(default, FocusScroller) is not { } pageTop || pageTop.Y + page.Bounds.Height <= 0 || page.ViewModel is not { } pageModel)
            {
                continue;
            }

            foreach (var block in page.BlockViews())
            {
                if (block.TranslatePoint(default, FocusScroller) is not { } top || top.Y + block.Bounds.Height <= 0 || block.ViewModel is not { } model)
                {
                    continue;
                }

                focus.ReportTop(model.PageIndex, focus.FractionOf(model.PageIndex, model.Bounds.Top));
                return;
            }

            focus.ReportTop(pageModel.PageIndex, 0);
            return;
        }
    }
}
