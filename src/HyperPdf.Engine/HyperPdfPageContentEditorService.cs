// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;

namespace HyperPdf.Engine;

/// <summary>Provides IPageContentEditor through the owning document.</summary>
internal sealed class HyperPdfPageContentEditorService : IPageContentEditor
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageContentEditorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfPageContentEditorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<PageObjectInfo[]> ReadObjects(int pageIndex) =>
        LibrarySignals.Run(token => ReadObjectsAsync(pageIndex, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Move(int pageIndex, int objectIndex, float deltaX, float deltaY) =>
        LibrarySignals.Run(token => MoveAsync(pageIndex, objectIndex, deltaX, deltaY, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Transform(int pageIndex, int objectIndex, System.Numerics.Matrix3x2 transformation) =>
        LibrarySignals.Run(token => TransformAsync(pageIndex, objectIndex, transformation, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> TransformMany(int pageIndex, PageObjectChange[] changes) =>
        LibrarySignals.Run(token => TransformManyAsync(pageIndex, changes, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> DeleteMany(int pageIndex, int[] objectIndexes) =>
        LibrarySignals.Run(token => DeleteManyAsync(pageIndex, objectIndexes, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Copy(int pageIndex, int objectIndex, int targetPageIndex, HyperPdf.Core.Geometry.PagePoint offset) =>
        LibrarySignals.Run(token => CopyAsync(pageIndex, objectIndex, targetPageIndex, offset, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> BringToFront(int pageIndex, int objectIndex) =>
        LibrarySignals.Run(token => BringToFrontAsync(pageIndex, objectIndex, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> BringForward(int pageIndex, int objectIndex) =>
        LibrarySignals.Run(token => BringForwardAsync(pageIndex, objectIndex, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SendBackward(int pageIndex, int objectIndex) =>
        LibrarySignals.Run(token => SendBackwardAsync(pageIndex, objectIndex, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SendToBack(int pageIndex, int objectIndex) =>
        LibrarySignals.Run(token => SendToBackAsync(pageIndex, objectIndex, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SetColorsMany(int pageIndex, int[] objectIndexes, PageObjectColors colors) =>
        LibrarySignals.Run(token => SetColorsManyAsync(pageIndex, objectIndexes, colors, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> ReplacePicture(int pageIndex, int objectIndex, ReadOnlyMemory<byte> file) =>
        LibrarySignals.Run(token => ReplacePictureAsync(pageIndex, objectIndex, file, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> CropPicture(int pageIndex, int objectIndex, PageMargins shares) =>
        LibrarySignals.Run(token => CropPictureAsync(pageIndex, objectIndex, shares, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<PageObjectFile> ExtractPicture(int pageIndex, int objectIndex) =>
        LibrarySignals.Run(token => ExtractPictureAsync(pageIndex, objectIndex, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SetLookMany(int pageIndex, int[] objectIndexes, PageObjectLook look) =>
        LibrarySignals.Run(token => SetLookManyAsync(pageIndex, objectIndexes, look, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SetColors(int pageIndex, int objectIndex, PageObjectColors colors) =>
        LibrarySignals.Run(token => SetColorsAsync(pageIndex, objectIndex, colors, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<PageObjectStyle> ReadStyle(int pageIndex, int objectIndex) =>
        LibrarySignals.Run(token => ReadStyleAsync(pageIndex, objectIndex, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Add(int pageIndex, PageObjectDraft draft) =>
        LibrarySignals.Run(token => AddAsync(pageIndex, draft, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> AddPicture(int pageIndex, HyperPdf.Core.Geometry.PageRect area, ReadOnlyMemory<byte> file) =>
        LibrarySignals.Run(token => AddPictureAsync(pageIndex, area, file, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Delete(int pageIndex, int objectIndex) =>
        LibrarySignals.Run(token => DeleteAsync(pageIndex, objectIndex, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> ReplaceText(int pageIndex, int objectIndex, string text) =>
        LibrarySignals.Run(token => ReplaceTextAsync(pageIndex, objectIndex, text, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> RestyleText(int pageIndex, int objectIndex, PageTextStyle style) =>
        LibrarySignals.Run(token => RestyleTextAsync(pageIndex, objectIndex, style, token));

    /// <summary>Runs ReadObjects against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask<PageObjectInfo[]> ReadObjectsAsync(int pageIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReadObjectsAsync(_owner, pageIndex, cancellationToken);

    /// <summary>Runs Move against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="deltaX">The deltaX.</param>
    /// <param name="deltaY">The deltaY.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask MoveAsync(int pageIndex, int objectIndex, float deltaX, float deltaY, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.MoveAsync(_owner, pageIndex, objectIndex, new(deltaX, deltaY), cancellationToken);

    /// <summary>Runs Transform against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="transformation">The transformation.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask TransformAsync(int pageIndex, int objectIndex, System.Numerics.Matrix3x2 transformation, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.TransformAsync(_owner, pageIndex, objectIndex, transformation, cancellationToken);

    /// <summary>Runs TransformMany against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask TransformManyAsync(int pageIndex, PageObjectChange[] changes, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.TransformManyAsync(_owner, pageIndex, changes, cancellationToken);

    /// <summary>Runs DeleteMany against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndexes">The objectIndexes.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask DeleteManyAsync(int pageIndex, int[] objectIndexes, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.DeleteManyAsync(_owner, pageIndex, objectIndexes, cancellationToken);

    /// <summary>Runs Copy against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="targetPageIndex">The targetPageIndex.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask CopyAsync(int pageIndex, int objectIndex, int targetPageIndex, HyperPdf.Core.Geometry.PagePoint offset, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.CopyAsync(_owner, pageIndex, objectIndex, targetPageIndex, offset, cancellationToken);

    /// <summary>Runs BringToFront against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask BringToFrontAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.BringToFrontAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <summary>Runs BringForward against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask BringForwardAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.StepAsync(_owner, pageIndex, objectIndex, false, cancellationToken);

    /// <summary>Runs SendBackward against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask SendBackwardAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.StepAsync(_owner, pageIndex, objectIndex, true, cancellationToken);

    /// <summary>Runs SendToBack against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask SendToBackAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.SendToBackAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <summary>Runs SetColorsMany against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndexes">The objectIndexes.</param>
    /// <param name="colors">The colors.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask SetColorsManyAsync(int pageIndex, int[] objectIndexes, PageObjectColors colors, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.SetColorsManyAsync(_owner, pageIndex, objectIndexes, colors, cancellationToken);

    /// <summary>Runs ReplacePicture against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask ReplacePictureAsync(int pageIndex, int objectIndex, ReadOnlyMemory<byte> file, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReplacePictureAsync(_owner, pageIndex, objectIndex, file, cancellationToken);

    /// <summary>Runs CropPicture against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="shares">The shares.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask CropPictureAsync(int pageIndex, int objectIndex, PageMargins shares, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.CropPictureAsync(_owner, pageIndex, objectIndex, shares, cancellationToken);

    /// <summary>Runs ExtractPicture against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask<PageObjectFile> ExtractPictureAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ExtractPictureAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <summary>Runs SetLookMany against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndexes">The objectIndexes.</param>
    /// <param name="look">The look.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask SetLookManyAsync(int pageIndex, int[] objectIndexes, PageObjectLook look, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.SetLookManyAsync(_owner, pageIndex, objectIndexes, look, cancellationToken);

    /// <summary>Runs SetColors against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="colors">The colors.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask SetColorsAsync(int pageIndex, int objectIndex, PageObjectColors colors, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.SetColorsAsync(_owner, pageIndex, objectIndex, colors, cancellationToken);

    /// <summary>Runs ReadStyle against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask<PageObjectStyle> ReadStyleAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReadStyleAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <summary>Runs Add against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="draft">The draft.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask AddAsync(int pageIndex, in PageObjectDraft draft, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentAdding.AddAsync(_owner, pageIndex, in draft, cancellationToken);

    /// <summary>Runs AddPicture against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="area">The area.</param>
    /// <param name="file">The file.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask AddPictureAsync(int pageIndex, HyperPdf.Core.Geometry.PageRect area, ReadOnlyMemory<byte> file, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentAdding.AddPictureAsync(_owner, pageIndex, area, file, cancellationToken);

    /// <summary>Runs Delete against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask DeleteAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.DeleteAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <summary>Runs ReplaceText against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask ReplaceTextAsync(int pageIndex, int objectIndex, string text, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReplaceTextAsync(_owner, pageIndex, objectIndex, text, cancellationToken);

    /// <summary>Runs RestyleText against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="objectIndex">The objectIndex.</param>
    /// <param name="style">The style.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask RestyleTextAsync(int pageIndex, int objectIndex, PageTextStyle style, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.RestyleTextAsync(_owner, pageIndex, objectIndex, style, cancellationToken);
}
