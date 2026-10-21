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
    /// <summary>The alpha byte of an opaque colour, in the top byte of an ARGB value.</summary>
    private const uint OpaqueAlpha = 0xFF000000;

    /// <summary>The mask of the red, green and blue bytes of an ARGB value.</summary>
    private const uint RgbMask = 0x00FFFFFF;

    /// <summary>White as 0xRRGGBB.</summary>
    private const uint White = 0xFFFFFF;

    /// <summary>The opacity of secondary text.</summary>
    private const byte MutedAlpha = 0xA6;

    /// <summary>The space kept above the place scrolled to.</summary>
    private const double ScrollMargin = 12;

    /// <summary>How many times a scroll aligns the place again after the layout changes under it.</summary>
    private const int MaxAlignAttempts = 6;

    /// <summary>How far the scroll position may move, in pixels, and still count as settled.</summary>
    private const double AlignTolerance = 0.5;

    /// <summary>How long a scroll waits for a layout pass before it carries on.</summary>
    private static readonly TimeSpan LayoutWait = TimeSpan.FromMilliseconds(50);

    /// <summary>A serif typeface, with fallbacks found on each platform.</summary>
    private static readonly FontFamily SerifFamily = new("Noto Serif, Source Serif 4, DejaVu Serif, Georgia, Cambria, Times New Roman, serif");

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

    /// <summary>Turns a packed 0xRRGGBB colour into an opaque Avalonia colour.</summary>
    /// <param name="rgb">The packed colour.</param>
    /// <returns>The same colour for Avalonia to paint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Color ToColor(uint rgb) => Color.FromUInt32(OpaqueAlpha | rgb);

    /// <summary>
    /// Turns the settings into the resources the styles use. They are set on the inner panel, not this control, so
    /// changing them does not re-trigger this control's own resource observable.
    /// </summary>
    /// <param name="focus">The Focus Mode whose settings are applied.</param>
    private void ApplySettings(FocusModeViewModel focus)
    {
        var type = FocusTypography.From(focus.FontSize, focus.LineSpacing, focus.ParagraphSpacing, focus.TextWidth);
        Root.Resources["FocusFontSize"] = type.FontSize;
        Root.Resources["FocusLineHeight"] = type.LineHeight;
        Root.Resources["FocusParagraphMargin"] = new Thickness(0, 0, 0, type.ParagraphMargin);
        Root.Resources["FocusHeadingSize"] = type.HeadingSize;
        Root.Resources["FocusHeadingLineHeight"] = type.HeadingLineHeight;
        Root.Resources["FocusHeadingMargin"] = new Thickness(0, type.HeadingTopMargin, 0, type.HeadingBottomMargin);
        Root.Resources["FocusItemMargin"] = new Thickness(type.ItemIndent, 0, 0, type.ItemBottomMargin);
        Root.Resources["FocusSmallSize"] = type.SmallSize;
        Root.Resources["FocusFontFamily"] = focus.Font == 1 ? SerifFamily : FontFamily.Default;
        PageList.MaxWidth = type.MaxWidth;

        var colours = FocusColours.Resolve(focus.PageColour, AppPaper());
        var text = ToColor(colours.Text);

        Root.Resources["FocusBackground"] = new SolidColorBrush(ToColor(colours.Page));
        Root.Resources["FocusForeground"] = new SolidColorBrush(text);
        Root.Resources["FocusMutedForeground"] = new SolidColorBrush(Color.FromArgb(MutedAlpha, text.R, text.G, text.B));
    }

    /// <summary>Reads the page colour the theme paints pages with.</summary>
    /// <returns>The colour as 0xRRGGBB, white when the theme has none.</returns>
    private uint AppPaper() =>
        this.TryFindResource("AppPaperBrush", out var found) && found is ISolidColorBrush brush ? brush.Color.ToUInt32() & RgbMask : White;

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
