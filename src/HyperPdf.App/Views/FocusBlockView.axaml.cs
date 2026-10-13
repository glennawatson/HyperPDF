// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Reading;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;

namespace HyperPdf.App.Views;

/// <summary>A block of Focus Mode text: styled by its kind, with the sentence and word being read aloud marked.</summary>
[DebuggerDisplay("FocusBlockView: {ViewModel}")]
public sealed partial class FocusBlockView : ReactiveUI.Avalonia.ReactiveUserControl<FocusBlockViewModel>
{
    /// <summary>The opacity of text dimmed by the focus band.</summary>
    private const double DimmedOpacity = 0.4;

    /// <summary>The style classes for each kind of block.</summary>
    private static readonly string[] KindClasses = ["paragraph", "heading", "item", "caption", "footnote", "cell", "figure"];

    /// <summary>Initializes a new instance of the <see cref="FocusBlockView"/> class.</summary>
    public FocusBlockView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(ShowBlock, OnError));

            // The sentence being read is marked with a theme brush, so the marks follow a change of resources.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Spoken, static v => v.ViewModel!.Word, static (_, _) => RxVoid.Default)
                .Merge(this.Events().ResourcesChanged.AsVoid)
                .SubscribeSafe(_ => ShowMarks(), OnError));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsDimmed, static v => v.Opacity, static dimmed => dimmed ? DimmedOpacity : 1));
            disposables.Add(this.ContextRequests
                .Where(_ => FocusMode is not null)
                .MarkHandled()
                .InjectLatest(this.WhenChanged(static v => v.ViewModel))
                .SubscribeSafe(
                    block => new ContextMenu { ItemsSource = new[] { new MenuItem { Header = "_Read Aloud from Here", Command = FocusMode?.ReadFromHereCommand, CommandParameter = block } } }
                        .Open(this),
                    OnError));
        });
    }

    /// <summary>Gets or sets Focus Mode, which reads aloud from a block chosen in its menu.</summary>
    public FocusModeViewModel? FocusMode { get; set; }

    /// <summary>Gets the block's text control, for tests and place keeping.</summary>
    internal TextBlock BodyText => Body;

    /// <summary>Splits a block's text into runs for the marks: before, the sentence (with the word inside it) and after.</summary>
    /// <param name="length">The text's length.</param>
    /// <param name="spoken">The sentence, relative to the block.</param>
    /// <param name="word">The word, relative to the block.</param>
    /// <returns>Each run's start, length and whether it is the sentence or the word.</returns>
    internal static List<BlockRun> Split(int length, TextRange spoken, TextRange word)
    {
        var cuts = new SortedSet<int> { 0, length };
        foreach (var range in (ReadOnlySpan<TextRange>)[spoken, word])
        {
            if (range.IsEmpty)
            {
                continue;
            }

            _ = cuts.Add(Math.Clamp(range.Start, 0, length));
            _ = cuts.Add(Math.Clamp(range.End, 0, length));
        }

        var runs = new List<BlockRun>(cuts.Count);
        var previous = -1;
        foreach (var cut in cuts)
        {
            if (previous >= 0 && cut > previous)
            {
                runs.Add(new(previous, cut - previous, Inside(spoken, previous), Inside(word, previous)));
            }

            previous = cut;
        }

        return runs;
    }

    /// <summary>Determines whether a position is inside a range.</summary>
    /// <param name="range">The range.</param>
    /// <param name="position">The position.</param>
    /// <returns><see langword="true"/> inside.</returns>
    private static bool Inside(TextRange range, int position) => !range.IsEmpty && position >= range.Start && position < range.End;

    /// <summary>Reports a binding failure.</summary>
    /// <param name="error">The error.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Shows a block's kind and text.</summary>
    /// <param name="block">The block.</param>
    private void ShowBlock(FocusBlockViewModel? block)
    {
        foreach (var kind in KindClasses)
        {
            _ = Body.Classes.Remove(kind);
        }

        if (block is not null)
        {
            Body.Classes.Add(KindClasses[(int)block.Kind]);
            AutomationProperties.SetName(Body, block.Text);
            FocusAccessibility.Describe(Body, block);
        }

        ShowMarks();
    }

    /// <summary>Shows the text, marking the sentence and word being read aloud.</summary>
    private void ShowMarks()
    {
        var block = ViewModel;
        Body.Inlines?.Clear();
        if (block is null)
        {
            Body.Text = null;
            return;
        }

        if (block.Spoken.IsEmpty)
        {
            Body.Text = block.Text;
            return;
        }

        Body.Text = null;
        var inlines = Body.Inlines ??= [];
        foreach (var (start, length, spoken, word) in Split(block.Text.Length, block.Spoken, block.Word))
        {
            var run = new Run(block.Text.Substring(start, length));
            if (spoken)
            {
                run.Background = this.FindResource("AppSpokenBrush") as IBrush;
            }

            if (word)
            {
                run.TextDecorations = TextDecorations.Underline;
            }

            inlines.Add(run);
        }
    }
}
