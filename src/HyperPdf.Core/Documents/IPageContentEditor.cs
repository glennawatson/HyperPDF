// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Reads the text, pictures and drawings a page is made of and changes them in place, one undoable step each.</summary>
public interface IPageContentEditor : IDocumentFeature
{
    /// <summary>Lists what a page is made of, in the order it is drawn.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels reading the page.</param>
    /// <returns>The objects that have a place on the page.</returns>
    ValueTask<PageObjectInfo[]> ReadObjectsAsync(int pageIndex, CancellationToken cancellationToken);

    /// <summary>Moves an object.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="deltaX">How far to move right, in page space points.</param>
    /// <param name="deltaY">How far to move down, in page space points.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask MoveAsync(int pageIndex, int objectIndex, float deltaX, float deltaY, CancellationToken cancellationToken);

    /// <summary>Removes an object.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask DeleteAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken);

    /// <summary>Replaces the text of a text object with new text at the same place, size and colour.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="text">The new text.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="InvalidOperationException">The object is not text.</exception>
    ValueTask ReplaceTextAsync(int pageIndex, int objectIndex, string text, CancellationToken cancellationToken);
}
