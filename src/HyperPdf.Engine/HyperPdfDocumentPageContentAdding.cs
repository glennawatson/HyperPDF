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
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.ImageFiles;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>Writes new objects into a page's own content, over the document's owned state.</summary>
internal static class HyperPdfDocumentPageContentAdding
{
    /// <summary>The message shown when the document changed while an edit was being prepared.</summary>
    private const string ChangedMessage = "The document changed while the edit was being prepared. Try again.";

    /// <summary>The prefix of the name a picture gets in the page's resources.</summary>
    private const string ImagePrefix = "HpImg";

    /// <summary>The prefix of the name the font gets in the page's resources.</summary>
    private const string FontPrefix = "HpText";

    /// <summary>The longest line of text that is written, in characters.</summary>
    private const int LineBytes = 4096;

    /// <summary>The largest value of a colour channel.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The bit shift of the red channel in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit shift of the green channel in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The mask of one colour channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>How far above the top of a line of text its baseline lies, as a fraction of the font size.</summary>
    private const float AscentRatio = 0.75F;

    /// <summary>The control point distance of a Bézier arc of a quarter circle, as a fraction of the radius.</summary>
    private const float ArcControl = 0.5522847F;

    /// <summary>Half of a length.</summary>
    private const float Half = 0.5F;

    /// <summary>How far a bold font lies from the plain one in the list of standard fonts.</summary>
    private const int BoldStep = 1;

    /// <summary>How far a slanted font lies from the plain one in the list of standard fonts.</summary>
    private const int ItalicStep = 2;

    /// <summary>Appends something to a page's content.</summary>
    /// <typeparam name="TState">What is written.</typeparam>
    /// <param name="content">The page content.</param>
    /// <param name="state">What to write.</param>
    private delegate void WriteInto<TState>(PdfPageContent content, in TState state);

