// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>
/// Edits the bookmarks. An entry is named by the <see cref="OutlineNode.Id"/> that <see cref="IDocument.GetOutline"/> reports;
/// 0 stands for the top level. Each edit is one step in the document's undo history, and a refusal comes back as a result with
/// the reason, for example when a certifying signature does not allow the change.
/// </summary>
public interface IOutlineEditor : IDocumentFeature
{
    /// <summary>Adds a bookmark that goes to the top of a page, as a child of an entry.</summary>
    /// <param name="parentId">The parent entry, or 0 for the top level.</param>
    /// <param name="index">The position among the parent's children; a negative number appends.</param>
    /// <param name="title">The title.</param>
    /// <param name="pageIndex">The zero based page the bookmark goes to.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> AddBookmark(int parentId, int index, string title, int pageIndex);

    /// <summary>Removes a bookmark and the bookmarks below it.</summary>
    /// <param name="id">The entry.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> RemoveBookmark(int id);

    /// <summary>Changes a bookmark's title.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="title">The new title.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> RenameBookmark(int id, string title);

    /// <summary>Moves a bookmark, with the bookmarks below it, under another entry or to another position.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="newParentId">The new parent, or 0 for the top level.</param>
    /// <param name="index">The position among the new parent's children; a negative number appends.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> MoveBookmark(int id, int newParentId, int index);

    /// <summary>Opens or closes a bookmark, so the bookmarks below it start shown or hidden.</summary>
    /// <param name="id">The entry.</param>
    /// <param name="open">Whether it is open.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> SetBookmarkOpen(int id, bool open);
}
