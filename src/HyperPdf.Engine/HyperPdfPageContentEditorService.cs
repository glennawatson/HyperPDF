// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

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
        Signal.FromAsync(token => HyperPdfDocumentPageContentEditing.ReadObjectsAsync(_owner, pageIndex, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Move(int pageIndex, int objectIndex, float deltaX, float deltaY) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.MoveAsync(_owner, pageIndex, objectIndex, new(deltaX, deltaY), token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Transform(int pageIndex, int objectIndex, System.Numerics.Matrix3x2 transformation) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.TransformAsync(_owner, pageIndex, objectIndex, transformation, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> TransformMany(int pageIndex, PageObjectChange[] changes) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.TransformManyAsync(_owner, pageIndex, changes, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> DeleteMany(int pageIndex, int[] objectIndexes) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.DeleteManyAsync(_owner, pageIndex, objectIndexes, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Copy(int pageIndex, int objectIndex, int targetPageIndex, HyperPdf.Core.Geometry.PagePoint offset) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.CopyAsync(_owner, pageIndex, objectIndex, targetPageIndex, offset, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> BringToFront(int pageIndex, int objectIndex) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.BringToFrontAsync(_owner, pageIndex, objectIndex, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> BringForward(int pageIndex, int objectIndex) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.StepAsync(_owner, pageIndex, objectIndex, false, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SendBackward(int pageIndex, int objectIndex) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.StepAsync(_owner, pageIndex, objectIndex, true, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SendToBack(int pageIndex, int objectIndex) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.SendToBackAsync(_owner, pageIndex, objectIndex, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SetColorsMany(int pageIndex, int[] objectIndexes, PageObjectColors colors) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.SetColorsManyAsync(_owner, pageIndex, objectIndexes, colors, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> ReplacePicture(int pageIndex, int objectIndex, ReadOnlyMemory<byte> file) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.ReplacePictureAsync(_owner, pageIndex, objectIndex, file, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> CropPicture(int pageIndex, int objectIndex, PageMargins shares) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.CropPictureAsync(_owner, pageIndex, objectIndex, shares, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<PageObjectFile> ExtractPicture(int pageIndex, int objectIndex) =>
        Signal.FromAsync(token => HyperPdfDocumentPageContentEditing.ExtractPictureAsync(_owner, pageIndex, objectIndex, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SetLookMany(int pageIndex, int[] objectIndexes, PageObjectLook look) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.SetLookManyAsync(_owner, pageIndex, objectIndexes, look, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> SetColors(int pageIndex, int objectIndex, PageObjectColors colors) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.SetColorsAsync(_owner, pageIndex, objectIndex, colors, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<PageObjectStyle> ReadStyle(int pageIndex, int objectIndex) =>
        Signal.FromAsync(token => HyperPdfDocumentPageContentEditing.ReadStyleAsync(_owner, pageIndex, objectIndex, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Add(int pageIndex, PageObjectDraft draft) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentAdding.AddAsync(_owner, pageIndex, in draft, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> AddPicture(int pageIndex, HyperPdf.Core.Geometry.PageRect area, ReadOnlyMemory<byte> file) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentAdding.AddPictureAsync(_owner, pageIndex, area, file, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> Delete(int pageIndex, int objectIndex) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.DeleteAsync(_owner, pageIndex, objectIndex, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> ReplaceText(int pageIndex, int objectIndex, string text) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.ReplaceTextAsync(_owner, pageIndex, objectIndex, text, token).ConfigureAwait(false);
            return RxVoid.Default;
        });

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> RestyleText(int pageIndex, int objectIndex, PageTextStyle style) =>
        Signal.FromAsync(async token =>
        {
            await HyperPdfDocumentPageContentEditing.RestyleTextAsync(_owner, pageIndex, objectIndex, style, token).ConfigureAwait(false);
            return RxVoid.Default;
        });
}
