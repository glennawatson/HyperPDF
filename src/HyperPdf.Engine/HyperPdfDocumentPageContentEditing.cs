// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Engine;

/// <summary>Reads and changes the objects a page is made of, over the document's owned state.</summary>
internal static class HyperPdfDocumentPageContentEditing
{
    /// <summary>The message shown when the document changed while an edit was being prepared.</summary>
    private const string ChangedMessage = "The document changed while the edit was being prepared. Try again.";

    /// <summary>What an edit does to an object.</summary>
    private enum ObjectEditKind
    {
        /// <summary>Moves the object.</summary>
        Move = 0,

        /// <summary>Removes the object.</summary>
        Delete = 1,

        /// <summary>Replaces the words of a text object.</summary>
        ReplaceText = 2,
    }

    /// <summary>Lists what a page is made of.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels reading the page.</param>
    /// <returns>The objects that have a place on the page.</returns>
    internal static async ValueTask<PageObjectInfo[]> ReadObjectsAsync(HyperPdfDocument self, int pageIndex, CancellationToken cancellationToken)
    {
        var content = await PdfPageContentReader.ReadAsync(self.Document, pageIndex, cancellationToken).ConfigureAwait(false);
        var page = content.Page;
        if (page is null)
        {
            return [];
        }

        // The result is counted first so it is made once, at its exact size.
        var items = content.Objects;
        var count = 0;
        for (var i = 0; i < items.Count; i++)
        {
            count += IsListed(items[i]) ? 1 : 0;
        }

        var objects = new PageObjectInfo[count];
        var next = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!IsListed(item))
            {
                continue;
            }

            objects[next] = new(item.Index, ToKind(item.Kind), LinkTargets.ToPageRect(page.ToPhysicalRectangle(item.Bounds)), item is PdfTextObject run ? run.Text : string.Empty);
            next++;
        }

        return objects;
    }

    /// <summary>Moves an object.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="delta">How far to move, in page space.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask MoveAsync(HyperPdfDocument self, int pageIndex, int objectIndex, PagePoint delta, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.Move, objectIndex, delta, null), cancellationToken);

    /// <summary>Removes an object.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask DeleteAsync(HyperPdfDocument self, int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.Delete, objectIndex, default, null), cancellationToken);

    /// <summary>Replaces the text of a text object.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="text">The new text.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    internal static ValueTask ReplaceTextAsync(HyperPdfDocument self, int pageIndex, int objectIndex, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        return EditAsync(self, pageIndex, new(ObjectEditKind.ReplaceText, objectIndex, default, text), cancellationToken);
    }

    /// <summary>Tells whether an object has a place on the page that can be picked.</summary>
    /// <param name="item">The object.</param>
    /// <returns><see langword="true"/> for an object that is not removed, has a finite box and, for text, has words.</returns>
    private static bool IsListed(PdfPageObject item)
    {
        if (item.IsDeleted || item.Kind == PdfPageObjectKind.None || !float.IsFinite(item.Bounds.Width) || !float.IsFinite(item.Bounds.Height))
        {
            return false;
        }

        return item.Kind != PdfPageObjectKind.Text || item is PdfTextObject { Text.Length: > 0 };
    }

    /// <summary>Maps a library object kind to the kind the viewer shows.</summary>
    /// <param name="kind">The library kind.</param>
    /// <returns>The viewer kind.</returns>
    private static PageObjectKind ToKind(PdfPageObjectKind kind) => kind switch
    {
        PdfPageObjectKind.Text => PageObjectKind.Text,
        PdfPageObjectKind.Image => PageObjectKind.Image,
        PdfPageObjectKind.Path => PageObjectKind.Drawing,
        _ => PageObjectKind.Other,
    };

    /// <summary>Finds an object by its number.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <returns>The object.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page has no such object.</exception>
    private static PdfPageObject Find(PdfPageContent content, int objectIndex)
    {
        var items = content.Objects;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Index == objectIndex)
            {
                return items[i];
            }
        }

        throw new ArgumentOutOfRangeException(nameof(objectIndex));
    }

    /// <summary>Makes one change to a page's content.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="edit">The change.</param>
    /// <exception cref="InvalidOperationException">The object to retype is not text.</exception>
    private static void Apply(PdfPageContent content, in ObjectEdit edit)
    {
        var item = Find(content, edit.ObjectIndex);
        switch (edit.Kind)
        {
            case ObjectEditKind.Move:
            {
                var page = content.Page!;
                var user = page.FromPhysical(new(edit.Delta.X, edit.Delta.Y)) - page.FromPhysical(Vector2.Zero);
                item.Translate(user.X, user.Y);
                break;
            }

            case ObjectEditKind.Delete:
            {
                item.Delete();
                break;
            }

            default:
            {
                var run = item as PdfTextObject ?? throw new InvalidOperationException("The object is not text.");
                PdfPageContentTextReplacement.Replace(content, run, edit.Text ?? string.Empty);
                break;
            }
        }
    }

    /// <summary>Reads a page, makes the change, then writes it back as one undoable step.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="edit">The change to make.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="InvalidOperationException">The document changed while the page was being read.</exception>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    private static async ValueTask EditAsync(HyperPdfDocument self, int pageIndex, ObjectEdit edit, CancellationToken cancellationToken)
    {
        var version = Volatile.Read(ref self.EditVersion);
        var content = await PdfPageContentReader.ReadAsync(self.Document, pageIndex, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (self.EditGate)
        {
            ObjectDisposedException.ThrowIf(self.IsDisposed, self);
            if (Volatile.Read(ref self.EditVersion) != version)
            {
                throw new InvalidOperationException(ChangedMessage);
            }

            using var access = HyperPdfNavigation.EnterPageWrite(self);
            Apply(content, edit);
            PdfPageContentApplication.Apply(content);
            HyperPdfNavigation.PagesEdited(self, PdfCacheScopes.PageContent);
        }
    }

    /// <summary>One change to one object.</summary>
    /// <param name="Kind">What to do.</param>
    /// <param name="ObjectIndex">The object's number.</param>
    /// <param name="Delta">How far to move, for a move.</param>
    /// <param name="Text">The new words, for a retype.</param>
    private readonly record struct ObjectEdit(ObjectEditKind Kind, int ObjectIndex, PagePoint Delta, string? Text);
}
