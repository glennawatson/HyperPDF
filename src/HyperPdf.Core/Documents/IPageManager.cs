// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// <param name="cancellationToken">Cancels page resource reads and the edit.</param>
    /// <returns>A task completing after the document has changed.</returns>
    ValueTask ApplyAsync(PageEditRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes, rotates, moves or copies selected pages as one undo step, and says what the edit could not keep.</summary>
    /// <param name="request">The operation and zero-based indexes.</param>
    /// <param name="report">Receives the printed pages of a preseparated document whose separations were not kept together.</param>
    /// <param name="cancellationToken">Cancels page resource reads and the edit.</param>
    /// <returns>A task completing after the document has changed.</returns>
    ValueTask ApplyAsync(PageEditRequest request, PageEditReport report, CancellationToken cancellationToken) => ApplyAsync(request, cancellationToken);

    /// <summary>Inserts every page of the selected files as one undo step, retaining forms, outlines and links.</summary>
    /// <param name="index">The first inserted page's zero-based index, from zero to the current page count.</param>
    /// <param name="files">PDF files in insertion order.</param>
    /// <param name="cancellationToken">Cancels opening and importing the files.</param>
    /// <returns>A task completing after the document has changed.</returns>
    ValueTask InsertAsync(int index, string[] files, CancellationToken cancellationToken);

    /// <summary>Inserts every page of the selected files as one undo step, and says what the copy could not keep.</summary>
    /// <param name="index">The first inserted page's zero-based index, from zero to the current page count.</param>
    /// <param name="files">PDF files in insertion order.</param>
    /// <param name="report">Receives what the copy could not keep, such as trap networks that no longer matched their pages.</param>
    /// <param name="cancellationToken">Cancels opening and importing the files.</param>
    /// <returns>A task completing after the document has changed.</returns>
    ValueTask InsertAsync(int index, string[] files, PageEditReport report, CancellationToken cancellationToken) => InsertAsync(index, files, cancellationToken);

    /// <summary>Writes the selected pages to a new PDF without changing the open document.</summary>
    /// <param name="pages">Zero-based indexes in output order.</param>
    /// <param name="destination">The stream receiving the new PDF.</param>
    /// <param name="cancellationToken">Cancels page reads and writing.</param>
    /// <returns>A task completing after the file has been written.</returns>
    ValueTask ExtractAsync(ReadOnlyMemory<int> pages, Stream destination, CancellationToken cancellationToken);

    /// <summary>Writes the selected pages to a new PDF without changing the open document, and says what the copy could not keep.</summary>
    /// <param name="pages">Zero-based indexes in output order.</param>
    /// <param name="destination">The stream receiving the new PDF.</param>
    /// <param name="report">Receives the printed pages of a preseparated document whose separations were not kept together.</param>
    /// <param name="cancellationToken">Cancels page reads and writing.</param>
    /// <returns>A task completing after the file has been written.</returns>
    ValueTask ExtractAsync(ReadOnlyMemory<int> pages, Stream destination, PageEditReport report, CancellationToken cancellationToken) =>
        ExtractAsync(pages, destination, cancellationToken);

    /// <summary>Reverts the latest edit.</summary>
    /// <param name="cancellationToken">Cancels before the edit is reverted.</param>
    /// <returns>True when an edit was reverted.</returns>
    ValueTask<bool> UndoAsync(CancellationToken cancellationToken);

    /// <summary>Reapplies the latest undone edit.</summary>
    /// <param name="cancellationToken">Cancels before the edit is reapplied.</param>
    /// <returns>True when an edit was reapplied.</returns>
    ValueTask<bool> RedoAsync(CancellationToken cancellationToken);
}
