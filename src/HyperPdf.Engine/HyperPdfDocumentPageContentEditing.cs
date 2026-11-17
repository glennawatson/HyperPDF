// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.ImageFiles;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>Reads and changes the objects a page is made of, over the document's owned state.</summary>
internal static class HyperPdfDocumentPageContentEditing
{
    /// <summary>The message shown when the document changed while an edit was being prepared.</summary>
    private const string ChangedMessage = "The document changed while the edit was being prepared. Try again.";

    /// <summary>The largest value of a colour channel.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The bit shift of the red channel in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit shift of the green channel in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The smallest width a line is taken to have when dashes are sized from it, in user space units.</summary>
    private const float MinimumDashUnit = 1F;

    /// <summary>How long a dash is, in line widths.</summary>
    private const float DashLength = 3F;

    /// <summary>How long the gap after a dot is, in line widths.</summary>
    private const float DotGap = 2F;

    /// <summary>What an edit does to an object.</summary>
    private enum ObjectEditKind
    {
        /// <summary>Moves the object.</summary>
        Move = 0,

        /// <summary>Removes the object.</summary>
        Delete = 1,

        /// <summary>Replaces the words of a text object.</summary>
        ReplaceText = 2,

        /// <summary>Resizes, turns or flips the object.</summary>
        Transform = 3,

        /// <summary>Changes the fill or stroke colour.</summary>
        Colors = 4,

        /// <summary>Sets a text object in another font and size.</summary>
        Restyle = 5,

        /// <summary>Paints the object again in front of everything and removes it from where it was.</summary>
        Front = 6,

        /// <summary>Paints the object again behind everything and removes it from where it was.</summary>
        Back = 7,

        /// <summary>Moves the object one step forward.</summary>
        Forward = 8,

        /// <summary>Moves the object one step back.</summary>
        Backward = 9,

        /// <summary>Changes the dashes, ends and corners of the object's lines, or how solid it is.</summary>
        Look = 10,

        /// <summary>Draws another picture in place of a picture.</summary>
        ReplacePicture = 11,

        /// <summary>Crops a picture, keeping what is stored.</summary>
        CropPicture = 12,

        /// <summary>Writes new words over the lines of a paragraph, wrapped to the width the paragraph had.</summary>
        ReflowParagraph = 13,
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

            var area = LinkTargets.ToPageRect(page.ToPhysicalRectangle(item.Bounds));
            objects[next] = item is PdfTextObject run
                ? new(item.Index, ToKind(item.Kind), area, RightToLeftText.Typed(self.Document, pageIndex, run)) { Flow = PageObjectFlows.Of(page, run) }
                : new(item.Index, ToKind(item.Kind), area, string.Empty);
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
        EditAsync(self, pageIndex, new(ObjectEditKind.Move, objectIndex, delta, null, default, default, default), cancellationToken);

    /// <summary>Resizes, turns or flips an object.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="transformation">The change in page space.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask TransformAsync(HyperPdfDocument self, int pageIndex, int objectIndex, Matrix3x2 transformation, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.Transform, objectIndex, default, null, transformation, default, default), cancellationToken);

    /// <summary>Moves, resizes or turns several objects, each by its own change, in one step.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="changes">The objects and their changes.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    internal static ValueTask TransformManyAsync(HyperPdfDocument self, int pageIndex, PageObjectChange[] changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var edits = new ObjectEdit[changes.Length];
        for (var i = 0; i < changes.Length; i++)
        {
            edits[i] = new(ObjectEditKind.Transform, changes[i].ObjectIndex, default, null, changes[i].Transformation, default, default);
        }

        return EditManyAsync(self, pageIndex, edits, cancellationToken);
    }

