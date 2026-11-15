// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Printing;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>
/// Adds a watermark, a header, a footer, page numbers, Bates numbers, a background colour or a crop to the pages of the open document.
/// One window serves every kind: the view model shows the settings the kind needs, and applies them as one undo step when accepted.
/// </summary>
[DebuggerDisplay("PageDecorationViewModel: {Kind}")]
public sealed partial class PageDecorationViewModel : ReactiveObject, IDisposable
{
    /// <summary>The value of a share of full strength that means solid.</summary>
    private const float Solid = 1F;

    /// <summary>The largest size of stamped text, in points.</summary>
    private const decimal LargestSize = 400;

    /// <summary>The smallest size of stamped text, in points.</summary>
    private const decimal SmallestSize = 4;

    /// <summary>The size of a watermark, in points.</summary>
    private const decimal WatermarkSize = 72;

    /// <summary>The size of a header, a footer or a page number, in points.</summary>
    private const decimal EdgeSize = 10;

    /// <summary>How far a watermark is turned, in degrees.</summary>
    private const decimal WatermarkTurn = 45;

    /// <summary>The distance the text keeps from the edge of the page, in points.</summary>
    private const decimal EdgeMargin = 24;

    /// <summary>The digits of a Bates number.</summary>
    private const int BatesDigits = 6;

    /// <summary>Where each choice of colour is found among the names: grey.</summary>
    private const int GreyIndex = 1;

    /// <summary>The place of the 50 percent choice among the opacities.</summary>
    private const int HalfOpacityIndex = 2;

    /// <summary>The place of the "this page" choice among the page scopes.</summary>
    private const int ThisPageScope = 1;

    /// <summary>The place of the "pages in a range" choice among the page scopes.</summary>
    private const int RangeScope = 2;

    /// <summary>The colours offered for stamped text, as opaque ARGB values.</summary>
    private static readonly uint[] TextColors = [0xFF000000, 0xFF666666, 0xFFC62828, 0xFF1565C0, 0xFF2E7D32];

    /// <summary>The colours offered for a background, as opaque ARGB values.</summary>
    private static readonly uint[] BackgroundColors = [0xFFFFFFFF, 0xFFF2F2F2, 0xFFFFF4B0, 0xFFFFF0D9, 0xFFDDEBFF, 0xFFDFF3E3, 0xFFEFE2FF];

    /// <summary>How solid the choices of opacity are.</summary>
    private static readonly float[] Opacities = [1F, 0.75F, 0.5F, 0.25F];

    /// <summary>The tab the pages are in.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Stops work that is going on when the view model is disposed.</summary>
    private readonly CancellationDisposable _lifetime = new();

