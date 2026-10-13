// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Draws the appearance streams of fields the library creates or changes and attaches them to the widgets (ISO 32000-2
/// 12.5.5 and 12.7.4). A check box or radio button gets a dictionary of an on-state and an <c>Off</c> state; every other
/// field gets one stream. A stream the form drew earlier is replaced in place, so repeated edits do not pile up objects.
/// </summary>
internal static class FormFieldAppearances
{
    /// <summary>Draws the appearance of a widget and sets its <c>/AP</c> and, for a button, its <c>/AS</c>.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction receiving the streams.</param>
    /// <param name="request">What kind of field the widget belongs to.</param>
    /// <param name="widget">The widget dictionary, changed in place; it must not be in the store yet.</param>
    /// <param name="pageIndex">The widget's page.</param>
    /// <param name="state">The state the widget starts in, or <see langword="null"/> for a widget that is not a button.</param>
    internal static void Attach(PdfDocument document, PdfEditTransaction transaction, FormAppearanceRequest request, PdfDictionary widget, int pageIndex, string? state)
    {
        var store = document.Objects;
        var drawn = PdfDocumentForms.GetForm(document).Drawn;
        var form = document.Catalog.GetDictionary(KnownName.AcroForm);
        var resources = PdfDocumentPages.GetPage(document, pageIndex).Resources;
        var context = new AppearanceContext(store, widget, request.Field, request.Type, request.Flags, form, resources)
        {
            Fonts = PdfDocumentRendering.GetRenderCache(document).Fonts,
            CancellationToken = request.CancellationToken,
        };
        var previous = widget.GetDictionary(KnownName.AP)?.GetRaw(KnownName.N) ?? default;
        var appearance = new PdfDictionary(store);
        if (request.Type is PdfFieldType.CheckBox or PdfFieldType.RadioButton)
        {
            AttachButton(transaction, request, new(context, drawn), previous, appearance);
            widget.Set(KnownName.AS, PdfValue.FromName(store.Names.Intern(state ?? FormValues.OffState)));
        }
        else
        {
            var stream = request.Type == PdfFieldType.Signature ? FormSignatureAppearance.Create(context) : PdfFormEditing.CreateAppearance(context);
            appearance.Set(KnownName.N, StoreStream(transaction, drawn, previous, stream));
            FormDefaultResources.AddFonts(store, stream);
        }

        widget.Set(KnownName.AP, PdfValue.FromDictionary(appearance));
    }

    /// <summary>Stores an appearance stream: in the object a stream the form drew earlier is in, otherwise as a new object.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="drawn">The numbers of the appearance streams the form drew.</param>
    /// <param name="previous">The widget's earlier appearance entry, as stored.</param>
    /// <param name="stream">The new stream.</param>
    /// <returns>A reference to the stream's object.</returns>
    private static PdfValue StoreStream(PdfEditTransaction transaction, HashSet<int> drawn, PdfValue previous, PdfStream stream)
    {
        if (previous.IsReference && drawn.Contains(previous.AsReference().Number))
        {
            transaction.Replace(previous.AsReference(), PdfValue.FromStream(stream));
            return previous;
        }

        var id = transaction.Add(PdfValue.FromStream(stream));
        _ = drawn.Add(id.Number);
        return PdfValue.FromReference(id);
    }

    /// <summary>Draws the on and off states of a button.</summary>
    /// <param name="transaction">The transaction receiving the streams.</param>
    /// <param name="request">The field.</param>
    /// <param name="drawing">The widget and the streams the form drew.</param>
    /// <param name="previous">The widget's earlier <c>/N</c> entry, as stored.</param>
    /// <param name="appearance">The <c>/AP</c> dictionary being built.</param>
    private static void AttachButton(PdfEditTransaction transaction, FormAppearanceRequest request, in ButtonDrawing drawing, PdfValue previous, PdfDictionary appearance)
    {
        var store = drawing.Context.Store;
        var earlier = StoreReading.Resolve(store, previous).AsDictionary();
        var on = store.Names.Intern(request.OnState ?? FormValues.DefaultOnState);
        var off = store.Names.Intern(FormValues.OffState);
        var states = new PdfDictionary(store);
        states.Set(on, StoreStream(transaction, drawing.Drawn, earlier?.GetRaw(on) ?? default, FormButtonAppearance.CreateButton(drawing.Context, true)));
        states.Set(off, StoreStream(transaction, drawing.Drawn, earlier?.GetRaw(off) ?? default, FormButtonAppearance.CreateButton(drawing.Context, false)));
        appearance.Set(KnownName.N, PdfValue.FromDictionary(states));
    }

    /// <summary>A widget and the appearance streams the form drew, for drawing a button's states.</summary>
    /// <param name="Context">The widget.</param>
    /// <param name="Drawn">The numbers of the appearance streams the form drew.</param>
    private readonly record struct ButtonDrawing(AppearanceContext Context, HashSet<int> Drawn);
}
