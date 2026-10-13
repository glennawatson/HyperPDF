// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Edits the bookmarks of an open document through the library.</summary>
internal sealed class HyperPdfOutlineEditorService : IOutlineEditor
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfOutlineEditorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfOutlineEditorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> AddBookmarkAsync(int parentId, int index, string title, int pageIndex, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(_owner, new AddStep(parentId, index, title, pageIndex), HyperPdfEditPrefetch.OutlineAsync, Add, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> RemoveBookmarkAsync(int id, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new IdStep(id),
            HyperPdfEditPrefetch.OutlineAsync,
            static (document, step) => PdfDocumentOutlineEditing.RemoveBookmark(document, Find(document, step.Id)),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> RenameBookmarkAsync(int id, string title, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new TitleStep(id, title),
            HyperPdfEditPrefetch.OutlineAsync,
            static (document, step) => PdfDocumentOutlineEditing.RenameBookmark(document, Find(document, step.Id), step.Title),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> MoveBookmarkAsync(int id, int newParentId, int index, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new MoveStep(id, newParentId, index),
            HyperPdfEditPrefetch.OutlineAsync,
            static (document, step) => PdfDocumentOutlineEditing.MoveBookmark(document, Find(document, step.Id), Find(document, step.NewParentId), step.Index),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> SetBookmarkOpenAsync(int id, bool open, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new OpenStep(id, open),
            HyperPdfEditPrefetch.OutlineAsync,
            static (document, step) => PdfDocumentOutlineEditing.SetBookmarkOpen(document, Find(document, step.Id), step.Open),
            cancellationToken);

    /// <summary>Adds a bookmark that goes to the top of a page.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="step">The arguments.</param>
    private static void Add(PdfDocument document, AddStep step)
    {
        var target = PdfBookmarkTarget.ToPage(PdfExplicitDestination.TopOfPage(step.PageIndex));
        _ = PdfDocumentOutlineEditing.AddBookmark(document, Find(document, step.ParentId), step.Index, new(step.Title, target));
    }

    /// <summary>Finds the entry with an object number.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="number">The object number; 0 stands for the top level.</param>
    /// <returns>The entry's object, or an invalid id for the top level.</returns>
    /// <exception cref="ArgumentException">The document has no such entry.</exception>
    private static PdfObjectId Find(PdfDocument document, int number)
    {
        if (number == 0)
        {
            return default;
        }

        var pending = new Stack<PdfOutlineItem>(PdfDocumentNavigation.GetOutline(document));
        while (pending.TryPop(out var item))
        {
            if (item.Id.Number == number)
            {
                return item.Id;
            }

            foreach (var child in item.Children)
            {
                pending.Push(child);
            }
        }

        throw new ArgumentException("That bookmark is no longer in the document.", nameof(number));
    }

    /// <summary>The arguments of an add.</summary>
    /// <param name="ParentId">The parent entry.</param>
    /// <param name="Index">The position.</param>
    /// <param name="Title">The title.</param>
    /// <param name="PageIndex">The page.</param>
    private readonly record struct AddStep(int ParentId, int Index, string Title, int PageIndex);

    /// <summary>The arguments of an edit that names one entry.</summary>
    /// <param name="Id">The entry.</param>
    private readonly record struct IdStep(int Id);

    /// <summary>The arguments of a rename.</summary>
    /// <param name="Id">The entry.</param>
    /// <param name="Title">The title.</param>
    private readonly record struct TitleStep(int Id, string Title);

    /// <summary>The arguments of a move.</summary>
    /// <param name="Id">The entry.</param>
    /// <param name="NewParentId">The new parent.</param>
    /// <param name="Index">The position.</param>
    private readonly record struct MoveStep(int Id, int NewParentId, int Index);

    /// <summary>The arguments of an open or close.</summary>
    /// <param name="Id">The entry.</param>
    /// <param name="Open">Whether it is open.</param>
    private readonly record struct OpenStep(int Id, bool Open);
}
