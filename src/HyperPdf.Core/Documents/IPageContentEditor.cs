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

    /// <summary>Resizes, turns or flips an object by a transformation in page space, which is how the handles on the picked object work.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="transformation">The change in page space points, with the page's top left corner as the origin and y going down.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask TransformAsync(int pageIndex, int objectIndex, System.Numerics.Matrix3x2 transformation, CancellationToken cancellationToken);

    /// <summary>Moves, resizes or turns several objects of one page, each by its own change, as one undoable step.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="changes">The objects and what happens to each.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask TransformManyAsync(int pageIndex, PageObjectChange[] changes, CancellationToken cancellationToken);

    /// <summary>Removes several objects of one page as one undoable step.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The objects' numbers from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask DeleteManyAsync(int pageIndex, int[] objectIndexes, CancellationToken cancellationToken);

    /// <summary>Paints another copy of an object on a page, on top of what is there. The copy keeps the object's colours, line style, clipping and transparency.</summary>
    /// <param name="pageIndex">The zero based index of the page the object is on.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="targetPageIndex">The zero based index of the page the copy goes on; the same page makes a duplicate.</param>
    /// <param name="offset">How far right and down from the original's place the copy goes, in page space points.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask CopyAsync(int pageIndex, int objectIndex, int targetPageIndex, HyperPdf.Core.Geometry.PagePoint offset, CancellationToken cancellationToken);

    /// <summary>Moves an object in front of everything else on its page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask BringToFrontAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken);

    /// <summary>Moves an object one step forward, over the object that was in front of it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask BringForwardAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken);

    /// <summary>Moves an object one step back, behind the object that was behind it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask SendBackwardAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken);

    /// <summary>Moves an object behind everything else on its page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask SendToBackAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken);

    /// <summary>Changes the fill or stroke colour of several objects of one page as one undoable step.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The objects' numbers from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="colors">What to change.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask SetColorsManyAsync(int pageIndex, int[] objectIndexes, PageObjectColors colors, CancellationToken cancellationToken);

    /// <summary>Draws another picture in place of a picture of the page, in the same frame.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The picture's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="file">The new picture's file: a JPEG, PNG, GIF, BMP or TIFF.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="NotSupportedException">The document cannot change this.</exception>
    /// <exception cref="InvalidOperationException">The object is not a picture.</exception>
    /// <exception cref="InvalidDataException">The file is not a picture that can be read.</exception>
    ValueTask ReplacePictureAsync(int pageIndex, int objectIndex, ReadOnlyMemory<byte> file, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Crops a picture of the page without changing the stored picture: the part cut off is clipped away when the page is drawn.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The picture's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="shares">The shares of the picture's width and height cut off each side, each from 0 up to but not including 1.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="NotSupportedException">The document cannot change this.</exception>
    /// <exception cref="InvalidOperationException">The object is not a picture.</exception>
    ValueTask CropPictureAsync(int pageIndex, int objectIndex, PageMargins shares, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Makes a file from a picture of the page: the original JPEG when the picture is one, and a PNG of its pixels otherwise.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The picture's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels reading the page.</param>
    /// <returns>The file.</returns>
    /// <exception cref="NotSupportedException">The document cannot do this.</exception>
    /// <exception cref="InvalidOperationException">The object is not a picture, or its pixels cannot be read.</exception>
    ValueTask<PageObjectFile> ExtractPictureAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Changes how several objects are drawn besides their colours: the dashes, ends and corners of their lines and how solid they are, in one undo step.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The objects' numbers from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="look">What to change.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="NotSupportedException">The document cannot change this.</exception>
    /// <exception cref="InvalidOperationException">An object is text, which cannot be made see-through.</exception>
    ValueTask SetLookManyAsync(int pageIndex, int[] objectIndexes, PageObjectLook look, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Changes the fill or stroke colour of an object.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="colors">What to change.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask SetColorsAsync(int pageIndex, int objectIndex, PageObjectColors colors, CancellationToken cancellationToken);

    /// <summary>Reads how an object is painted.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels reading the page.</param>
    /// <returns>The object's colours and line width.</returns>
    ValueTask<PageObjectStyle> ReadStyleAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken);

    /// <summary>Writes a new rectangle, ellipse, line or line of text into the page's own content.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="draft">What to add and where.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask AddAsync(int pageIndex, PageObjectDraft draft, CancellationToken cancellationToken);

    /// <summary>Places a picture on the page, as large as fits the area while keeping its shape, in the middle of it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="area">The area the picture may fill, in page space.</param>
    /// <param name="file">The picture file: a JPEG, PNG, GIF, BMP or TIFF.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    ValueTask AddPictureAsync(int pageIndex, HyperPdf.Core.Geometry.PageRect area, ReadOnlyMemory<byte> file, CancellationToken cancellationToken);

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

    /// <summary>Sets a text object in another font and size, keeping its words, place and colour. The text is written in a standard font.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjectsAsync"/>.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="InvalidOperationException">The object is not text.</exception>
    ValueTask RestyleTextAsync(int pageIndex, int objectIndex, PageTextStyle style, CancellationToken cancellationToken);
}
