// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Flattens the annotations of one page: decides what each annotation becomes, draws the appearances, and rewrites the page.</summary>
internal static class PdfFlattenPages
{
    /// <summary>The degrees in a half turn.</summary>
    private const float HalfTurnDegrees = 180;

    /// <summary>Flattens one page, and bakes its rotation when asked.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="copies">Receives the edited page dictionary when the page changed.</param>
    internal static void Apply(PdfFlattenSession session, int pageIndex, Dictionary<int, PdfDictionary> copies)
    {
        var page = PdfDocumentPages.GetPage(session.Document, pageIndex);
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        var bake = session.Options.BakeRotation && page.Rotation != 0;
        var hasAnnotations = annotations is { Count: > 0 };
        if (!hasAnnotations && !bake)
        {
            return;
        }

        var copy = PdfDocumentPageOperations.CloneForEdit(session.Transaction, page);
        var changed = hasAnnotations && FlattenAnnotations(session, page, annotations!, copy);
        if (bake)
        {
            PdfFlattenRotation.Bake(session, page, copy);
            session.RotationsBaked++;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        session.PagesChanged++;
        copies[pageIndex] = copy;
    }

    /// <summary>Draws the page's flattened annotations and removes them from its /Annots.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page as it was before the edit.</param>
    /// <param name="annotations">The page's /Annots.</param>
    /// <param name="copy">The copy of the page dictionary being changed.</param>
    /// <returns><see langword="true"/> when the page changed.</returns>
    private static bool FlattenAnnotations(PdfFlattenSession session, PdfPage page, PdfArray annotations, PdfDictionary copy)
    {
        var entries = Plan(session, page, annotations);
        Account(session, page.Index, entries);
        RemovePopups(session, entries);
        var draws = BuildDraws(session, page, entries);
        if (draws.Count == 0 && !entries.Exists(static entry => entry.Decision.Action != PdfFlattenAction.Keep))
        {
            return false;
        }

        if (draws.Count > 0)
        {
            PdfFlattenContent.Add(session, page, copy, draws);
        }

        var kept = BuildKept(session, entries);
        if (kept.Count > 0)
        {
            copy.Set(KnownName.Annots, PdfValue.FromArray(kept));
        }
        else
        {
            _ = copy.Remove(KnownName.Annots);
        }

        if (page.Dictionary.GetRaw(KnownName.Annots).IsReference)
        {
            session.Candidates.Add(page.Dictionary.GetRaw(KnownName.Annots).AsReference());
        }

        if (session.Structure?.GetNewPageKey(page.Index) is >= 0 and var key)
        {
            copy.Set(KnownName.StructParents, PdfValue.FromInteger(key));
        }

        return true;
    }

    /// <summary>Decides what happens to each annotation of a page.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotations">The page's /Annots.</param>
    /// <returns>An entry for each annotation, in order.</returns>
    private static List<PdfFlattenEntry> Plan(PdfFlattenSession session, PdfPage page, PdfArray annotations)
    {
        var entries = new List<PdfFlattenEntry>(annotations.Count);
        for (var i = 0; i < annotations.Count; i++)
        {
            session.CancellationToken.ThrowIfCancellationRequested();
            var annotation = annotations.GetDictionary(i);
            var raw = annotations.GetRaw(i);
            entries.Add(annotation is null
                ? new(i, raw, null, PdfFlattenDecision.OutOfScope, null)
                : PlanOne(session, page, annotations, i, annotation));
        }

        return entries;
    }

    /// <summary>Decides what happens to one annotation, finding the appearance it is drawn from.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotations">The page's /Annots.</param>
    /// <param name="index">The annotation's index.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The entry.</returns>
    private static PdfFlattenEntry PlanOne(PdfFlattenSession session, PdfPage page, PdfArray annotations, int index, PdfDictionary annotation)
    {
        var raw = annotations.GetRaw(index);
        var decision = PdfFlattenSelection.Decide(session.Options, annotation);
        if (decision.Action != PdfFlattenAction.Draw)
        {
            return new(index, raw, annotation, decision, null);
        }

        // ISO 19005-1 6.1.13 forbids optional content, so a file that claims PDF/A-1 cannot have it in a drawing it keeps.
        if (session.PdfAPart == 1 && annotation.ContainsKey(KnownName.OC))
        {
            return new(index, raw, annotation, PdfFlattenDecision.Keep(PdfFlattenSkipReason.LayerNotAllowed), null);
        }

        var appearance = PdfFlattenAppearance.Resolve(session, page, annotations, index, out var reason);
        if (appearance is not null)
        {
            return new(index, raw, annotation, decision, appearance);
        }

        // A widget with nothing to show still leaves with its form field; any other annotation stays.
        var isWidget = annotation.IsName(KnownName.Subtype, KnownName.Widget);
        return new(index, raw, annotation, isWidget ? PdfFlattenDecision.Remove(reason) : PdfFlattenDecision.Keep(reason), null);
    }

    /// <summary>Counts what each annotation becomes and notes the objects and widgets that leave the document.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="entries">The page's entries.</param>
    private static void Account(PdfFlattenSession session, int pageIndex, List<PdfFlattenEntry> entries)
    {
        foreach (var entry in entries)
        {
            var decision = entry.Decision;
            if (decision.HasReason)
            {
                session.Skipped.Add(new(pageIndex, entry.Index, entry.GetSubtype(session.Document.Objects.Names), decision.Reason, decision.Action == PdfFlattenAction.Remove));
            }

            if (decision.Action == PdfFlattenAction.Keep)
            {
                continue;
            }

            Release(session, entry);
            if (decision.Action == PdfFlattenAction.Remove)
            {
                session.WidgetsRemoved++;
            }
            else if (entry.IsWidget)
            {
                session.WidgetsDrawn++;
            }
            else
            {
                session.AnnotationsDrawn++;
            }
        }
    }

    /// <summary>Notes that an annotation leaves the page and lists the objects only it used.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="entry">The entry.</param>
    private static void Release(PdfFlattenSession session, in PdfFlattenEntry entry)
    {
        if (entry.Annotation is not { } annotation)
        {
            return;
        }

        if (entry.Raw.IsReference)
        {
            var id = entry.Raw.AsReference();
            _ = session.RemovedAnnotations.Add(id.Number);
            session.Candidates.Add(id);
            if (entry.IsWidget)
            {
                _ = session.RemovedWidgets.Add(id.Number);
            }
        }

        AddAppearanceObjects(session.Candidates, annotation);
    }

    /// <summary>Lists the appearance streams an annotation refers to.</summary>
    /// <param name="output">Receives the objects.</param>
    /// <param name="annotation">The annotation.</param>
    private static void AddAppearanceObjects(List<PdfObjectId> output, PdfDictionary annotation)
    {
        if (annotation.GetDictionary(KnownName.AP) is not { } appearances)
        {
            return;
        }

        for (var i = 0; i < appearances.Count; i++)
        {
            var value = appearances.GetValueAt(i);
            if (value.IsReference)
            {
                output.Add(value.AsReference());
            }
            else if (value.AsDictionary() is { } states)
            {
                for (var j = 0; j < states.Count; j++)
                {
                    var state = states.GetValueAt(j);
                    if (state.IsReference)
                    {
                        output.Add(state.AsReference());
                    }
                }
            }
        }
    }

    /// <summary>Removes the popups of annotations that leave the page, and the annotations that were replies to them lose their link.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="entries">The page's entries, changed in place.</param>
    private static void RemovePopups(PdfFlattenSession session, List<PdfFlattenEntry> entries)
    {
        if (session.RemovedAnnotations.Count == 0)
        {
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            if (!IsPopupOfRemoved(session, entries[i]))
            {
                continue;
            }

            entries[i] = entries[i] with { Decision = new(PdfFlattenAction.Remove, false, default) };
            Release(session, entries[i]);
        }
    }

    /// <summary>Determines whether an annotation is a popup of an annotation that left the page.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="entry">The entry.</param>
    /// <returns><see langword="true"/> for a popup that stayed and whose parent was removed.</returns>
    private static bool IsPopupOfRemoved(PdfFlattenSession session, in PdfFlattenEntry entry) =>
        entry.Annotation is { } popup && entry.Decision.Action == PdfFlattenAction.Keep && popup.IsName(KnownName.Subtype, KnownName.Popup)
        && session.RemovedAnnotations.Contains(popup.GetRaw(KnownName.Parent).AsReference().Number);

    /// <summary>Makes the drawings of the annotations that are drawn: others first, then form field widgets, as the viewer draws them.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page.</param>
    /// <param name="entries">The page's entries.</param>
    /// <returns>The drawings in order.</returns>
    private static List<PdfFlattenDraw> BuildDraws(PdfFlattenSession session, PdfPage page, List<PdfFlattenEntry> entries)
    {
        var draws = new List<PdfFlattenDraw>();
        foreach (var entry in entries)
        {
            if (entry.Decision.Action == PdfFlattenAction.Remove)
            {
                _ = session.Structure?.Release(page.Index, page, entry.Annotation!, entry.Raw.AsReference(), false);
            }
        }

        foreach (var widgets in (ReadOnlySpan<bool>)[false, true])
        {
            foreach (var entry in entries)
            {
                if (entry.Decision.Action == PdfFlattenAction.Draw && entry.IsWidget == widgets)
                {
                    draws.Add(CreateDraw(session, page, entry));
                }
            }
        }

        return draws;
    }

    /// <summary>Makes the drawing of one annotation.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The drawing.</returns>
    private static PdfFlattenDraw CreateDraw(PdfFlattenSession session, PdfPage page, in PdfFlattenEntry entry)
    {
        var annotation = entry.Annotation!;
        var appearance = entry.Appearance!;
        var marking = PdfFlattenMarking.None;
        if (session.Structure is { } structure)
        {
            marking = structure.Release(page.Index, page, annotation, entry.Raw.AsReference(), true);
        }

        var primary = appearance.Primary;
        var overlay = appearance.Overlay;
        if (NeedsUpright(annotation, page) && annotation.TryGetRectangle(KnownName.Rect, out var rect))
        {
            primary = primary with { Mapping = Upright(primary.Mapping, rect, page.Rotation) };
            overlay = overlay is { } over ? over with { Mapping = Upright(over.Mapping, rect, page.Rotation) } : null;
        }

        return new(primary, overlay, annotation.GetRaw(KnownName.OC), marking, GetBlend(session, annotation));
    }

    /// <summary>Gets the blend mode an annotation is painted with (ISO 32000-2 12.5.2, 11.3.5).</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The blend mode, or none when it is Normal, absent or not allowed: PDF/A-1 permits only Normal and Compatible (ISO 19005-1 6.4).</returns>
    private static PdfName GetBlend(PdfFlattenSession session, PdfDictionary annotation)
    {
        var blend = annotation.GetName(KnownName.BM);
        return session.PdfAPart == 1 || blend.IsNone || blend == KnownName.Normal || blend == KnownName.Compatible ? default : blend;
    }

    /// <summary>Determines whether an annotation stays upright on a rotated page (the NoRotate flag).</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="page">The page.</param>
    /// <returns><see langword="true"/> when the page is rotated and the flag is set.</returns>
    private static bool NeedsUpright(PdfDictionary annotation, PdfPage page) =>
        page.Rotation != 0 && (annotation.GetInt32(KnownName.F) & (int)PdfAnnotationFlags.NoRotate) != 0;

    /// <summary>Turns a mapping about the rectangle's top left corner, against the page's rotation, as the viewer does.</summary>
    /// <param name="mapping">The mapping.</param>
    /// <param name="rect">The annotation's rectangle.</param>
    /// <param name="rotation">The page's rotation in degrees.</param>
    /// <returns>The turned mapping.</returns>
    private static Matrix3x2 Upright(Matrix3x2 mapping, PdfRectangle rect, int rotation)
    {
        var corner = new Vector2(Math.Min(rect.Left, rect.Right), Math.Max(rect.Top, rect.Bottom));
        return mapping * Matrix3x2.CreateTranslation(-corner) * Matrix3x2.CreateRotation(rotation * MathF.PI / HalfTurnDegrees) * Matrix3x2.CreateTranslation(corner);
    }

    /// <summary>Makes the page's new /Annots: the annotations that stay, with replies to removed annotations unlinked.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="entries">The page's entries.</param>
    /// <returns>The array.</returns>
    private static PdfArray BuildKept(PdfFlattenSession session, List<PdfFlattenEntry> entries)
    {
        var kept = new PdfArray(session.Document.Objects, entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Decision.Action != PdfFlattenAction.Keep)
            {
                continue;
            }

            kept.Add(IsReplyToRemoved(session, entry.Annotation) ? Unlink(session, entry) : entry.Raw);
        }

        return kept;
    }

    /// <summary>Determines whether an annotation replies to an annotation that left the page.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="annotation">The annotation, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when its /IRT names a removed annotation.</returns>
    private static bool IsReplyToRemoved(PdfFlattenSession session, PdfDictionary? annotation) =>
        annotation is not null && session.RemovedAnnotations.Contains(annotation.GetRaw(KnownName.IRT).AsReference().Number);

    /// <summary>Removes the /IRT and /RT of a reply whose parent was flattened, so it no longer refers to a removed object.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="entry">The reply.</param>
    /// <returns>The entry for the page's /Annots.</returns>
    private static PdfValue Unlink(PdfFlattenSession session, in PdfFlattenEntry entry)
    {
        var copy = entry.Annotation!.Clone();
        _ = copy.Remove(KnownName.IRT);
        _ = copy.Remove(KnownName.RT);
        if (!entry.Raw.IsReference)
        {
            return PdfValue.FromDictionary(copy);
        }

        session.Transaction.Replace(entry.Raw.AsReference(), PdfValue.FromDictionary(copy));
        return entry.Raw;
    }
}
