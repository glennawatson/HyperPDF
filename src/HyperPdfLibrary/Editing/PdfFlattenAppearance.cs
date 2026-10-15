// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Finds the appearance a flatten draws for an annotation: the normal appearance it has (ISO 32000-2 12.5.5), or one drawn
/// for it when it has none, from the field value for form fields and in the shape the viewer draws for other types.
/// </summary>
/// <param name="Primary">The appearance and its matrix.</param>
/// <param name="Overlay">A second appearance drawn over the first, or null.</param>
/// <param name="Generated">Whether the appearance was drawn for the annotation, which had none.</param>
[DebuggerDisplay("PdfFlattenAppearance: {Primary}")]
internal sealed record PdfFlattenAppearance(PdfFlattenForm Primary, PdfFlattenForm? Overlay, bool Generated)
{
    /// <summary>Finds the appearance to draw for an annotation.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page the annotation is on.</param>
    /// <param name="annotations">The page's /Annots.</param>
    /// <param name="index">The annotation's index in the array.</param>
    /// <param name="reason">Why there is nothing to draw, when the result is <see langword="null"/>.</param>
    /// <returns>The appearance, or <see langword="null"/> when there is nothing to draw.</returns>
    internal static PdfFlattenAppearance? Resolve(PdfFlattenSession session, PdfPage page, PdfArray annotations, int index, out PdfFlattenSkipReason reason)
    {
        reason = PdfFlattenSkipReason.NoAppearance;
        var annotation = annotations.GetDictionary(index)!;
        if (!annotation.TryGetRectangle(KnownName.Rect, out var rect) || rect.IsEmpty)
        {
            reason = PdfFlattenSkipReason.UnusableAppearance;
            return null;
        }

        var widget = annotation.IsName(KnownName.Subtype, KnownName.Widget) ? PdfFormReading.Resolve(session.Form, page.Index, annotations, index) : null;
        var stream = PageRecorder.GetAppearance(annotation, session.Document.Objects.Names, out var raw);
        if (stream is not null && !ShouldRedraw(session, widget))
        {
            return FromStream(session, widget is not null, rect, raw, stream, out reason);
        }

        // An /N dictionary without the current state shows nothing; the viewer never draws one in its place.
        return stream is null && annotation.GetDictionary(KnownName.AP)?.Get(KnownName.N).AsDictionary() is not null
            ? null
            : Generate(session, page, annotation, widget, rect, out reason);
    }

    /// <summary>Determines whether a form field is drawn again although it has an appearance.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="widget">The widget, or <see langword="null"/> for another annotation.</param>
    /// <returns><see langword="true"/> when the options ask for it, the form asks readers to, and the field is drawn from its value.</returns>
    private static bool ShouldRedraw(PdfFlattenSession session, ResolvedWidget? widget) =>
        session.Options.RedrawFields && session.PdfAPart == 0 && widget is { } resolved && IsDrawnFromValue(resolved.Type)
        && PdfFormReading.NeedAppearances(session.Form);

    /// <summary>Determines whether a field type is drawn from its value.</summary>
    /// <param name="type">The field type.</param>
    /// <returns><see langword="true"/> for text, choice and push button fields.</returns>
    private static bool IsDrawnFromValue(PdfFieldType type) => type is PdfFieldType.Text or PdfFieldType.ComboBox or PdfFieldType.ListBox or PdfFieldType.PushButton;

    /// <summary>Fits an appearance the annotation already has to its rectangle.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="isWidget">Whether the annotation is a widget.</param>
    /// <param name="rect">The annotation's rectangle.</param>
    /// <param name="raw">The appearance as stored.</param>
    /// <param name="stream">The appearance stream.</param>
    /// <param name="reason">Why it cannot be drawn.</param>
    /// <returns>The appearance, or <see langword="null"/>.</returns>
    private static PdfFlattenAppearance? FromStream(PdfFlattenSession session, bool isWidget, PdfRectangle rect, PdfValue raw, PdfStream stream, out PdfFlattenSkipReason reason)
    {
        reason = PdfFlattenSkipReason.UnusableAppearance;
        return raw.IsReference && PageRecorder.TryMapAppearance(stream.Dictionary, rect, out var mapping)
            ? new(new(Repair(session, isWidget, raw, stream), mapping), null, false)
            : null;
    }

    /// <summary>Draws an appearance for an annotation that has none.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="widget">The widget, or <see langword="null"/> for another annotation.</param>
    /// <param name="rect">The annotation's rectangle.</param>
    /// <param name="reason">Why nothing was drawn.</param>
    /// <returns>The appearance, or <see langword="null"/>.</returns>
    private static PdfFlattenAppearance? Generate(PdfFlattenSession session, PdfPage page, PdfDictionary annotation, ResolvedWidget? widget, PdfRectangle rect, out PdfFlattenSkipReason reason)
    {
        reason = PdfFlattenSkipReason.NoAppearance;
        if (widget is not null)
        {
            return GenerateWidget(session, page, widget.Value, rect, out reason);
        }

        var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
        if (session.PdfAPart > 0 && subtype == KnownName.FreeText)
        {
            reason = PdfFlattenSkipReason.NeedsFont;
            return null;
        }

        if (AnnotationAppearances.Generate(new(session.RenderCache, page, annotation)) is not { } generated)
        {
            return null;
        }

        var primary = Place(session, generated.Form, generated.Rect);
        var overlay = generated.Overlay is { } over ? Place(session, over, generated.Rect) : null;
        if (primary is null)
        {
            reason = PdfFlattenSkipReason.UnusableAppearance;
            return null;
        }

        session.AppearancesDrawn++;
        return new(primary.Value, overlay, true);
    }

