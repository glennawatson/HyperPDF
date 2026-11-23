// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Documents;

/// <summary>Edits page order and geometry while retaining document structure and undo history.</summary>
public interface IPageManager
{
    /// <summary>Gets the description of the edit that can be undone, or null.</summary>
    string? UndoLabel { get; }

    /// <summary>Gets the description of the edit that can be redone, or null.</summary>
    string? RedoLabel { get; }

    /// <summary>Deletes, rotates, moves or copies selected pages as one undo step.</summary>
    /// <param name="request">The operation and zero-based indexes.</param>
    /// <returns>A cold signal giving one value after the document has changed; unsubscribing cancels page resource reads and the edit.</returns>
    IObservable<RxVoid> Apply(PageEditRequest request);

    /// <summary>Deletes, rotates, moves or copies selected pages as one undo step, and says what the edit could not keep.</summary>
    /// <param name="request">The operation and zero-based indexes.</param>
    /// <param name="report">Receives the printed pages of a preseparated document whose separations were not kept together.</param>
    /// <returns>A cold signal giving one value after the document has changed; unsubscribing cancels page resource reads and the edit.</returns>
    IObservable<RxVoid> Apply(PageEditRequest request, PageEditReport report) => Apply(request);

    /// <summary>Inserts every page of the selected files as one undo step, retaining forms, outlines and links.</summary>
    /// <param name="index">The first inserted page's zero-based index, from zero to the current page count.</param>
    /// <param name="files">PDF files in insertion order.</param>
    /// <returns>A cold signal giving one value after the document has changed; unsubscribing cancels opening and importing the files.</returns>
    IObservable<RxVoid> Insert(int index, string[] files);

    /// <summary>Inserts every page of the selected files as one undo step, and says what the copy could not keep.</summary>
    /// <param name="index">The first inserted page's zero-based index, from zero to the current page count.</param>
    /// <param name="files">PDF files in insertion order.</param>
    /// <param name="report">Receives what the copy could not keep, such as trap networks that no longer matched their pages.</param>
    /// <returns>A cold signal giving one value after the document has changed; unsubscribing cancels opening and importing the files.</returns>
    IObservable<RxVoid> Insert(int index, string[] files, PageEditReport report) => Insert(index, files);

    /// <summary>
    /// Shuffles the pages of a file in among the document's pages as one undo step: the document's first page, the file's first
    /// page, the document's second page, and on. The pages left over when one runs out go at the end. Two files of single-sided
    /// scans, one of the fronts and one of the backs, become one file in reading order.
    /// </summary>
    /// <param name="file">The PDF whose pages go in between.</param>
    /// <param name="report">Receives what the copy could not keep.</param>
    /// <returns>A cold signal giving one value after the document has changed; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> Interleave(string file, PageEditReport report) =>
        Signal.Throw<RxVoid>(new NotSupportedException("This document cannot have pages shuffled in."));

    /// <summary>Makes a page of each picture, in order, and inserts them as one undo step.</summary>
    /// <param name="index">The first inserted page's zero-based index, from zero to the current page count.</param>
    /// <param name="images">The pictures, one page each.</param>
    /// <returns>
    /// A cold signal giving one value after the document has changed; unsubscribing cancels building and importing the pages. It fails with
    /// <see cref="NotSupportedException"/> when the manager cannot make pages from pictures.
    /// </returns>
    IObservable<RxVoid> InsertImages(int index, PageImageSource[] images) => Signal.Throw<RxVoid>(new NotSupportedException());

    /// <summary>Writes the selected pages to a new PDF without changing the open document.</summary>
    /// <param name="pages">Zero-based indexes in output order.</param>
    /// <param name="destination">The stream receiving the new PDF.</param>
    /// <returns>A cold signal giving one value after the file has been written; unsubscribing cancels page reads and writing.</returns>
    IObservable<RxVoid> Extract(ReadOnlyMemory<int> pages, Stream destination);

    /// <summary>Writes the selected pages to a new PDF without changing the open document, and says what the copy could not keep.</summary>
    /// <param name="pages">Zero-based indexes in output order.</param>
    /// <param name="destination">The stream receiving the new PDF.</param>
    /// <param name="report">Receives the printed pages of a preseparated document whose separations were not kept together.</param>
    /// <returns>A cold signal giving one value after the file has been written; unsubscribing cancels page reads and writing.</returns>
    IObservable<RxVoid> Extract(ReadOnlyMemory<int> pages, Stream destination, PageEditReport report) => Extract(pages, destination);

    /// <summary>Reverts the latest edit.</summary>
    /// <returns>A cold signal giving <see langword="true"/> when an edit was reverted; unsubscribing cancels before the edit is reverted.</returns>
    IObservable<bool> Undo();

    /// <summary>Reapplies the latest undone edit.</summary>
    /// <returns>A cold signal giving <see langword="true"/> when an edit was reapplied; unsubscribing cancels before the edit is reapplied.</returns>
    IObservable<bool> Redo();
}
