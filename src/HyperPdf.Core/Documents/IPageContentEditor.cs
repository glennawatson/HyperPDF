// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;

namespace HyperPdf.Core.Documents;

/// <summary>Reads the text, pictures and drawings a page is made of and changes them in place, one undoable step each.</summary>
public interface IPageContentEditor : IDocumentFeature
{
    /// <summary>Lists what a page is made of, in the order it is drawn.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>A cold signal giving the objects that have a place on the page once; unsubscribing cancels the work.</returns>
    IObservable<PageObjectInfo[]> ReadObjects(int pageIndex);

    /// <summary>Moves an object.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <param name="deltaX">How far to move right, in page space points.</param>
    /// <param name="deltaY">How far to move down, in page space points.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> Move(int pageIndex, int objectIndex, float deltaX, float deltaY);

    /// <summary>Resizes, turns or flips an object by a transformation in page space, which is how the handles on the picked object work.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <param name="transformation">The change in page space points, with the page's top left corner as the origin and y going down.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> Transform(int pageIndex, int objectIndex, System.Numerics.Matrix3x2 transformation);

    /// <summary>Moves, resizes or turns several objects of one page, each by its own change, as one undoable step.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="changes">The objects and what happens to each.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> TransformMany(int pageIndex, PageObjectChange[] changes);

    /// <summary>Removes several objects of one page as one undoable step.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The objects' numbers from <see cref="ReadObjects"/>.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> DeleteMany(int pageIndex, int[] objectIndexes);

    /// <summary>Paints another copy of an object on a page, on top of what is there. The copy keeps the object's colours, line style, clipping and transparency.</summary>
    /// <param name="pageIndex">The zero based index of the page the object is on.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <param name="targetPageIndex">The zero based index of the page the copy goes on; the same page makes a duplicate.</param>
    /// <param name="offset">How far right and down from the original's place the copy goes, in page space points.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> Copy(int pageIndex, int objectIndex, int targetPageIndex, HyperPdf.Core.Geometry.PagePoint offset);

    /// <summary>Moves an object in front of everything else on its page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> BringToFront(int pageIndex, int objectIndex);

    /// <summary>Moves an object one step forward, over the object that was in front of it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> BringForward(int pageIndex, int objectIndex);

    /// <summary>Moves an object one step back, behind the object that was behind it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> SendBackward(int pageIndex, int objectIndex);

    /// <summary>Moves an object behind everything else on its page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> SendToBack(int pageIndex, int objectIndex);

    /// <summary>Changes the fill or stroke colour of several objects of one page as one undoable step.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The objects' numbers from <see cref="ReadObjects"/>.</param>
    /// <param name="colors">What to change.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> SetColorsMany(int pageIndex, int[] objectIndexes, PageObjectColors colors);

    /// <summary>Draws another picture in place of a picture of the page, in the same frame.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The picture's number from <see cref="ReadObjects"/>.</param>
    /// <param name="file">The new picture's file: a JPEG, PNG, GIF, BMP or TIFF.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    /// <exception cref="NotSupportedException">The document cannot change this.</exception>
    /// <exception cref="InvalidOperationException">The object is not a picture.</exception>
    /// <exception cref="InvalidDataException">The file is not a picture that can be read.</exception>
    IObservable<RxVoid> ReplacePicture(int pageIndex, int objectIndex, ReadOnlyMemory<byte> file);

    /// <summary>Crops a picture of the page without changing the stored picture: the part cut off is clipped away when the page is drawn.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The picture's number from <see cref="ReadObjects"/>.</param>
    /// <param name="shares">The shares of the picture's width and height cut off each side, each from 0 up to but not including 1.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    /// <exception cref="NotSupportedException">The document cannot change this.</exception>
    /// <exception cref="InvalidOperationException">The object is not a picture.</exception>
    IObservable<RxVoid> CropPicture(int pageIndex, int objectIndex, PageMargins shares);

    /// <summary>Makes a file from a picture of the page: the original JPEG when the picture is one, and a PNG of its pixels otherwise.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The picture's number from <see cref="ReadObjects"/>.</param>
    /// <returns>A cold signal giving the file once; unsubscribing cancels the work.</returns>
    /// <exception cref="NotSupportedException">The document cannot do this.</exception>
    /// <exception cref="InvalidOperationException">The object is not a picture, or its pixels cannot be read.</exception>
    IObservable<PageObjectFile> ExtractPicture(int pageIndex, int objectIndex);

    /// <summary>
    /// Changes how several objects are drawn besides their colours, in one undo step: the dashes, ends and corners of their lines,
    /// how solid they are and how their colours mix with what is under them.
    /// </summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The objects' numbers from <see cref="ReadObjects"/>.</param>
    /// <param name="look">What to change.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    /// <exception cref="NotSupportedException">The document cannot change this.</exception>
    IObservable<RxVoid> SetLookMany(int pageIndex, int[] objectIndexes, PageObjectLook look);

    /// <summary>Changes the fill or stroke colour of an object.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <param name="colors">What to change.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> SetColors(int pageIndex, int objectIndex, PageObjectColors colors);

    /// <summary>Reads how an object is painted.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <returns>A cold signal giving the object's colours and line width once; unsubscribing cancels the work.</returns>
    IObservable<PageObjectStyle> ReadStyle(int pageIndex, int objectIndex);

    /// <summary>Writes a new rectangle, ellipse, line or line of text into the page's own content.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="draft">What to add and where.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> Add(int pageIndex, PageObjectDraft draft);

    /// <summary>Places a picture on the page, as large as fits the area while keeping its shape, in the middle of it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="area">The area the picture may fill, in page space.</param>
    /// <param name="file">The picture file: a JPEG, PNG, GIF, BMP or TIFF.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> AddPicture(int pageIndex, HyperPdf.Core.Geometry.PageRect area, ReadOnlyMemory<byte> file);

    /// <summary>Removes an object.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    IObservable<RxVoid> Delete(int pageIndex, int objectIndex);

    /// <summary>Replaces the text of a text object with new text at the same place, size and colour.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <param name="text">The new text.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    /// <exception cref="InvalidOperationException">The object is not text.</exception>
    IObservable<RxVoid> ReplaceText(int pageIndex, int objectIndex, string text);

    /// <summary>
    /// Replaces the text of the lines of a paragraph with new text wrapped to the width the paragraph had, starting where its first line
    /// started: a longer text takes more lines, and a shorter one takes fewer. It is one undo step.
    /// </summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The numbers, from <see cref="ReadObjects"/>, of the text objects that are the paragraph's lines.</param>
    /// <param name="text">The new text; a line break starts a new line.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    /// <exception cref="InvalidOperationException">An object is not text.</exception>
    IObservable<RxVoid> ReflowParagraph(int pageIndex, int[] objectIndexes, string text);

    /// <summary>Sets a text object in another font and size, keeping its words, place and colour. The text is written in a standard font.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number from <see cref="ReadObjects"/>.</param>
    /// <param name="style">The font and size.</param>
    /// <returns>A cold signal giving a task completing after the page has changed once; unsubscribing cancels the work.</returns>
    /// <exception cref="InvalidOperationException">The object is not text.</exception>
    IObservable<RxVoid> RestyleText(int pageIndex, int objectIndex, PageTextStyle style);
}