    /// <summary>Initializes a new instance of the <see cref="PageDecorationViewModel"/> class.</summary>
    /// <param name="owner">The tab whose pages are decorated.</param>
    public PageDecorationViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        Answered = Signal.Merge(AcceptCommand, CancelCommand);
    }

    /// <summary>Gets the looks of letters offered.</summary>
    public static IReadOnlyList<string> Families { get; } = UiText.CreateChoices(["Sans serif", "Serif", "Fixed width"]);

    /// <summary>Gets the colours offered for stamped text, by name.</summary>
    public static IReadOnlyList<string> ColorNames { get; } = UiText.CreateChoices(["Black", "Grey", "Red", "Blue", "Green"]);

    /// <summary>Gets the colours offered for a background, by name.</summary>
    public static IReadOnlyList<string> BackgroundNames { get; } = UiText.CreateChoices(["White", "Light grey", "Light yellow", "Cream", "Light blue", "Light green", "Lilac"]);

    /// <summary>Gets how solid stamped text can be.</summary>
    public static IReadOnlyList<string> OpacityNames { get; } = UiText.CreateChoices(["Solid", "A little see-through", "Half see-through", "Very see-through"]);

    /// <summary>Gets the places on the page that text can sit, in <see cref="PageStampAnchor"/> order.</summary>
    public static IReadOnlyList<string> AnchorNames { get; } = UiText.CreateChoices(
        ["Middle of the page", "Top left", "Top middle", "Top right", "Bottom left", "Bottom middle", "Bottom right", "Left side", "Right side"]);

    /// <summary>Gets which pages a decoration goes on.</summary>
    public static IReadOnlyList<string> ScopeNames { get; } = UiText.CreateChoices(["Every page", "This page only", "The pages I type"]);

    /// <summary>Gets the question's answer: whether the settings were accepted.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Gets the interaction that shows the window; the output says whether to apply the settings.</summary>
    public Interaction<PageDecorationViewModel, bool> ShowInteraction { get; } = new();

    /// <summary>Gets what is being added.</summary>
    [Reactive]
    public partial PageDecorationKind Kind { get; private set; }

    /// <summary>Gets or sets the words stamped on the pages, which may hold {page}, {pages}, {number} and {date}.</summary>
    [Reactive]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the place of the chosen look of letters in <see cref="Families"/>.</summary>
    [Reactive]
    public partial int FamilyIndex { get; set; }

    /// <summary>Gets or sets a value indicating whether the letters are bold.</summary>
    [Reactive]
    public partial bool IsBold { get; set; }

    /// <summary>Gets or sets a value indicating whether the letters are slanted.</summary>
    [Reactive]
    public partial bool IsItalic { get; set; }

    /// <summary>Gets or sets the size of the letters, in points.</summary>
    [Reactive]
    public partial decimal FontSize { get; set; } = EdgeSize;

    /// <summary>Gets or sets the place of the chosen colour in <see cref="ColorNames"/>.</summary>
    [Reactive]
    public partial int ColorIndex { get; set; }

    /// <summary>Gets or sets the place of the chosen opacity in <see cref="OpacityNames"/>.</summary>
    [Reactive]
    public partial int OpacityIndex { get; set; }

    /// <summary>Gets or sets how far the text is turned anticlockwise, in degrees.</summary>
    [Reactive]
    public partial decimal Rotation { get; set; }

    /// <summary>Gets or sets the place of the chosen anchor in <see cref="AnchorNames"/>.</summary>
    [Reactive]
    public partial int AnchorIndex { get; set; }

    /// <summary>Gets or sets how far the text keeps from the edge of the page, in points.</summary>
    [Reactive]
    public partial decimal Margin { get; set; } = EdgeMargin;

    /// <summary>Gets or sets a value indicating whether the text is drawn under the page's own content.</summary>
    [Reactive]
    public partial bool IsBehind { get; set; }

    /// <summary>Gets or sets the number a running number starts at.</summary>
    [Reactive]
    public partial int FirstNumber { get; set; } = 1;

    /// <summary>Gets or sets how many digits a running number is padded to.</summary>
    [Reactive]
    public partial int Digits { get; set; } = BatesDigits;

    /// <summary>Gets or sets the place of the chosen background colour in <see cref="BackgroundNames"/>.</summary>
    [Reactive]
    public partial int BackgroundIndex { get; set; }

    /// <summary>Gets or sets the width taken off the left when pages are cropped, in points.</summary>
    [Reactive]
    public partial decimal CropLeft { get; set; }

    /// <summary>Gets or sets the height taken off the top when pages are cropped, in points.</summary>
    [Reactive]
    public partial decimal CropTop { get; set; }

    /// <summary>Gets or sets the width taken off the right when pages are cropped, in points.</summary>
    [Reactive]
    public partial decimal CropRight { get; set; }

    /// <summary>Gets or sets the height taken off the bottom when pages are cropped, in points.</summary>
    [Reactive]
    public partial decimal CropBottom { get; set; }

    /// <summary>Gets or sets the place of the chosen scope in <see cref="ScopeNames"/>.</summary>
    [Reactive]
    public partial int ScopeIndex { get; set; }

    /// <summary>Gets or sets the pages typed for the range scope, such as 1-3, 7.</summary>
    [Reactive]
    public partial string RangeText { get; set; } = string.Empty;

    /// <summary>Gets the name of the window.</summary>
    public string Title => Kind switch
    {
        PageDecorationKind.Watermark => UiText.Get("Add a watermark"),
        PageDecorationKind.Header => UiText.Get("Add a header"),
        PageDecorationKind.Footer => UiText.Get("Add a footer"),
        PageDecorationKind.PageNumbers => UiText.Get("Add page numbers"),
        PageDecorationKind.Bates => UiText.Get("Add Bates numbers"),
        PageDecorationKind.Background => UiText.Get("Fill the page background"),
        _ => UiText.Get("Crop the pages"),
    };

    /// <summary>Gets a value indicating whether the window asks for words, letters and a place: every kind but a background and a crop.</summary>
    public bool IsText => Kind is not (PageDecorationKind.Background or PageDecorationKind.Crop);

    /// <summary>Gets a value indicating whether the window asks for a rotation and whether the text is behind: a watermark.</summary>
    public bool IsWatermark => Kind == PageDecorationKind.Watermark;

    /// <summary>Gets a value indicating whether the window asks for the running number: Bates numbers.</summary>
    public bool IsBates => Kind == PageDecorationKind.Bates;

    /// <summary>Gets a value indicating whether the window asks for a background colour.</summary>
    public bool IsBackground => Kind == PageDecorationKind.Background;

    /// <summary>Gets a value indicating whether the window asks for margins to crop.</summary>
    public bool IsCrop => Kind == PageDecorationKind.Crop;

    /// <summary>Gets a value indicating whether the window asks for pages to type.</summary>
    public bool AsksForPages => ScopeIndex == RangeScope;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _lifetime.Dispose();

    /// <summary>Shows the window for one kind of decoration, and applies the settings when they are accepted.</summary>
    /// <param name="kind">What to add.</param>
    /// <returns>A task that ends when the window is closed and any change is made.</returns>
    public async Task ShowAsync(PageDecorationKind kind)
    {
        Begin(kind);
        if (await ShowInteraction.Handle(this).ToTask().ConfigureAwait(true))
        {
            await ApplyAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Gives up without changing the pages.</summary>
    /// <returns>Always <see langword="false"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;

    /// <summary>Accepts the settings.</summary>
    /// <returns>Always <see langword="true"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Accept() => true;

    /// <summary>Sets the settings a kind of decoration starts with.</summary>
    /// <param name="kind">What is being added.</param>
    private void Begin(PageDecorationKind kind)
    {
        Kind = kind;
        this.RaisePropertyChanged(nameof(Title));
        Text = kind switch
        {
            PageDecorationKind.Watermark => UiText.Get("CONFIDENTIAL"),
            PageDecorationKind.Header => "{date}",
            PageDecorationKind.PageNumbers => UiText.Get("Page {page} of {pages}"),
            PageDecorationKind.Bates => "{number}",
            _ => string.Empty,
        };
        var watermark = kind == PageDecorationKind.Watermark;
        FontSize = watermark ? WatermarkSize : EdgeSize;
        Rotation = watermark ? WatermarkTurn : 0;
        ColorIndex = watermark ? GreyIndex : 0;
        OpacityIndex = watermark ? HalfOpacityIndex : 0;
        AnchorIndex = (int)(kind switch
        {
            PageDecorationKind.Header => PageStampAnchor.TopCenter,
            PageDecorationKind.Footer or PageDecorationKind.PageNumbers => PageStampAnchor.BottomCenter,
            PageDecorationKind.Bates => PageStampAnchor.BottomRight,
            _ => PageStampAnchor.Center,
        });
        IsBehind = false;
        ScopeIndex = 0;
        RangeText = string.Empty;
    }

    /// <summary>Shows the window for a watermark.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task ShowWatermarkAsync() => ShowAsync(PageDecorationKind.Watermark);

    /// <summary>Shows the window for a header.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task ShowHeaderAsync() => ShowAsync(PageDecorationKind.Header);

    /// <summary>Shows the window for a footer.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task ShowFooterAsync() => ShowAsync(PageDecorationKind.Footer);

    /// <summary>Shows the window for page numbers.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task ShowPageNumbersAsync() => ShowAsync(PageDecorationKind.PageNumbers);

    /// <summary>Shows the window for Bates numbers.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task ShowBatesAsync() => ShowAsync(PageDecorationKind.Bates);

    /// <summary>Shows the window for a background colour.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task ShowBackgroundAsync() => ShowAsync(PageDecorationKind.Background);

    /// <summary>Shows the window for a crop.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task ShowCropAsync() => ShowAsync(PageDecorationKind.Crop);

    /// <summary>Gets the pages the settings apply to.</summary>
    /// <returns>The zero based pages, or <see langword="null"/> when the pages typed are not in the document.</returns>
    private int[]? ChosenPages()
    {
        var count = _owner.PageCount;
        if (ScopeIndex == ThisPageScope)
        {
            return [Math.Clamp(_owner.CurrentPageIndex, 0, count - 1)];
        }

        if (ScopeIndex != RangeScope)
        {
            var every = new int[count];
            for (var i = 0; i < every.Length; i++)
            {
                every[i] = i;
            }

            return every;
        }

        var typed = new List<int>();
        return PageRanges.TryParse(RangeText, count, typed) ? [.. typed] : null;
    }

    /// <summary>Makes the stamp from the settings.</summary>
    /// <returns>The stamp.</returns>
    private PageTextStamp MakeStamp() => new(
        Text,
        (PageTextFamily)Math.Clamp(FamilyIndex, 0, Families.Count - 1),
        IsBold,
        IsItalic,
        (float)Math.Clamp(FontSize, SmallestSize, LargestSize),
        TextColors[Math.Clamp(ColorIndex, 0, TextColors.Length - 1)],
        Opacities[Math.Clamp(OpacityIndex, 0, Opacities.Length - 1)],
        IsWatermark ? (float)Rotation : 0F,
        (PageStampAnchor)Math.Clamp(AnchorIndex, 0, AnchorNames.Count - 1),
        (float)Math.Max(0, Margin),
        IsBehind)
    { FirstNumber = FirstNumber, Digits = Digits };

    /// <summary>Makes the change the settings ask for, in one undo step.</summary>
    /// <returns>A task that ends when the pages have changed or the notice says why they could not.</returns>
    private async Task ApplyAsync()
    {
        if (DocumentFeature<IPageDecorator>.Get(_owner.TryGetDocument()) is not { } decorator)
        {
            _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("This document cannot be changed in this way.")));
            return;
        }

        if (ChosenPages() is not { Length: > 0 } pages)
        {
            _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("Those page numbers are not in the document.")));
            return;
        }

        if (IsText && string.IsNullOrWhiteSpace(Text))
        {
            _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("Type the words to add first.")));
            return;
        }

        try
        {
            var token = _lifetime.Token;
            await Task.Run(() => RunAsync(decorator, pages, token), token).ConfigureAwait(true);
            if (Kind == PageDecorationKind.Crop)
            {
                _owner.Pages.NotifyStructureChanged();
            }

            foreach (var page in pages)
            {
                _owner.ReportPageChange(page);
            }

            _owner.SetNotice(LocalizedMessage.Literal(UiText.Format("Changed {0} pages. Undo is in the Pages menu.", pages.Length)));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("Could not change the pages.")} {ex.Message}"));
        }
    }

    /// <summary>Calls the feature for the kind being added.</summary>
    /// <param name="decorator">The feature.</param>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="token">Stops the work.</param>
    /// <returns>A task.</returns>
    private Task RunAsync(IPageDecorator decorator, int[] pages, CancellationToken token) => Kind switch
    {
        PageDecorationKind.Background => decorator.FillBackgroundAsync(pages, BackgroundColors[Math.Clamp(BackgroundIndex, 0, BackgroundColors.Length - 1)], token).AsTask(),
        PageDecorationKind.Crop => decorator.CropAsync(pages, new((float)CropLeft, (float)CropTop, (float)CropRight, (float)CropBottom), token).AsTask(),
        _ => decorator.StampTextAsync(pages, MakeStamp(), token).AsTask(),
    };
}
