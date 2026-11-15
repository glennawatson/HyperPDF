// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;

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
    public ValueTask<PageObjectInfo[]> ReadObjectsAsync(int pageIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReadObjectsAsync(_owner, pageIndex, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask MoveAsync(int pageIndex, int objectIndex, float deltaX, float deltaY, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.MoveAsync(_owner, pageIndex, objectIndex, new(deltaX, deltaY), cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask TransformAsync(int pageIndex, int objectIndex, System.Numerics.Matrix3x2 transformation, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.TransformAsync(_owner, pageIndex, objectIndex, transformation, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask TransformManyAsync(int pageIndex, PageObjectChange[] changes, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.TransformManyAsync(_owner, pageIndex, changes, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DeleteManyAsync(int pageIndex, int[] objectIndexes, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.DeleteManyAsync(_owner, pageIndex, objectIndexes, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask CopyAsync(int pageIndex, int objectIndex, int targetPageIndex, HyperPdf.Core.Geometry.PagePoint offset, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.CopyAsync(_owner, pageIndex, objectIndex, targetPageIndex, offset, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask BringToFrontAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.BringToFrontAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask BringForwardAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.StepAsync(_owner, pageIndex, objectIndex, false, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask SendBackwardAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.StepAsync(_owner, pageIndex, objectIndex, true, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask SendToBackAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.SendToBackAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask SetColorsManyAsync(int pageIndex, int[] objectIndexes, PageObjectColors colors, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.SetColorsManyAsync(_owner, pageIndex, objectIndexes, colors, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask ReplacePictureAsync(int pageIndex, int objectIndex, ReadOnlyMemory<byte> file, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReplacePictureAsync(_owner, pageIndex, objectIndex, file, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask CropPictureAsync(int pageIndex, int objectIndex, PageMargins shares, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.CropPictureAsync(_owner, pageIndex, objectIndex, shares, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<PageObjectFile> ExtractPictureAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ExtractPictureAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask SetLookManyAsync(int pageIndex, int[] objectIndexes, PageObjectLook look, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.SetLookManyAsync(_owner, pageIndex, objectIndexes, look, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask SetColorsAsync(int pageIndex, int objectIndex, PageObjectColors colors, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.SetColorsAsync(_owner, pageIndex, objectIndex, colors, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<PageObjectStyle> ReadStyleAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReadStyleAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask AddAsync(int pageIndex, PageObjectDraft draft, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentAdding.AddAsync(_owner, pageIndex, in draft, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask AddPictureAsync(int pageIndex, HyperPdf.Core.Geometry.PageRect area, ReadOnlyMemory<byte> file, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentAdding.AddPictureAsync(_owner, pageIndex, area, file, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DeleteAsync(int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.DeleteAsync(_owner, pageIndex, objectIndex, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask ReplaceTextAsync(int pageIndex, int objectIndex, string text, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.ReplaceTextAsync(_owner, pageIndex, objectIndex, text, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask RestyleTextAsync(int pageIndex, int objectIndex, PageTextStyle style, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageContentEditing.RestyleTextAsync(_owner, pageIndex, objectIndex, style, cancellationToken);
}