    /// <summary>Draws the appearance of a form field from its value.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page.</param>
    /// <param name="widget">The widget.</param>
    /// <param name="rect">The widget's rectangle.</param>
    /// <param name="reason">Why nothing was drawn.</param>
    /// <returns>The appearance, or <see langword="null"/>.</returns>
    private static PdfFlattenAppearance? GenerateWidget(PdfFlattenSession session, PdfPage page, in ResolvedWidget widget, PdfRectangle rect, out PdfFlattenSkipReason reason)
    {
        reason = PdfFlattenSkipReason.NoAppearance;
        if (!IsDrawnFromValue(widget.Type) && !widget.IsButton)
        {
            return null;
        }

        if (session.PdfAPart > 0)
        {
            reason = PdfFlattenSkipReason.NeedsFont;
            return null;
        }

        var store = session.Document.Objects;
        var context = new AppearanceContext(store, widget.Widget, widget.Field, widget.Type, widget.Flags, store.Catalog.GetDictionary(KnownName.AcroForm), page.Resources)
        {
            Fonts = session.RenderCache.Fonts,
            CancellationToken = session.CancellationToken,
        };
        var stream = widget.IsButton ? FormButtonAppearance.CreateButton(context, IsOn(store.Names, widget)) : PdfFormEditing.CreateAppearance(context);
        var placed = Place(session, stream, rect);
        if (placed is null)
        {
            reason = PdfFlattenSkipReason.UnusableAppearance;
            return null;
        }

        session.AppearancesDrawn++;
        return new(placed.Value, null, true);
    }

    /// <summary>Determines whether a check box or radio button without appearances is switched on.</summary>
    /// <param name="names">The document's names.</param>
    /// <param name="widget">The widget.</param>
    /// <returns><see langword="true"/> when its state, or its field's value when it has no state, is not Off.</returns>
    private static bool IsOn(PdfNameTable names, in ResolvedWidget widget)
    {
        var state = FieldAttributes.ReadState(widget.Widget.Get(KnownName.AS), names);
        if (state.Length > 0)
        {
            return !string.Equals(state, FormValues.OffState, StringComparison.Ordinal);
        }

        var value = FieldAttributes.ReadState(FieldAttributes.Find(widget.Field, KnownName.V), names);
        return value.Length > 0 && !string.Equals(value, FormValues.OffState, StringComparison.Ordinal);
    }

    /// <summary>Adds a drawn appearance to the document and fits it to a rectangle.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="stream">The appearance.</param>
    /// <param name="rect">The rectangle.</param>
    /// <returns>The form, or <see langword="null"/> when the appearance has no usable box.</returns>
    private static PdfFlattenForm? Place(PdfFlattenSession session, PdfStream stream, PdfRectangle rect) =>
        PageRecorder.TryMapAppearance(stream.Dictionary, rect, out var mapping)
            ? new(PdfValue.FromReference(session.Transaction.Add(PdfValue.FromStream(stream))), mapping)
            : null;

    /// <summary>
    /// Makes an appearance stream drawable by a page: it gets the form type it must have, and a form field's appearance
    /// that names no resources gets the form's default resources (ISO 32000-2 12.7.3.3), which a viewer would use for it.
    /// </summary>
    /// <param name="session">The flatten.</param>
    /// <param name="isWidget">Whether the stream belongs to a widget.</param>
    /// <param name="raw">The stream as stored.</param>
    /// <param name="stream">The stream.</param>
    /// <returns>A reference to the stream, or to a repaired copy of it.</returns>
    private static PdfValue Repair(PdfFlattenSession session, bool isWidget, PdfValue raw, PdfStream stream)
    {
        var dictionary = stream.Dictionary;
        var defaults = isWidget && !dictionary.ContainsKey(KnownName.Resources) ? session.Document.Catalog.GetDictionary(KnownName.AcroForm)?.GetRaw(KnownName.DR) ?? default : default;
        var isForm = dictionary.IsName(KnownName.Subtype, KnownName.Form);
        if (isForm && defaults.IsNull)
        {
            return raw;
        }

        var number = raw.AsReference().Number;
        if (session.Repaired.TryGetValue(number, out var done))
        {
            return done;
        }

        var copy = dictionary.Clone();
        if (!isForm)
        {
            copy.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
            copy.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Form));
        }

        if (!defaults.IsNull)
        {
            copy.Set(KnownName.Resources, defaults);
        }

        var repaired = PdfValue.FromReference(session.Transaction.Add(PdfValue.FromStream(new(copy, stream.CopyRawData()))));
        session.Repaired[number] = repaired;
        return repaired;
    }
}