    /// <summary>Writes a new object into a page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="draft">What to add and where.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="InvalidOperationException">The document changed while the page was being read.</exception>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    internal static ValueTask AddAsync(HyperPdfDocument self, int pageIndex, in PageObjectDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft.Text);
        var font = draft.Kind == PageObjectDraftKind.Text ? NewTextFonts.Find(self.FontState, draft.Text, FontOf(draft.Family, draft.Bold, draft.Italic)) : null;
        return WriteAsync(self, pageIndex, new NewObject(draft, font), Write, cancellationToken);
    }

    /// <summary>Places a picture on a page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="area">The area the picture may fill, in page space.</param>
    /// <param name="file">The picture file.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="NotSupportedException">The bytes are not a picture the library reads.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask AddPictureAsync(HyperPdfDocument self, int pageIndex, PageRect area, ReadOnlyMemory<byte> file, CancellationToken cancellationToken) =>
        WriteAsync(self, pageIndex, new PictureDraft(area, file), WritePicture, cancellationToken);

    /// <summary>Picks the standard font for a family, weight and slant.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the text is bold.</param>
    /// <param name="italic">Whether the text is slanted.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AppearanceFont FontOf(PageTextFamily family, bool bold, bool italic) =>
        (AppearanceFont)((int)FirstOf(family) + (bold ? BoldStep : 0) + (italic ? ItalicStep : 0));

    /// <summary>Reads a page, lets a writer append to it, then writes it back as one undoable step.</summary>
    /// <typeparam name="TState">What the writer needs.</typeparam>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="state">The object or picture to write.</param>
    /// <param name="write">Appends to the page content.</param>
    /// <param name="cancellationToken">Cancels before the edit.</param>
    /// <returns>A task completing after the page has changed.</returns>
    /// <exception cref="InvalidOperationException">The document changed while the page was being read.</exception>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    private static async ValueTask WriteAsync<TState>(HyperPdfDocument self, int pageIndex, TState state, WriteInto<TState> write, CancellationToken cancellationToken)
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
            write(content, state);
            PdfPageContentApplication.Apply(content);
            HyperPdfNavigation.PagesEdited(self, PdfCacheScopes.PageContent);
        }
    }

    /// <summary>Appends a picture, which is stored in the document and drawn to fit its area.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="picture">The picture and where it goes.</param>
    private static void WritePicture(PdfPageContent content, in PictureDraft picture)
    {
        var page = content.Page!;
        var sink = new PdfObjectStoreSink(content.Document.Objects);
        var span = picture.File.Span;
        var created = PdfImageFiles.Detect(span) == PdfImageFileFormat.Jpeg
            ? PdfImageWriter.AddJpeg(sink, span)
            : PdfImageWriter.AddRaster(sink, PdfImageFiles.Decode(span, CancellationToken.None));
        var name = PdfPageContentEditing.AllocateName(content, KnownName.XObject, ImagePrefix);
        PdfPageContentEditing.AddResource(content, KnownName.XObject, name, PdfValue.FromReference(created.Id));

        // The picture keeps its shape and sits in the middle of the area.
        var area = picture.Area;
        var shape = created.Width > 0 && created.Height > 0 ? (float)created.Width / created.Height : 1F;
        var width = MathF.Min(area.Width, area.Height * shape);
        var height = shape > 0 ? width / shape : area.Height;
        var left = area.Left + ((area.Width - width) * Half);
        var top = area.Top + ((area.Height - height) * Half);
        var bottomLeft = ToUser(page, left, top + height);
        var across = ToUser(page, left + width, top + height) - bottomLeft;
        var up = ToUser(page, left, top) - bottomLeft;
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            builder.Transform(across.X, across.Y, up.X, up.Y, bottomLeft.X, bottomLeft.Y);
            builder.DrawXObject(content.Document.Objects.Names.GetSpelling(name));
            builder.RestoreState();
            PdfPageContentEditing.Append(content, builder.WrittenSpan);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Appends the drawing of an object to the page's content.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="item">The object and the font for its letters, if it needs one.</param>
    private static void Write(PdfPageContent content, in NewObject item)
    {
        var draft = item.Draft;
        var page = content.Page!;
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            if (draft.Kind == PageObjectDraftKind.Text)
            {
                WriteText(content, ref builder, draft, item.Font);
            }
            else
            {
                WriteShape(page, ref builder, draft);
            }

            builder.RestoreState();
            PdfPageContentEditing.Append(content, builder.WrittenSpan);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Writes a rectangle, ellipse or line.</summary>
    /// <param name="page">The page, which maps the page the viewer shows to user space.</param>
    /// <param name="builder">The output.</param>
    /// <param name="draft">The shape.</param>
    private static void WriteShape(PdfPage page, ref PdfContentBuilder builder, in PageObjectDraft draft)
    {
        var box = draft.Bounds;
        SetColours(ref builder, draft);
        switch (draft.Kind)
        {
            case PageObjectDraftKind.Line:
            {
                var from = ToUser(page, box.Left, box.Top);
                var to = ToUser(page, box.Right, box.Bottom);
                builder.MoveTo(from.X, from.Y);
                builder.LineTo(to.X, to.Y);
                builder.Stroke();
                return;
            }

            case PageObjectDraftKind.Ellipse:
            {
                WriteEllipse(page, ref builder, box.Left, box.Top, box.Right, box.Bottom);
                break;
            }

            default:
            {
                var a = ToUser(page, box.Left, box.Top);
                var b = ToUser(page, box.Right, box.Top);
                var c = ToUser(page, box.Right, box.Bottom);
                var d = ToUser(page, box.Left, box.Bottom);
                builder.MoveTo(a.X, a.Y);
                builder.LineTo(b.X, b.Y);
                builder.LineTo(c.X, c.Y);
                builder.LineTo(d.X, d.Y);
                builder.ClosePath();
                break;
            }
        }

        Paint(ref builder, draft);
    }

    /// <summary>Writes an ellipse as four Bézier arcs.</summary>
    /// <param name="page">The page.</param>
    /// <param name="builder">The output.</param>
    /// <param name="left">The left edge in page space.</param>
    /// <param name="top">The top edge in page space.</param>
    /// <param name="right">The right edge in page space.</param>
    /// <param name="bottom">The bottom edge in page space.</param>
    private static void WriteEllipse(PdfPage page, ref PdfContentBuilder builder, float left, float top, float right, float bottom)
    {
        var rx = (right - left) * Half;
        var ry = (bottom - top) * Half;
        var cx = left + rx;
        var cy = top + ry;
        var kx = rx * ArcControl;
        var ky = ry * ArcControl;
        var start = ToUser(page, right, cy);
        builder.MoveTo(start.X, start.Y);
        Curve(page, ref builder, new(right, cy + ky), new(cx + kx, bottom), new(cx, bottom));
        Curve(page, ref builder, new(cx - kx, bottom), new(left, cy + ky), new(left, cy));
        Curve(page, ref builder, new(left, cy - ky), new(cx - kx, top), new(cx, top));
        Curve(page, ref builder, new(cx + kx, top), new(right, cy - ky), new(right, cy));
        builder.ClosePath();
    }

    /// <summary>Writes one Bézier curve between points in page space.</summary>
    /// <param name="page">The page.</param>
    /// <param name="builder">The output.</param>
    /// <param name="first">The first control point in page space.</param>
    /// <param name="second">The second control point in page space.</param>
    /// <param name="end">The end point in page space.</param>
    private static void Curve(PdfPage page, ref PdfContentBuilder builder, Vector2 first, Vector2 second, Vector2 end)
    {
        var a = ToUser(page, first.X, first.Y);
        var b = ToUser(page, second.X, second.Y);
        var c = ToUser(page, end.X, end.Y);
        builder.CurveTo(a.X, a.Y, b.X, b.Y, c.X, c.Y);
    }

    /// <summary>Writes a line of text in Helvetica on the line the box gives.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="builder">The output.</param>
    /// <param name="draft">The text.</param>
    /// <param name="fallback">An installed font that has the letters the bundled fonts lack, or <see langword="null"/>.</param>
    private static void WriteText(PdfPageContent content, ref PdfContentBuilder builder, in PageObjectDraft draft, PdfFontSource? fallback)
    {
        // A built-in font keeps the line a single object that is picked and retyped as a whole; letters it lacks are shown with the bundled font of the same look.
        var run = PdfNewTextRun.Create(content, draft.Text, FontOf(draft.Family, draft.Bold, draft.Italic), fallback);
        var page = content.Page!;
        var baseline = ToUser(page, draft.Bounds.Left, draft.Bounds.Top + (draft.FontSize * AscentRatio));
        var across = ToUser(page, draft.Bounds.Left + 1F, draft.Bounds.Top + (draft.FontSize * AscentRatio)) - baseline;
        var up = ToUser(page, draft.Bounds.Left, draft.Bounds.Top + (draft.FontSize * AscentRatio) - 1F) - baseline;
        builder.SetFillRgb(Channel(draft.Fill, RedShift), Channel(draft.Fill, GreenShift), Channel(draft.Fill, 0));
        builder.BeginText();
        run.SelectFont(ref builder, draft.FontSize);
        builder.SetTextMatrix(across.X, across.Y, up.X, up.Y, baseline.X, baseline.Y);
        run.Show(ref builder, draft.FontSize);
        builder.EndText();
        run.Finish();
    }

    /// <summary>Gets the plain style of a family. Each family lists its four styles in the same order: plain, bold, slanted, bold slanted.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AppearanceFont FirstOf(PageTextFamily family) => family switch
    {
        PageTextFamily.Serif => AppearanceFont.TimesRoman,
        PageTextFamily.Fixed => AppearanceFont.Courier,
        _ => AppearanceFont.Helvetica,
    };

    /// <summary>Sets the fill and line colours and the line width of a shape.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="draft">The shape.</param>
    private static void SetColours(ref PdfContentBuilder builder, in PageObjectDraft draft)
    {
        if (draft.HasFill)
        {
            builder.SetFillRgb(Channel(draft.Fill, RedShift), Channel(draft.Fill, GreenShift), Channel(draft.Fill, 0));
        }

        if (!draft.HasStroke && draft.Kind != PageObjectDraftKind.Line)
        {
            return;
        }

        builder.SetStrokeRgb(Channel(draft.Stroke, RedShift), Channel(draft.Stroke, GreenShift), Channel(draft.Stroke, 0));
        builder.SetLineWidth(draft.LineWidth);
    }

    /// <summary>Paints the path that was written, as a fill, an outline or both.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="draft">The shape.</param>
    private static void Paint(ref PdfContentBuilder builder, in PageObjectDraft draft)
    {
        if (draft.HasFill && draft.HasStroke)
        {
            builder.FillAndStroke();
        }
        else if (draft.HasFill)
        {
            builder.Fill();
        }
        else if (draft.HasStroke)
        {
            builder.Stroke();
        }
        else
        {
            builder.EndPath();
        }
    }

    /// <summary>Maps a point of the page the viewer shows to user space.</summary>
    /// <param name="page">The page.</param>
    /// <param name="x">The x in page space.</param>
    /// <param name="y">The y in page space.</param>
    /// <returns>The point in user space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 ToUser(PdfPage page, float x, float y) => page.FromPhysical(new(x, y));

    /// <summary>Gets one channel of 0xRRGGBB as 0 to 1.</summary>
    /// <param name="rgb">The colour.</param>
    /// <param name="shift">The bit shift of the channel.</param>
    /// <returns>The channel.</returns>
    private static float Channel(uint rgb, int shift) => ((rgb >> shift) & ChannelMask) / ChannelMax;

    /// <summary>A picture and the area it may fill.</summary>
    /// <param name="Area">The area in page space.</param>
    /// <param name="File">The picture file.</param>
    private readonly record struct PictureDraft(PageRect Area, ReadOnlyMemory<byte> File);

    /// <summary>A new object and the font its letters need.</summary>
    /// <param name="Draft">The object.</param>
    /// <param name="Font">An installed font that has the letters the bundled fonts lack, or <see langword="null"/>.</param>
    private readonly record struct NewObject(PageObjectDraft Draft, PdfFontSource? Font);
}
