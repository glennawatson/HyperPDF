// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Sets how a run of pages is numbered for a reader, such as <c>i, ii, iii</c> for the front matter and <c>A-1, A-2</c> for an appendix.</summary>
[DebuggerDisplay("PageLabelsViewModel")]
public sealed partial class PageLabelsViewModel : ReactiveObject
{
    /// <summary>The tab the pages are in.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Initializes a new instance of the <see cref="PageLabelsViewModel"/> class.</summary>
    /// <param name="owner">The tab whose pages are numbered.</param>
    public PageLabelsViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        Answered = Signal.Merge(AcceptCommand, CancelCommand);
    }

    /// <summary>Gets the ways a number can be written, in <see cref="PageLabelStyle"/> order.</summary>
    public static IReadOnlyList<string> StyleNames { get; } = UiText.CreateChoices(["No number, only the words before it", "1, 2, 3", "I, II, III", "i, ii, iii", "A, B, C", "a, b, c"]);

    /// <summary>Gets the window's title.</summary>
    public string Title { get; } = UiText.Get("Page Numbers");

    /// <summary>Gets the question's answer: whether the settings were accepted.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Gets the interaction that shows the window; the output says whether to apply the settings.</summary>
    public Interaction<PageLabelsViewModel, bool> ShowInteraction { get; } = new();

    /// <summary>Gets or sets the first page to number, counted from 1.</summary>
    [Reactive]
    public partial string FromPage { get; set; } = string.Empty;

    /// <summary>Gets or sets the last page to number, counted from 1; empty for the end of the document.</summary>
    [Reactive]
    public partial string ToPage { get; set; } = string.Empty;

    /// <summary>Gets or sets the place of the chosen way of writing the number in <see cref="StyleNames"/>.</summary>
    [Reactive]
    public partial int StyleIndex { get; set; }

    /// <summary>Gets or sets the words written before each number.</summary>
    [Reactive]
    public partial string Prefix { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of the first page, counted from 1.</summary>
    [Reactive]
    public partial string FirstNumber { get; set; } = "1";

    /// <summary>Shows the window and applies the settings when they are accepted.</summary>
    /// <returns>A signal that ends when the window is closed and any change is made.</returns>
    [ReactiveCommand]
    public IObservable<RxVoid> Show()
    {
        FromPage = (Math.Max(0, _owner.CurrentPageIndex) + 1).ToString(CultureInfo.InvariantCulture);
        ToPage = string.Empty;
        StyleIndex = (int)PageLabelStyle.Decimal;
        Prefix = string.Empty;
        FirstNumber = "1";
        return ShowInteraction.Handle(this).ToSignal().Do(ApplyIfAccepted).Select(static _ => RxVoid.Default);
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

    /// <summary>Takes the chosen things out when the window was accepted.</summary>
    /// <param name="accepted">Whether the window was accepted.</param>
    private void ApplyIfAccepted(bool accepted)
    {
        if (accepted)
        {
            Apply();
        }
    }

    /// <summary>Numbers every page 1, 2, 3 again.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Reset() => Write([new(0, PageLabelStyle.Decimal, string.Empty, 1)], UiText.Get("Every page is numbered 1, 2, 3 again."));

    /// <summary>Numbers the pages the settings name, and keeps every other page's label.</summary>
    private void Apply()
    {
        var count = _owner.PageCount;
        if (DocumentFeature<IPageLabeler>.Get(_owner.TryGetDocument()) is not { } labeler)
        {
            _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("This document cannot be changed in this way.")));
            return;
        }

        var to = count;
        if (!TryReadPage(FromPage, out var from) || (ToPage.Trim().Length > 0 && !TryReadPage(ToPage, out to)) || from > to
            || !int.TryParse(FirstNumber, NumberStyles.None, CultureInfo.InvariantCulture, out var first) || first < 1)
        {
            _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("Those pages or that first number are not right. Pages are counted from 1, and the first number must be 1 or more.")));
            return;
        }

        var style = (PageLabelStyle)Math.Clamp(StyleIndex, 0, StyleNames.Count - 1);
        Write(PageLabelMerger.Apply(labeler.GetRanges(), count, from - 1, to - 1, style, Prefix, first), UiText.Get("Changed the page numbers. Undo is in the Pages menu."));
    }

    /// <summary>Reads a page number the user typed.</summary>
    /// <param name="text">The words.</param>
    /// <param name="page">Receives the page, counted from 1.</param>
    /// <returns><see langword="true"/> when the words are a page of the document.</returns>
    private bool TryReadPage(string text, out int page) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out page) && page >= 1 && page <= _owner.PageCount;

    /// <summary>Writes the ranges and says so.</summary>
    /// <param name="ranges">The ranges.</param>
    /// <param name="done">What to say once it is made.</param>
    private void Write(List<PageLabelRun> ranges, string done)
    {
        if (DocumentFeature<IPageLabeler>.Get(_owner.TryGetDocument()) is not { } labeler)
        {
            _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("This document cannot be changed in this way.")));
            return;
        }

        try
        {
            labeler.SetRanges([..ranges]);
            _owner.Pages.NotifyStructureChanged();
            _owner.SetNotice(LocalizedMessage.Literal(done));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("Could not change the page numbers.")} {ex.Message}"));
        }
    }
}
