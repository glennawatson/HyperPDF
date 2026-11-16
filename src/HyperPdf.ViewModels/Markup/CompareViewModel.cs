// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Compares the open document with another file and lists the pages whose words differ.</summary>
[DebuggerDisplay("CompareViewModel: {Differences.Count} pages differ")]
public sealed partial class CompareViewModel : ReactiveObject
{
    /// <summary>The tab whose document is compared.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The services that open the other file.</summary>
    private readonly AppServices _services;

    /// <summary>Initializes a new instance of the <see cref="CompareViewModel"/> class.</summary>
    /// <param name="owner">The tab whose document is compared.</param>
    /// <param name="services">The services that open the other file.</param>
    public CompareViewModel(DocumentTabViewModel owner, AppServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(services);
        _owner = owner;
        _services = services;
    }

    /// <summary>Gets the window's title.</summary>
    public string Title { get; } = UiText.Get("Compare with Another Document");

    /// <summary>Gets the interaction that asks which file to compare with; the output is its path, or <see langword="null"/> when the choice is given up.</summary>
    public Interaction<RxVoid, string?> ChooseFileInteraction { get; } = new();

    /// <summary>Gets the interaction that shows the result.</summary>
    public Interaction<CompareViewModel, RxVoid> ShowInteraction { get; } = new();

    /// <summary>Gets the pages whose words differ.</summary>
    [Reactive]
    public partial IReadOnlyList<ComparedPage> Differences { get; private set; } = [];

    /// <summary>Gets the words that say what the comparison found.</summary>
    [Reactive]
    public partial string Result { get; private set; } = string.Empty;

    /// <summary>Makes the words of the line of a differing page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The words.</returns>
    public static string Describe(ComparedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return UiText.Format("Page {0}: {1} words added, {2} words removed. Press to go to it.", page.Page + 1, page.Added, page.Removed);
    }

    /// <summary>Goes to a page that differs.</summary>
    /// <param name="page">The page.</param>
    public void GoTo(ComparedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (page.Page < _owner.PageCount)
        {
            _owner.GoToPage(page.Page);
        }
    }

    /// <summary>Asks for a file, compares it with the open document and shows the pages that differ.</summary>
    /// <returns>A signal that ends when the window is closed.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> Compare()
    {
        if (_owner.TryGetDocument() is not { } current)
        {
            return Signal.Return(RxVoid.Default);
        }

        return ChooseFileInteraction.Handle(RxVoid.Default).ToSignal()
            .SwitchMap(path => path is { Length: > 0 } ? FindDifferences(current, path).SwitchMap(ShowFound) : Signal.Return(RxVoid.Default));
    }

    /// <summary>Opens the other file and compares it on the pool, then hands the result back to the interface thread.</summary>
    /// <param name="current">The open document.</param>
    /// <param name="path">The other file.</param>
    /// <returns>A cold signal giving the pages that differ, or <see langword="null"/> when the file could not be compared.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<List<ComparedPage>?> FindDifferences(IDocument current, string path) =>
        Signal.Start<List<ComparedPage>?>(() => Run(current, path), TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Catch<List<ComparedPage>?, Exception>(Unreadable);

    /// <summary>Says the other file could not be compared.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal giving <see langword="null"/> for an expected failure, or failing with the same error.</returns>
    private IObservable<List<ComparedPage>?> Unreadable(Exception error)
    {
        var expected = error is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException;
        if (expected)
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("That file could not be compared.")} {error.Message}"));
        }

        return expected ? Signal.Return<List<ComparedPage>?>(null) : Signal.Throw<List<ComparedPage>?>(error);
    }

    /// <summary>Opens the other file and compares it.</summary>
    /// <param name="current">The open document.</param>
    /// <param name="path">The other file.</param>
    /// <returns>The pages that differ.</returns>
    private List<ComparedPage> Run(IDocument current, string path)
    {
        using var other = _services.Engine.Open(path, null);
        return DocumentComparer.Compare(current, other, CancellationToken.None);
    }

    /// <summary>Keeps what the comparison found and shows it.</summary>
    /// <param name="found">The pages that differ, or <see langword="null"/> when the comparison failed.</param>
    /// <returns>A signal that ends when the window is closed.</returns>
    private IObservable<RxVoid> ShowFound(List<ComparedPage>? found)
    {
        if (found is null)
        {
            return Signal.Return(RxVoid.Default);
        }

        Differences = found;
        Result = found.Count == 0
            ? UiText.Get("The words of the two documents are the same.")
            : UiText.Format("{0} pages have different words. Text in pictures and the look of the pages are not compared.", found.Count);
        return ShowInteraction.Handle(this).ToSignal();
    }
}
