// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Finding words in the pages' own text and putting other words in their place.</summary>
public sealed partial class DocumentTabViewModel
{
    /// <summary>Gets the interaction that shows the find and replace window. It answers whether to go ahead.</summary>
    public Interaction<FindReplaceViewModel, bool> FindReplaceInteraction { get; } = new();

    /// <summary>Counts how many times words appear in a text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="find">The words.</param>
    /// <param name="comparison">How letters are compared.</param>
    /// <returns>The count.</returns>
    private static int CountOf(string text, string find, StringComparison comparison)
    {
        var count = 0;
        var at = text.IndexOf(find, comparison);
        while (at >= 0)
        {
            count++;
            at = text.IndexOf(find, at + find.Length, comparison);
        }

        return count;
    }

    /// <summary>Replaces the words in every text object of every page that has them, one page after another.</summary>
    /// <param name="editor">The document's content editor.</param>
    /// <param name="pageCount">The pages.</param>
    /// <param name="request">What to find and what to put in its place.</param>
    /// <param name="cancellationToken">Stops the work between objects.</param>
    /// <returns>What changed.</returns>
    private static async Task<TextReplaceCount> ReplaceAllAsync(IPageContentEditor editor, int pageCount, FindReplaceViewModel request, CancellationToken cancellationToken)
    {
        var comparison = request.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var replaced = 0;
        var pages = new List<int>();
        for (var page = 0; page < pageCount; page++)
        {
            var onPage = await ReplaceOnPageAsync(editor, page, request, comparison, cancellationToken).ConfigureAwait(false);
            if (onPage == 0)
            {
                continue;
            }

            replaced += onPage;
            pages.Add(page);
        }

        return new(replaced, pages);
    }

    /// <summary>
    /// Replaces the words in each text object of one page that has them. Changing an object can move the others in the page's
    /// list, so the page is read again after each change. It stops after as many changes as the page first had objects to change,
    /// so new words that hold the old ones are not changed again.
    /// </summary>
    /// <param name="editor">The document's content editor.</param>
    /// <param name="page">The page.</param>
    /// <param name="request">What to find and what to put in its place.</param>
    /// <param name="comparison">How letters are compared.</param>
    /// <param name="cancellationToken">Stops the work between objects.</param>
    /// <returns>How many times the words were replaced on the page.</returns>
    private static async Task<int> ReplaceOnPageAsync(IPageContentEditor editor, int page, FindReplaceViewModel request, StringComparison comparison, CancellationToken cancellationToken)
    {
        var objects = await editor.ReadObjects(page).ToTask(cancellationToken).ConfigureAwait(false);
        var left = CountMatching(objects, request.Find, comparison);
        var replaced = 0;
        while (left > 0 && FirstMatching(objects, request.Find, comparison) is { } item)
        {
            await editor.ReplaceText(page, item.Index, item.Text.Replace(request.Find, request.Replacement, comparison)).ToTask(cancellationToken).ConfigureAwait(false);
            replaced += CountOf(item.Text, request.Find, comparison);
            left--;
            objects = await editor.ReadObjects(page).ToTask(cancellationToken).ConfigureAwait(false);
        }

        return replaced;
    }

    /// <summary>Counts the text objects that hold the words.</summary>
    /// <param name="objects">The page's objects.</param>
    /// <param name="find">The words.</param>
    /// <param name="comparison">How letters are compared.</param>
    /// <returns>The count.</returns>
    private static int CountMatching(PageObjectInfo[] objects, string find, StringComparison comparison)
    {
        var count = 0;
        foreach (var item in objects)
        {
            if (item.Kind == PageObjectKind.Text && item.Text.Contains(find, comparison))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Finds the first text object that holds the words.</summary>
    /// <param name="objects">The page's objects.</param>
    /// <param name="find">The words.</param>
    /// <param name="comparison">How letters are compared.</param>
    /// <returns>The object, or <see langword="null"/> when none does.</returns>
    private static PageObjectInfo? FirstMatching(PageObjectInfo[] objects, string find, StringComparison comparison)
    {
        foreach (var item in objects)
        {
            if (item.Kind == PageObjectKind.Text && item.Text.Contains(find, comparison))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Asks what words to find on the pages and what to put in their place, then replaces every one in the pages' own text. Each
    /// change can be undone. Words split across two pieces of text on the page are not found.
    /// </summary>
    /// <returns>A signal that ends once the words are replaced or the window is closed.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> FindAndReplace()
    {
        if (DocumentFeature<IPageContentEditor>.Get(TryGetDocument()) is not { } editor)
        {
            SetNotice(LocalizedMessage.Get(UiText, "This document's text cannot be changed."));
            return Signal.Return(RxVoid.Default);
        }

        var request = new FindReplaceViewModel(SearchIfMade?.Query ?? string.Empty);
        var pageCount = PageCount;
        return FindReplaceInteraction.Handle(request).ToSignal()
            .SwitchMap(accepted => !accepted || request.Find.Length == 0
                ? Signal.Return(RxVoid.Default)
                : Signal.FromAsync(token => ReplaceAllAsync(editor, pageCount, request, token))
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Select(Replaced));
    }

    /// <summary>Has the changed pages drawn again and says how many times the words were replaced.</summary>
    /// <param name="count">What changed.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Replaced(TextReplaceCount count)
    {
        foreach (var page in count.Pages)
        {
            ReportPageChange(page);
        }

        if (count.Replaced > 0)
        {
            Pages.Refresh();
        }

        SetNotice(count.Replaced == 0
            ? LocalizedMessage.Get(UiText, "Those words are not in the pages' text, so nothing was replaced.")
            : LocalizedMessage.Format(UiText, "Replaced the words {0} times on {1} pages.", count.Replaced, count.Pages.Count));
        return RxVoid.Default;
    }
}
