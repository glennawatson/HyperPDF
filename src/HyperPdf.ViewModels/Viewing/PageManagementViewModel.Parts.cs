// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Output;
using HyperPdf.ViewModels.Sidebar;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Shuffling a file's pages in, and saving the document in parts that start at its bookmarks.</summary>
public sealed partial class PageManagementViewModel
{
    /// <summary>The pages of a pair when pages are shuffled in: one of the document's and one of the file's.</summary>
    private const int PairSize = 2;

    /// <summary>Lists where the pages shuffled in from a file end up.</summary>
    /// <param name="own">The document's pages before.</param>
    /// <param name="added">The pages shuffled in.</param>
    /// <returns>Their indexes, lowest first.</returns>
    private static List<int> ShuffledIn(int own, int added)
    {
        var pages = new List<int>(added);
        var pairs = Math.Min(own, added);
        for (var index = 0; index < pairs; index++)
        {
            pages.Add((index * PairSize) + 1);
        }

        // The file's pages left over when the document runs out follow every other page.
        for (var index = pairs; index < added; index++)
        {
            pages.Add(own + index);
        }

        return pages;
    }

    /// <summary>Finds where each part starts: the pages the top bookmarks point at, lowest first, with the first part always starting at the first page.</summary>
    /// <param name="bookmarks">The top bookmarks.</param>
    /// <param name="pageCount">The document's pages.</param>
    /// <returns>The first page of each part.</returns>
    private static List<int> PartStarts(OutlineItemViewModel[] bookmarks, int pageCount)
    {
        var starts = new SortedSet<int> { 0 };
        foreach (var bookmark in bookmarks)
        {
            if (bookmark.Target is { Kind: LinkTargetKind.Page, PageIndex: >= 0 } target && target.PageIndex < pageCount)
            {
                _ = starts.Add(target.PageIndex);
            }
        }

        return [.. starts];
    }

    /// <summary>Asks for a PDF and shuffles its pages in among the document's: the document's first page, the file's first, and on.</summary>
    /// <returns>A signal that ends once the pages are in, the choice was given up or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEdit))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Interleave() => Run(
        manager => InsertInteraction.Handle(RxVoid.Default).ToSignal()
            .SwitchMap(paths => paths.Length > 0 ? InterleaveWith(manager, paths[0]) : Signal.Return(default(PageManagementOperationResult))),
        UiText.Get("Could not shuffle the pages in."),
        true);

    /// <summary>Asks for a folder and saves the document there in parts, each starting at a top bookmark's page.</summary>
    /// <returns>A signal that ends once the files are written, the choice was given up or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEdit))]
    private IObservable<RxVoid> SplitAtBookmarks()
    {
        if (!_owner.HasOutline)
        {
            _owner.SetNotice(LocalizedMessage.Get(UiText, "This document has no bookmarks to split it at."));
            return Signal.Return(RxVoid.Default);
        }

        return Run(
            manager => SplitFolderInteraction.Handle(RxVoid.Default).ToSignal().SwitchMap(folder => SplitIntoParts(manager, folder)),
            UiText.Get("Could not save the parts as separate files."),
            false);
    }

    /// <summary>Shuffles a file's pages in and selects them.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="path">The file.</param>
    /// <returns>A cold signal giving the result of the operation.</returns>
    private IObservable<PageManagementOperationResult> InterleaveWith(IPageManager manager, string path)
    {
        var own = _owner.PageCount;
        var report = new PageEditReport();
        return OnPool(manager.Interleave(path, report)).Select(_ => Interleaved(report, own));
    }

    /// <summary>Says what the copy could not keep and selects the pages shuffled in.</summary>
    /// <param name="report">What the copy could not keep.</param>
    /// <param name="own">The document's pages before.</param>
    /// <returns>The result of the operation.</returns>
    private PageManagementOperationResult Interleaved(PageEditReport report, int own)
    {
        ShowSeparationNotice(report);
        return new(true, () => ShuffledIn(own, Math.Max(0, _owner.PageCount - own)));
    }

    /// <summary>Saves each part of the document as a PDF of its own in a folder, named after the document and the part's number.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="folder">The folder, or empty when the choice was given up.</param>
    /// <returns>A cold signal giving an operation that changed nothing in the open document.</returns>
    private IObservable<PageManagementOperationResult> SplitIntoParts(IPageManager manager, string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return Signal.Return(default(PageManagementOperationResult));
        }

        var starts = PartStarts(_owner.Outline, _owner.PageCount);
        return LazySequence.Concat(PartFiles(manager, folder, starts))
            .Count()
            .Select(written => PartsDone(written, folder));
    }

    /// <summary>Makes the writing of one file for each part, started one after another.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="folder">The folder.</param>
    /// <param name="starts">The first page of each part.</param>
    /// <returns>The writes, in part order.</returns>
    private IEnumerable<IObservable<bool>> PartFiles(IPageManager manager, string folder, List<int> starts)
    {
        var name = Path.GetFileNameWithoutExtension(_owner.FileName);
        for (var part = 0; part < starts.Count; part++)
        {
            var end = part + 1 < starts.Count ? starts[part + 1] : _owner.PageCount;
            var pages = CopyRange(starts[part], end - starts[part]);
            var destination = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"{name}-part-{part + 1}.pdf"));
            yield return CopyFile.WriteSignal(destination, TemporarySuffix, stream => manager.Extract(pages, stream).Select(static _ => true), static written => written).TakeUntil(_lifetime);
        }
    }

    /// <summary>Says how many parts were written and where.</summary>
    /// <param name="written">How many files were written.</param>
    /// <param name="folder">The folder.</param>
    /// <returns>An operation that changed nothing in the open document.</returns>
    private PageManagementOperationResult PartsDone(int written, string folder)
    {
        _owner.SetNotice(LocalizedMessage.Format(UiText, "Saved the document as {0} parts in {1}, each starting at a bookmark.", written, folder));
        return default;
    }
}
