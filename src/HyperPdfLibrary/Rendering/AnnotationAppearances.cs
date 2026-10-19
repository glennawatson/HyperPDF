// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Generates the appearance of an annotation that has none (ISO 32000-2 12.5.5): the subtype's plan is read from the
/// dictionary once, written into a pooled content buffer in one pass, and wrapped in an in-memory form. Results are
/// cached per annotation and object revision in <see cref="GeneratedAppearanceCache"/>.
/// </summary>
internal static class AnnotationAppearances
{
    /// <summary>Gets the appearance of an annotation that has no normal appearance.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or <see langword="null"/> when the subtype is not drawn or the annotation lacks what it needs.</returns>
    internal static GeneratedAppearance? Generate(in AnnotationContext context)
    {
        // The revision is read before the annotation, so an edit that lands while this runs makes the entry stale, never fresh.
        var revision = Volatile.Read(ref context.Cache.Document.State.ObjectsRevision);
        return context.Cache.Appearances.GetOrCreate(context.Annotation, revision, context, static state => Create(state));
    }

    /// <summary>Wraps content in an in-memory uncompressed form XObject.</summary>
    /// <param name="content">The content.</param>
    /// <param name="context">The annotation.</param>
    /// <param name="box">The <c>/BBox</c>.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <returns>The stream.</returns>
    internal static PdfStream CreateForm(byte[] content, in AnnotationContext context, PdfRectangle box, PdfDictionary? resources)
    {
        var dictionary = new PdfDictionary(context.Store);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Form));
        dictionary.Set(KnownName.BBox, PdfValue.FromArray(box.ToArray(context.Store)));
        if (resources is not null)
        {
            dictionary.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        }

        return new(dictionary, content);
    }

    /// <summary>Gets the resources of a generated appearance: one graphics state with the annotation's opacity (<c>/CA</c>) and a blend mode.</summary>
    /// <param name="context">The annotation.</param>
    /// <param name="blend">The blend mode.</param>
    /// <returns>The resources.</returns>
    internal static PdfDictionary GetResources(in AnnotationContext context, KnownName blend)
    {
        var cache = context.Cache;
        var opacity = context.Annotation.Get(KnownName.CA);
        return cache.Appearances.GetResources(context.Store, cache.GraphicsStateName, cache.LowerCa, opacity.IsNull ? 1 : opacity.AsSingle(1), blend);
    }

    /// <summary>Reads a plan and writes its content into a pooled buffer.</summary>
    /// <typeparam name="TPlan">The plan type.</typeparam>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or <see langword="null"/> when the plan cannot be read.</returns>
    internal static GeneratedAppearance? Build<TPlan>(in AnnotationContext context)
        where TPlan : struct, IAppearancePlan<TPlan>
    {
        if (!TPlan.TryRead(context.Annotation, out var plan))
        {
            return null;
        }

        var builder = default(PdfContentBuilder);
        try
        {
            // The graphics state carries the annotation's opacity and blend mode.
            builder.SetGraphicsState("GS"u8);
            plan.Emit(ref builder);
            var form = CreateForm(builder.ToArray(), context, plan.Bounds, GetResources(context, plan.Blend));
            return new(form, null, plan.Bounds);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Selects the plan for an annotation's subtype and generates its appearance.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or <see langword="null"/>.</returns>
    private static GeneratedAppearance? Create(in AnnotationContext context) => context.Annotation.GetName(KnownName.Subtype).ToKnownName() switch
    {
        KnownName.Highlight or KnownName.Underline or KnownName.StrikeOut or KnownName.Squiggly => Build<MarkupPlan>(context),
        KnownName.Square or KnownName.Circle or KnownName.Polygon or KnownName.PolyLine or KnownName.Line => Build<ShapePlan>(context),
        KnownName.Ink => Build<InkPlan>(context),
        KnownName.Text => Build<NotePlan>(context),
        KnownName.FileAttachment => Build<AttachmentPlan>(context),
        KnownName.Link => Build<LinkPlan>(context),
        KnownName.Caret => Build<CaretPlan>(context),
        KnownName.Redact => Build<RedactPlan>(context),
        KnownName.FreeText => FreeTextAppearance.Create(context),
        KnownName.Widget => WidgetAppearance.Create(context),
        _ => null,
    };
}
