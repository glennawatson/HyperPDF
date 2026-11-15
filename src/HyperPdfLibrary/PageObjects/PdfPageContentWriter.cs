// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Writes changed page objects and preserves untouched source bytes.</summary>
public static class PdfPageContentWriter
{
    /// <summary>The components of an RGB colour.</summary>
    internal const int RgbComponents = 3;

    /// <summary>The components of a CMYK colour.</summary>
    internal const int CmykComponents = 4;

    /// <summary>The key of a graphics state parameter dictionary that sets the opacity of filling.</summary>
    private const string FillOpacityKey = "ca";

    /// <summary>Gets the byte that ends a line.</summary>
    internal static ReadOnlySpan<byte> NewLine => "\n"u8;

    /// <summary>Writes the content again. Objects nobody changed keep the bytes they were read from.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <returns>The new content stream data, not compressed.</returns>
    /// <exception cref = "InvalidOperationException">A form this content paints was changed but its content cannot be written.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Regenerate(PdfPageContent state) => PdfPageContentWriter.Regenerate(state, PdfRegenerateMode.Preserve);

    /// <summary>Writes the content again.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "mode">Whether objects nobody changed keep their original bytes or are written from the model.</param>
    /// <returns>The new content stream data, not compressed.</returns>
    /// <exception cref = "InvalidOperationException">A form this content paints was changed but its content cannot be written.</exception>
    public static byte[] Regenerate(PdfPageContent state, PdfRegenerateMode mode)
    {
        state.Generated.Clear();
        var builder = default(PdfContentBuilder);
        try
        {
            PdfPageContentWriter.WriteAll(state, ref builder, mode);
            return builder.ToArray();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Writes the content again.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "mode">Whether objects nobody changed keep their original bytes or are written from the model.</param>
    /// <param name = "cancellationToken">Cancels the work before it starts.</param>
    /// <returns>The new content stream data, not compressed.</returns>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<byte[]> RegenerateAsync(PdfPageContent state, PdfRegenerateMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(PdfPageContentWriter.Regenerate(state, mode));
    }

    /// <summary>Determines whether an object is written from the model rather than copied.</summary>
    /// <param name = "item">The object.</param>
    /// <param name = "mode">The mode.</param>
    /// <returns><see langword="true"/> when the object's bytes are written again.</returns>
    internal static bool ShouldWrite(PdfPageObject item, PdfRegenerateMode mode)
    {
        if (item.IsModified)
        {
            return true;
        }

        return mode == PdfRegenerateMode.Rewrite && item is not PdfTextObject { IsOpaque: true } and not PdfImageObject { IsInline: true };
    }

    /// <summary>Writes the <c>cm</c> that moves an object in user space.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "item">The object.</param>
    internal static void WriteTransform(ref PdfContentBuilder builder, PdfPageObject item)
    {
        if (!item.IsTransformed || !PdfPageContentWriter.TryGetLocalTransform(item, out var matrix))
        {
            return;
        }

        builder.Transform(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);
    }

    /// <summary>Gets the matrix a <c>cm</c> needs so the object moves by its transformation in user space.</summary>
    /// <param name = "item">The object.</param>
    /// <param name = "matrix">Receives the matrix.</param>
    /// <returns><see langword="false"/> when the object's own matrix cannot be inverted.</returns>
    internal static bool TryGetLocalTransform(PdfPageObject item, out Matrix3x2 matrix)
    {
        if (!Matrix3x2.Invert(item.Matrix, out var inverse))
        {
            matrix = Matrix3x2.Identity;
            return false;
        }

        matrix = item.Matrix * item.Transformation * inverse;
        return true;
    }

    /// <summary>Writes the objects, the tail of the content and any appended content.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "mode">The mode.</param>
    internal static void WriteAll(PdfPageContent state, ref PdfContentBuilder builder, PdfRegenerateMode mode)
    {
        if (state.Prepended.Count > 0)
        {
            builder.WriteRaw(CollectionsMarshal.AsSpan(state.Prepended));
        }

        var wrapped = state.Appended.Count > 0;
        for (var i = 0; wrapped && i <= state.Underflow; i++)
        {
            builder.SaveState();
        }

        var position = 0;
        var nextMark = 0;
        foreach (var item in state.ObjectItems)
        {
            position = PdfPageContentWriter.WriteScrubbedBegins(state, ref builder, item.Source.Start, position, ref nextMark);
            if (!PdfPageContentWriter.ShouldWrite(item, mode) || item.Source.Start < position)
            {
                continue;
            }

            builder.WriteRaw(state.SourceBytes.AsSpan(position, item.Source.Start - position));
            PdfPageContentWriter.WriteObject(state, ref builder, item, mode);
            position = item.Source.End;
        }

        position = PdfPageContentWriter.WriteScrubbedBegins(state, ref builder, state.SourceBytes.Length, position, ref nextMark);
        builder.WriteRaw(state.SourceBytes.AsSpan(position));
        if (!wrapped)
        {
            return;
        }

        builder.WriteRaw(PdfPageContentWriter.NewLine);
        for (var i = 0; i <= state.Unclosed; i++)
        {
            builder.RestoreState();
        }

        builder.WriteRaw(CollectionsMarshal.AsSpan(state.Appended));
    }

    /// <summary>Writes the opening operators of marked-content sequences whose text was scrubbed, for those that start before a limit.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "limit">The offset to stop at.</param>
    /// <param name = "position">The offset the content has been written to.</param>
    /// <param name = "next">The index of the next sequence to look at; advanced.</param>
    /// <returns>The offset the content has been written to now.</returns>
    internal static int WriteScrubbedBegins(PdfPageContent state, ref PdfContentBuilder builder, int limit, int position, ref int next)
    {
        while (next < state.Begins.Count && state.Begins[next].BeginSource.End <= limit)
        {
            var mark = state.Begins[next];
            next++;
            if (!(mark.IsScrubbed || mark.IsArtifact) || mark.BeginSource.Start < position)
            {
                continue;
            }

            builder.WriteRaw(state.SourceBytes.AsSpan(position, mark.BeginSource.Start - position));
            PdfPageContentWriter.WriteMarkBegin(state, ref builder, mark);
            position = mark.BeginSource.End;
        }

        return position;
    }

    /// <summary>Writes a marked-content opening without the text that redaction removed: no /ActualText and no /Alt.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "mark">The sequence.</param>
    internal static void WriteMarkBegin(PdfPageContent state, ref PdfContentBuilder builder, PdfMark mark)
    {
        var names = state.Document.Objects.Names;
        builder.WriteRaw(PdfPageContentWriter.NewLine);
        if (mark.IsArtifact)
        {
            builder.BeginMarkedContent("Artifact"u8);
            return;
        }

        var tag = names.GetSpelling(mark.Tag);
        if (mark.Properties is not { } properties)
        {
            builder.BeginMarkedContent(tag);
            return;
        }

        var copy = properties.Clone();
        _ = copy.Remove(KnownName.ActualText);
        _ = copy.Remove(KnownName.Alt);
        if (mark.PropertiesName.IsNone)
        {
            builder.BeginMarkedContent(tag, copy, names);
            return;
        }

        var name = PdfPageContentEditing.AllocateName(state, KnownName.Properties, "RdP");
        state.Generated.Add(new(KnownName.Properties, name, PdfValue.FromDictionary(copy), null));
        builder.BeginMarkedContent(tag, names.GetSpelling(name));
    }

    /// <summary>Writes one object.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "item">The object.</param>
    /// <param name = "mode">The mode.</param>
    internal static void WriteObject(PdfPageContent state, ref PdfContentBuilder builder, PdfPageObject item, PdfRegenerateMode mode)
    {
        builder.WriteRaw(PdfPageContentWriter.NewLine);
        if (item is PdfTextObject text)
        {
            PdfPageContentTextWriter.WriteText(state, ref builder, text);
            return;
        }

        if (item.IsDeleted)
        {
            PdfPageContentPathWriter.WriteClipOnly(ref builder, item);
            return;
        }

        var wrap = item.IsTransformed || item.IsRecoloured || item.IsRestyled;
        if (wrap)
        {
            builder.SaveState();
            PdfPageContentWriter.WriteTransform(ref builder, item);
            PdfPageContentWriter.WritePaints(state, ref builder, item);
            PdfPageContentWriter.WriteStyle(state, ref builder, item);
            PdfPageContentWriter.WriteCrop(ref builder, item);
        }

        PdfPageContentWriter.WriteBody(state, ref builder, item, wrap, mode);
        if (!wrap)
        {
            return;
        }

        builder.RestoreState();
        PdfPageContentPathWriter.WriteClipOnly(ref builder, item);
    }

    /// <summary>Writes the body of an object that is not text.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "item">The object.</param>
    /// <param name = "wrapped">Whether a save and the object's changes were already written.</param>
    /// <param name = "mode">The mode.</param>
    internal static void WriteBody(PdfPageContent state, ref PdfContentBuilder builder, PdfPageObject item, bool wrapped, PdfRegenerateMode mode)
    {
        switch (item)
        {
            case PdfPathObject path:
                {
                    PdfPageContentPathWriter.WritePath(ref builder, path, !wrapped);
                    break;
                }

            case PdfImageObject image:
                {
                    PdfPageContentWriter.WriteImage(state, ref builder, image, mode);
                    break;
                }

            case PdfShadingObject shading:
                {
                    builder.PaintShading(state.Document.Objects.Names.GetSpelling(shading.Name));
                    break;
                }

            case PdfFormObject form:
                {
                    PdfPageContentWriter.WriteForm(state, ref builder, form);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Writes an image: the original bytes, or a <c>Do</c> of the replacement.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "image">The image.</param>
    /// <param name = "mode">The mode.</param>
    internal static void WriteImage(PdfPageContent state, ref PdfContentBuilder builder, PdfImageObject image, PdfRegenerateMode mode)
    {
        if (image.ReplacementImage.IsValid)
        {
            var stored = PdfPageContentEditing.AllocateName(state, KnownName.XObject, "Rg");
            state.Generated.Add(new(KnownName.XObject, stored, PdfValue.FromReference(image.ReplacementImage), null));
            builder.DrawXObject(state.Document.Objects.Names.GetSpelling(stored));
            return;
        }

        if (image.Replacement is { } replacement)
        {
            var name = PdfPageContentEditing.AllocateName(state, KnownName.XObject, "Rg");
            state.Generated.Add(new(KnownName.XObject, name, PdfValue.FromStream(replacement), null));
            builder.DrawXObject(state.Document.Objects.Names.GetSpelling(name));
            return;
        }

        if (image.IsInline || mode == PdfRegenerateMode.Preserve)
        {
            PdfPageContentWriter.WriteSource(state, ref builder, image);
            return;
        }

        builder.DrawXObject(state.Document.Objects.Names.GetSpelling(image.Name));
    }

    /// <summary>Writes a form: the original bytes, or a <c>Do</c> of a new form holding its changed content.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "form">The form.</param>
    internal static void WriteForm(PdfPageContent state, ref PdfContentBuilder builder, PdfFormObject form)
    {
        if ((form.ReadContent is { } changed) && PdfPageContentEditing.IsModified(changed))
        {
            var name = PdfPageContentEditing.AllocateName(state, KnownName.XObject, "Rf");
            state.Generated.Add(new(KnownName.XObject, name, default, changed));
            builder.DrawXObject(state.Document.Objects.Names.GetSpelling(name));
            return;
        }

        builder.DrawXObject(state.Document.Objects.Names.GetSpelling(form.Name));
    }

    /// <summary>Copies an object's original bytes.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "item">The object.</param>
    internal static void WriteSource(PdfPageContent state, ref PdfContentBuilder builder, PdfPageObject item)
    {
        builder.WriteRaw(state.SourceBytes.AsSpan(item.Source.Start, item.Source.Length));
        builder.WriteRaw(PdfPageContentWriter.NewLine);
    }

    /// <summary>Writes the clip that crops an object to part of its frame. The frame is the unit square, which pictures and forms are drawn in.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "item">The object.</param>
    internal static void WriteCrop(ref PdfContentBuilder builder, PdfPageObject item)
    {
        if (item.ChangedCrop is not { } crop)
        {
            return;
        }

        builder.Rectangle(crop.Left, crop.Bottom, 1F - crop.Left - crop.Right, 1F - crop.Top - crop.Bottom);
        builder.Clip();
        builder.EndPath();
    }

    /// <summary>Writes the operators for an object's changed line style and opacity.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "item">The object.</param>
    internal static void WriteStyle(PdfPageContent state, ref PdfContentBuilder builder, PdfPageObject item)
    {
        if (item.ChangedLineStyle is { } line)
        {
            builder.SetLineCap((int)line.Cap);
            builder.SetLineJoin((int)line.Join);
            builder.SetDash(line.Dash, line.Phase);
        }

        if (item.ChangedOpacity is not { } opacity)
        {
            return;
        }

        var objects = state.Document.Objects;
        var name = PdfPageContentEditing.AllocateName(state, KnownName.ExtGState, "RgS");
        var graphicsState = new PdfDictionary(objects);
        graphicsState.Set(KnownName.Type, PdfValue.FromName(KnownName.ExtGState));
        graphicsState.Set(KnownName.CA, PdfValue.FromReal(opacity));
        graphicsState.Set(objects.Names.Intern(FillOpacityKey), PdfValue.FromReal(opacity));
        state.Generated.Add(new(KnownName.ExtGState, name, PdfValue.FromDictionary(graphicsState), null));
        builder.SetGraphicsState(objects.Names.GetSpelling(name));
    }

    /// <summary>Writes the colour operators for an object's changed colours.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "item">The object.</param>
    internal static void WritePaints(PdfPageContent state, ref PdfContentBuilder builder, PdfPageObject item)
    {
        if (item.ChangedFill is { } fill)
        {
            PdfPageContentPaintWriter.WritePaint(state, ref builder, fill, false);
        }

        if (item.ChangedStroke is { } stroke)
        {
            PdfPageContentPaintWriter.WritePaint(state, ref builder, stroke, true);
        }
    }
}