    /// <summary>Changes the colours of several objects in one step.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The objects' numbers.</param>
    /// <param name="colors">What to change.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    internal static ValueTask SetColorsManyAsync(HyperPdfDocument self, int pageIndex, int[] objectIndexes, PageObjectColors colors, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectIndexes);
        var edits = new ObjectEdit[objectIndexes.Length];
        for (var i = 0; i < objectIndexes.Length; i++)
        {
            edits[i] = new(ObjectEditKind.Colors, objectIndexes[i], default, null, default, colors, default);
        }

        return EditManyAsync(self, pageIndex, edits, cancellationToken);
    }

    /// <summary>Draws another picture in place of a picture of the page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The picture's number.</param>
    /// <param name="file">The new picture's file.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask ReplacePictureAsync(HyperPdfDocument self, int pageIndex, int objectIndex, ReadOnlyMemory<byte> file, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.ReplacePicture, objectIndex, default, null, default, default, default) { Picture = file }, cancellationToken);

    /// <summary>Crops a picture of the page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The picture's number.</param>
    /// <param name="shares">The shares cut off each side.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask CropPictureAsync(HyperPdfDocument self, int pageIndex, int objectIndex, PageMargins shares, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.CropPicture, objectIndex, default, null, default, default, default) { Shares = shares }, cancellationToken);

    /// <summary>Makes a file from a picture of the page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The picture's number.</param>
    /// <param name="cancellationToken">Cancels reading the page.</param>
    /// <returns>The file.</returns>
    /// <exception cref="InvalidOperationException">The object is not a picture, or its pixels cannot be read.</exception>
    internal static async ValueTask<PageObjectFile> ExtractPictureAsync(HyperPdfDocument self, int pageIndex, int objectIndex, CancellationToken cancellationToken)
    {
        var content = await PdfPageContentReader.ReadAsync(self.Document, pageIndex, cancellationToken).ConfigureAwait(false);
        if (Find(content, objectIndex) is not PdfImageObject image)
        {
            throw new InvalidOperationException("Only a picture can be saved.");
        }

        return PdfImageExtraction.GetImage(image, content, pageIndex) is { } file
            ? new(Path.GetExtension(file.Name).TrimStart('.'), file.Data)
            : throw new InvalidOperationException("That picture's pixels cannot be read.");
    }

    /// <summary>Changes how several objects are drawn besides their colours, in one step.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The objects' numbers.</param>
    /// <param name="look">What to change.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    internal static ValueTask SetLookManyAsync(HyperPdfDocument self, int pageIndex, int[] objectIndexes, in PageObjectLook look, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectIndexes);
        var edits = new ObjectEdit[objectIndexes.Length];
        for (var i = 0; i < objectIndexes.Length; i++)
        {
            edits[i] = new(ObjectEditKind.Look, objectIndexes[i], default, null, default, default, default) { Look = look };
        }

        return EditManyAsync(self, pageIndex, edits, cancellationToken);
    }

    /// <summary>Removes several objects in one step.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The objects' numbers.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    internal static ValueTask DeleteManyAsync(HyperPdfDocument self, int pageIndex, int[] objectIndexes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectIndexes);
        var edits = new ObjectEdit[objectIndexes.Length];
        for (var i = 0; i < objectIndexes.Length; i++)
        {
            edits[i] = new(ObjectEditKind.Delete, objectIndexes[i], default, null, default, default, default);
        }

        return EditManyAsync(self, pageIndex, edits, cancellationToken);
    }

    /// <summary>Moves an object in front of everything else on its page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask BringToFrontAsync(HyperPdfDocument self, int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.Front, objectIndex, default, null, default, default, default), cancellationToken);

    /// <summary>Moves an object one step forward or back.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="backward">True to move it one step back, false to move it one step forward.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask StepAsync(HyperPdfDocument self, int pageIndex, int objectIndex, bool backward, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(backward ? ObjectEditKind.Backward : ObjectEditKind.Forward, objectIndex, default, null, default, default, default), cancellationToken);

    /// <summary>Moves an object behind everything else on its page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask SendToBackAsync(HyperPdfDocument self, int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.Back, objectIndex, default, null, default, default, default), cancellationToken);

    /// <summary>Paints another copy of an object on a page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based index of the page the object is on.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="targetPageIndex">The zero based index of the page the copy goes on.</param>
    /// <param name="offset">How far from the original's place the copy goes, in page space.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="InvalidOperationException">The document changed while the pages were being read.</exception>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    internal static async ValueTask CopyAsync(HyperPdfDocument self, int pageIndex, int objectIndex, int targetPageIndex, PagePoint offset, CancellationToken cancellationToken)
    {
        var version = Volatile.Read(ref self.EditVersion);
        var source = await PdfPageContentReader.ReadAsync(self.Document, pageIndex, cancellationToken).ConfigureAwait(false);
        var target = targetPageIndex == pageIndex ? source : await PdfPageContentReader.ReadAsync(self.Document, targetPageIndex, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (self.EditGate)
        {
            ObjectDisposedException.ThrowIf(self.IsDisposed, self);
            if (Volatile.Read(ref self.EditVersion) != version)
            {
                throw new InvalidOperationException(ChangedMessage);
            }

            using var access = HyperPdfNavigation.EnterPageWrite(self);
            var move = ToUserMatrix(target.Page!, Matrix3x2.CreateTranslation(offset.X, offset.Y));
            if (targetPageIndex == pageIndex)
            {
                // On its own page a copy is written as the object's own content, so it can be edited like the original.
                PdfPageContentObjectCopy.CopyInline(source, Find(source, objectIndex), move);
            }
            else if (!PdfPageContentObjectCopy.TryCopyInline(source, target, Find(source, objectIndex), move))
            {
                // A resource name that means something else on the target page leaves the copy as a form.
                PdfPageContentObjectCopy.Draw(target, PdfPageContentObjectCopy.CreateForm(source, Find(source, objectIndex)), move);
            }

            PdfPageContentApplication.Apply(target);
            HyperPdfNavigation.PagesEdited(self, PdfCacheScopes.PageContent);
        }
    }

    /// <summary>Changes the colours of an object.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="colors">What to change.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask SetColorsAsync(HyperPdfDocument self, int pageIndex, int objectIndex, PageObjectColors colors, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.Colors, objectIndex, default, null, default, colors, default), cancellationToken);

    /// <summary>Reads how an object is painted.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="cancellationToken">Cancels reading the page.</param>
    /// <returns>The colours, line width and font size.</returns>
    internal static async ValueTask<PageObjectStyle> ReadStyleAsync(HyperPdfDocument self, int pageIndex, int objectIndex, CancellationToken cancellationToken)
    {
        var content = await PdfPageContentReader.ReadAsync(self.Document, pageIndex, cancellationToken).ConfigureAwait(false);
        var item = Find(content, objectIndex);
        return new(ToArgb(item.FillPaint), ToArgb(item.StrokePaint), item.LineWidth, item is PdfTextObject run ? run.FontSize : 0F);
    }

    /// <summary>Removes an object.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask DeleteAsync(HyperPdfDocument self, int pageIndex, int objectIndex, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.Delete, objectIndex, default, null, default, default, default), cancellationToken);

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
        return EditAsync(self, pageIndex, new(ObjectEditKind.ReplaceText, objectIndex, default, text, default, default, default) { Fonts = self.FontState }, cancellationToken);
    }

    /// <summary>Replaces the text of the lines of a paragraph with new text wrapped to the width the paragraph had.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndexes">The numbers of the text objects that are the paragraph's lines.</param>
    /// <param name="text">The new text.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    internal static ValueTask ReflowParagraphAsync(HyperPdfDocument self, int pageIndex, int[] objectIndexes, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectIndexes);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfZero(objectIndexes.Length);
        var edit = new ObjectEdit(ObjectEditKind.ReflowParagraph, objectIndexes[0], default, text, default, default, default) { Fonts = self.FontState, Lines = objectIndexes };
        return EditAsync(self, pageIndex, edit, cancellationToken);
    }

    /// <summary>Sets a text object in another font and size.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="objectIndex">The object's number.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask RestyleTextAsync(HyperPdfDocument self, int pageIndex, int objectIndex, PageTextStyle style, CancellationToken cancellationToken) =>
        EditAsync(self, pageIndex, new(ObjectEditKind.Restyle, objectIndex, default, null, default, default, style) { Fonts = self.FontState }, cancellationToken);

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

    /// <summary>Turns a change in page space into the same change in the page's user space, where the content is written.</summary>
    /// <param name="page">The page, which knows its rotation and origin.</param>
    /// <param name="change">The change in page space.</param>
    /// <returns>The change in user space.</returns>
    private static Matrix3x2 ToUserMatrix(PdfPage page, Matrix3x2 change)
    {
        // The change is carried out on the viewer's page and the result brought back, so the map of three points fixes the matrix.
        Vector2 Carry(Vector2 user) => page.FromPhysical(Vector2.Transform(page.ToPhysical(user), change));
        var origin = Carry(Vector2.Zero);
        var x = Carry(Vector2.UnitX) - origin;
        var y = Carry(Vector2.UnitY) - origin;
        return new(x.X, x.Y, y.X, y.Y, origin.X, origin.Y);
    }

    /// <summary>Turns a paint into an opaque colour, for the colour spaces a person picks from.</summary>
    /// <param name="paint">The paint.</param>
    /// <returns>The colour as 0xAARRGGBB; black for a paint that is not a plain device colour.</returns>
    private static uint ToArgb(PdfPaint paint)
    {
        const uint Opaque = 0xFF000000;
        var c = paint.Components;
        return paint.ColorSpace.ToKnownName() switch
        {
            KnownName.DeviceGray when c.Length >= 1 => Opaque | Pack(c[0], c[0], c[0]),
            KnownName.DeviceRGB when c.Length >= 3 => Opaque | Pack(c[0], c[1], c[2]),
            KnownName.DeviceCMYK when c.Length >= 4 => Opaque | Pack((1F - c[0]) * (1F - c[3]), (1F - c[1]) * (1F - c[3]), (1F - c[2]) * (1F - c[3])),
            _ => Opaque,
        };
    }

    /// <summary>Packs three channels of 0 to 1 into 0xRRGGBB.</summary>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <returns>The packed colour.</returns>
    private static uint Pack(float red, float green, float blue) =>
        (Channel(red) << RedShift) | (Channel(green) << GreenShift) | Channel(blue);

    /// <summary>Turns a channel of 0 to 1 into 0 to 255.</summary>
    /// <param name="value">The channel.</param>
    /// <returns>The byte.</returns>
    private static uint Channel(float value) => (uint)Math.Round(Math.Clamp(value, 0F, 1F) * ChannelMax);

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

            case ObjectEditKind.Transform:
            {
                item.Transform(ToUserMatrix(content.Page!, edit.Matrix));
                break;
            }

            case ObjectEditKind.Colors:
            {
                if (edit.Colors.ChangeFill)
                {
                    item.SetFillPaint(PdfPaint.FromRgb24(edit.Colors.Fill));
                }

                if (edit.Colors.ChangeStroke)
                {
                    item.SetStrokePaint(PdfPaint.FromRgb24(edit.Colors.Stroke));
                }

                break;
            }

            case ObjectEditKind.Look or ObjectEditKind.ReplacePicture or ObjectEditKind.CropPicture:
            {
                ApplyAppearance(content, item, edit);
                break;
            }

            case ObjectEditKind.Front or ObjectEditKind.Back:
            {
                Reorder(content, item, edit.Kind == ObjectEditKind.Back);
                break;
            }

            case ObjectEditKind.Forward or ObjectEditKind.Backward:
            {
                Step(content, item, edit.Kind == ObjectEditKind.Backward);
                break;
            }

            default:
            {
                Retype(content, item, edit);
                break;
            }
        }
    }

    /// <summary>Writes new words over the lines of a paragraph, wrapped to the width the paragraph had.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="edit">The edit, which names the paragraph's lines and holds the new words.</param>
    /// <exception cref="InvalidOperationException">A line is not text.</exception>
    private static void Reflow(PdfPageContent content, in ObjectEdit edit)
    {
        var lines = new List<PdfTextObject>(edit.Lines.Length);
        foreach (var index in edit.Lines)
        {
            lines.Add(Find(content, index) as PdfTextObject ?? throw new InvalidOperationException("The object is not text."));
        }

        var words = edit.Text ?? string.Empty;
        PdfPageContentTextReplacement.ReplaceParagraph(content, lines, words, AppearanceFont.Helvetica, 0F, FallbackFor(edit, words, AppearanceFont.Helvetica));
    }

    /// <summary>Writes a text object's words again, either as new words or in another font and size.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="item">The object to retype or restyle, which must be text.</param>
    /// <param name="edit">The edit: a retype with new words, a restyle that keeps the words, or a reflow of a paragraph.</param>
    /// <exception cref="InvalidOperationException">The object is not text.</exception>
    private static void Retype(PdfPageContent content, PdfPageObject item, in ObjectEdit edit)
    {
        if (edit.Kind == ObjectEditKind.ReflowParagraph)
        {
            Reflow(content, edit);
            return;
        }

        var run = item as PdfTextObject ?? throw new InvalidOperationException("The object is not text.");
        var restyle = edit.Kind == ObjectEditKind.Restyle;
        var font = restyle ? HyperPdfDocumentPageContentAdding.FontOf(edit.Style.Family, edit.Style.Bold, edit.Style.Italic) : AppearanceFont.Helvetica;
        var words = restyle ? run.GetRemainingText() : edit.Text ?? string.Empty;
        PdfPageContentTextReplacement.Replace(content, run, words, font, restyle ? edit.Style.FontSize : 0F, FallbackFor(edit, words, font));
    }

    /// <summary>
    /// Moves an object one step forward or back. A step forward puts the object over the one in front of it: the object and everything painted after that one are painted
    /// again, in their order, and removed from where they were. A step back is the step forward of the object behind it.
    /// </summary>
    /// <param name="content">The page content.</param>
    /// <param name="item">The object.</param>
    /// <param name="backward">True to move it one step back, false to move it one step forward.</param>
    private static void Step(PdfPageContent content, PdfPageObject item, bool backward)
    {
        var objects = content.Objects;
        var at = IndexOfObject(objects, item);
        var mover = backward ? Neighbour(objects, at, -1) : item;
        var over = backward ? item : Neighbour(objects, at, 1);
        if (mover is null || over is null)
        {
            return;
        }

        var moved = new List<PdfPageObject> { mover };
        for (var i = IndexOfObject(objects, over) + 1; i < objects.Count; i++)
        {
            moved.Add(objects[i]);
        }

        PdfPageContentObjectCopy.MoveInline(content, moved.ToArray(), false);
    }

    /// <summary>Finds the object next to another that has a place on the page that can be picked.</summary>
    /// <param name="objects">The objects of the page, in the order they are painted.</param>
    /// <param name="at">The place of the object.</param>
    /// <param name="direction">1 for the object painted after it, -1 for the object painted before it.</param>
    /// <returns>The neighbour, or <see langword="null"/> when there is none.</returns>
    private static PdfPageObject? Neighbour(IReadOnlyList<PdfPageObject> objects, int at, int direction)
    {
        for (var i = at + direction; i >= 0 && i < objects.Count; i += direction)
        {
            if (!objects[i].IsDeleted && IsListed(objects[i]))
            {
                return objects[i];
            }
        }

        return null;
    }

    /// <summary>Finds the place of an object among the objects of a page.</summary>
    /// <param name="objects">The objects.</param>
    /// <param name="item">The object.</param>
    /// <returns>Its place.</returns>
    private static int IndexOfObject(IReadOnlyList<PdfPageObject> objects, PdfPageObject item)
    {
        for (var i = 0; i < objects.Count; i++)
        {
            if (ReferenceEquals(objects[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Paints an object again at one end of the page and removes it from where it was.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="item">The object.</param>
    /// <param name="behind">True to paint it behind everything else, false to paint it in front.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Reorder(PdfPageContent content, PdfPageObject item, bool behind) => PdfPageContentObjectCopy.MoveInline(content, [item], behind);

    /// <summary>Finds an installed font for the letters of some text an edit writes.</summary>
    /// <param name="edit">The edit.</param>
    /// <param name="words">The text.</param>
    /// <param name="font">The look.</param>
    /// <returns>The font, or <see langword="null"/> when none is needed or the edit has no font state.</returns>
    private static PdfFontSource? FallbackFor(in ObjectEdit edit, string words, AppearanceFont font) => edit.Fonts is { } fonts ? NewTextFonts.Find(fonts, words, font) : null;

    /// <summary>Reads a page, makes the change, then writes it back as one undoable step.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="edit">The change to make.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="InvalidOperationException">The document changed while the page was being read.</exception>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask EditAsync(HyperPdfDocument self, int pageIndex, in ObjectEdit edit, CancellationToken cancellationToken) =>
        EditManyAsync(self, pageIndex, [edit], cancellationToken);

    /// <summary>Reads a page, makes several changes, then writes it back as one undoable step.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="edits">The changes to make, in order.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="InvalidOperationException">The document changed while the page was being read.</exception>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    private static async ValueTask EditManyAsync(HyperPdfDocument self, int pageIndex, ObjectEdit[] edits, CancellationToken cancellationToken)
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
            for (var i = 0; i < edits.Length; i++)
            {
                Apply(content, edits[i]);
            }

            PdfPageContentApplication.Apply(content);
            HyperPdfNavigation.PagesEdited(self, PdfCacheScopes.PageContent);
        }
    }

    /// <summary>Applies an edit of how an object looks: its lines and opacity, a replacement picture or a crop.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="item">The object.</param>
    /// <param name="edit">The edit.</param>
    private static void ApplyAppearance(PdfPageContent content, PdfPageObject item, in ObjectEdit edit)
    {
        switch (edit.Kind)
        {
            case ObjectEditKind.ReplacePicture:
            {
                ReplacePicture(content, item, edit.Picture.Span);
                break;
            }

            case ObjectEditKind.CropPicture:
            {
                CropPicture(item, edit.Shares);
                break;
            }

            default:
            {
                ApplyLook(item, edit.Look);
                break;
            }
        }
    }

    /// <summary>Draws another picture in place of a picture of the page.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="item">The object.</param>
    /// <param name="file">The new picture's file.</param>
    /// <exception cref="InvalidOperationException">The object is not a picture.</exception>
    /// <exception cref="InvalidDataException">The file is not a picture that can be read.</exception>
    private static void ReplacePicture(PdfPageContent content, PdfPageObject item, ReadOnlySpan<byte> file)
    {
        if (item is not PdfImageObject image)
        {
            throw new InvalidOperationException("Only a picture can be replaced.");
        }

        var sink = new PdfObjectStoreSink(content.Document.Objects);
        try
        {
            var created = PdfImageFiles.Detect(file) == PdfImageFileFormat.Jpeg
                ? PdfImageWriter.AddJpeg(sink, file)
                : PdfImageWriter.AddRaster(sink, PdfImageFiles.Decode(file, CancellationToken.None));
            image.ReplaceWithImage(created.Id);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException("That is not a picture that can be read. Choose a PNG, JPEG, GIF, BMP or TIFF file.", ex);
        }
    }

    /// <summary>Crops a picture, drawing or shading of the page.</summary>
    /// <param name="item">The object.</param>
    /// <param name="shares">The shares of the object cut off each side.</param>
    /// <exception cref="InvalidOperationException">The object is text.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The shares are out of range or leave nothing.</exception>
    private static void CropPicture(PdfPageObject item, PageMargins shares)
    {
        if (item is PdfTextObject)
        {
            throw new InvalidOperationException("Text cannot be cropped.");
        }

        item.SetCrop(shares.Left, shares.Top, shares.Right, shares.Bottom);
    }

    /// <summary>Changes how an object is drawn besides its colours.</summary>
    /// <param name="item">The object.</param>
    /// <param name="look">What to change.</param>
    private static void ApplyLook(PdfPageObject item, in PageObjectLook look)
    {
        if (look.ChangeOpacity)
        {
            item.SetOpacity(Math.Clamp(look.Opacity, 0F, 1F));
        }

        if (look.ChangeGradient)
        {
            var gradient = look.Gradient;
            var start = PdfPaint.FromRgb24(gradient.Start);
            var end = PdfPaint.FromRgb24(gradient.End);
            item.SetGradientFill(gradient.IsRadial ? PdfGradient.Radial(start, end) : PdfGradient.Linear(start, end, gradient.AngleDegrees));
        }

        if (look.ChangeBlend)
        {
            item.SetBlendMode((PdfBlendMode)(int)look.Blend);
        }

        if (look.ChangeLine)
        {
            item.SetLineStyle(LineStyleFor(item.LineWidth, look));
        }
    }

    /// <summary>Makes the line style for a look: dashes sized to the line's width, so a thick line has long dashes and a thin line short ones.</summary>
    /// <param name="lineWidth">The width of the object's line in user space units.</param>
    /// <param name="look">The look.</param>
    /// <returns>The style.</returns>
    private static PdfLineStyle LineStyleFor(float lineWidth, in PageObjectLook look)
    {
        var width = Math.Max(lineWidth, MinimumDashUnit);
        var dash = look.Dash switch
        {
            PageLineDash.Dashed => new[] { width * DashLength, width * DashLength },
            PageLineDash.Dotted => look.Cap == PageLineCap.Round ? new[] { 0F, width * DotGap } : new[] { width, width * DotGap },
            PageLineDash.DashDot => new[] { width * DashLength, width * DotGap, width, width * DotGap },
            _ => [],
        };
        return new((PdfLineCap)(int)look.Cap, (PdfLineJoin)(int)look.Join, dash, 0F);
    }

    /// <summary>One change to one object.</summary>
    /// <param name="Kind">What to do.</param>
    /// <param name="ObjectIndex">The object's number.</param>
    /// <param name="Delta">How far to move, for a move.</param>
    /// <param name="Text">The new words, for a retype.</param>
    /// <param name="Matrix">The change in page space, for a resize, turn or flip.</param>
    /// <param name="Colors">The colours to change.</param>
    /// <param name="Style">The font and size, for a restyle of text.</param>
    private readonly record struct ObjectEdit(ObjectEditKind Kind, int ObjectIndex, PagePoint Delta, string? Text, Matrix3x2 Matrix, PageObjectColors Colors, PageTextStyle Style)
    {
        /// <summary>Gets the engine's font state, which finds an installed font for letters the bundled fonts lack; set for the edits that write text.</summary>
        internal TextBoxFontState? Fonts { get; init; }

        /// <summary>Gets what changes about how the object is drawn, for a look edit.</summary>
        internal PageObjectLook Look { get; init; }

        /// <summary>Gets the file of the new picture, for a replacement.</summary>
        internal ReadOnlyMemory<byte> Picture { get; init; }

        /// <summary>Gets the shares of the picture cut off each side, for a crop.</summary>
        internal PageMargins Shares { get; init; }

        /// <summary>Gets the numbers of the text objects that are the lines of a paragraph, for a reflow.</summary>
        internal int[] Lines { get; init; } = [];
    }
}
