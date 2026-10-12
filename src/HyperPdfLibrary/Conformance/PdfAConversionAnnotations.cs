// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Makes annotations meet PDF/A (ISO 19005-1 §6.5; ISO 19005-2 and -3 §6.3; ISO 19005-4:2020 §6.3): each prints and is
/// not hidden, keeps only its normal appearance, and drops colour arrays an RGB output intent cannot describe.
/// Annotations that are hidden anyway are removed, as showing them would change the page. Annotation types a level
/// forbids, annotations with no appearance, and transparent annotations in PDF/A-1 stop the conversion.
/// </summary>
internal static class PdfAConversionAnnotations
{
    /// <summary>The Invisible flag.</summary>
    private const int Invisible = 1;

    /// <summary>The Hidden flag.</summary>
    private const int Hidden = 2;

    /// <summary>The Print flag.</summary>
    private const int Print = 4;

    /// <summary>The NoView flag.</summary>
    private const int NoView = 32;

    /// <summary>The ToggleNoView flag.</summary>
    private const int ToggleNoView = 256;

    /// <summary>Applies the fixes to every page.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="intentSpace">The colour space of the PDF/A output intent's profile, such as "RGB ".</param>
    internal static void Apply(PdfAConversionSession session, string? intentSpace)
    {
        var tally = new PdfAConversionAnnotationTally();
        var document = session.Document;
        for (var page = 0; page < document.PageCount; page++)
        {
            session.CancellationToken.ThrowIfCancellationRequested();
            FixPage(session, PdfDocumentPages.GetPage(document, page).Id, intentSpace, tally);
        }

        Report(session, tally);
    }

    /// <summary>Fixes the annotations of one page.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="pageId">The page object.</param>
    /// <param name="intentSpace">The output intent's colour space.</param>
    /// <param name="tally">The counts of each kind of change.</param>
    private static void FixPage(PdfAConversionSession session, PdfObjectId pageId, string? intentSpace, PdfAConversionAnnotationTally tally)
    {
        var store = session.Store;
        if (StoreReading.GetObject(store, pageId).AsDictionary() is not { } page)
        {
            return;
        }

        var annotsRaw = page.GetRaw(KnownName.Annots);
        if (StoreReading.Resolve(store, annotsRaw).AsArray() is not { } annotations)
        {
            return;
        }

        var kept = new PdfArray(store, annotations.Count);
        var removed = new HashSet<int>();
        for (var i = 0; i < annotations.Count; i++)
        {
            var item = annotations.GetRaw(i);
            if (StoreReading.Resolve(store, item).AsDictionary() is not { } annotation)
            {
                kept.Add(item);
                continue;
            }

            var fixedAnnotation = FixAnnotation(session, annotation, intentSpace, tally);
            if (fixedAnnotation is null)
            {
                _ = removed.Add(item.IsReference ? item.AsReference().Number : -1);
                continue;
            }

            kept.Add(Store(store, item, annotation, fixedAnnotation));
        }

        RemoveOrphanPopups(store, kept, removed);
        if (kept.Count != annotations.Count || HasChanged(kept, annotations))
        {
            WriteAnnots(store, pageId, page, annotsRaw, kept);
        }
    }

    /// <summary>Fixes one annotation.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="intentSpace">The output intent's colour space.</param>
    /// <param name="tally">The counts.</param>
    /// <returns>The annotation, a fixed copy, or <see langword="null"/> when it is removed.</returns>
    private static PdfDictionary? FixAnnotation(PdfAConversionSession session, PdfDictionary annotation, string? intentSpace, PdfAConversionAnnotationTally tally)
    {
        var subtype = session.Store.Names.GetString(annotation.GetName(KnownName.Subtype));
        if (!PdfAConversionAnnotationTypes.IsAllowed(session.Level, subtype))
        {
            _ = tally.Forbidden.Add(subtype.Length == 0 ? "untyped" : subtype);
            return annotation;
        }

        var flags = annotation.Get(KnownName.F);
        var value = flags.IsNumber ? (int)flags.AsInteger() : 0;
        if ((value & (Hidden | NoView)) != 0 && subtype != "Popup")
        {
            return RemoveHidden(annotation, subtype, tally);
        }

        var copy = annotation.Clone();
        var zeroSize = IsZeroSize(annotation);
        FixFlags(session, copy, subtype, zeroSize, tally);
        if (session.Part == PdfAClaim.Part1 && annotation.Get(KnownName.CA) is { IsNumber: true } alpha && alpha.AsNumber() < 1)
        {
            tally.Transparent++;
        }

        if (intentSpace != "RGB ")
        {
            RemoveColours(copy, tally);
        }

        PdfAConversionAppearances.Fix(session, annotation, copy, subtype, zeroSize, tally);
        return copy;
    }

    /// <summary>Removes a hidden annotation, which nobody sees; a hidden form field instead stops the conversion.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="subtype">Its type.</param>
    /// <param name="tally">The counts.</param>
    /// <returns><see langword="null"/> to remove it, or the annotation to keep it.</returns>
    private static PdfDictionary? RemoveHidden(PdfDictionary annotation, string subtype, PdfAConversionAnnotationTally tally)
    {
        if (subtype == "Widget")
        {
            tally.HiddenFields++;
            return annotation;
        }

        tally.Removed++;
        return null;
    }

    /// <summary>Sets Print and clears the hiding flags, where the level needs /F.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="copy">The annotation copy.</param>
    /// <param name="subtype">Its type.</param>
    /// <param name="zeroSize">Whether it has no area.</param>
    /// <param name="tally">The counts.</param>
    private static void FixFlags(PdfAConversionSession session, PdfDictionary copy, string subtype, bool zeroSize, PdfAConversionAnnotationTally tally)
    {
        var flags = copy.Get(KnownName.F);
        var value = flags.IsNumber ? (int)flags.AsInteger() : 0;
        var exempt = subtype == "Popup" || (zeroSize && session.Part == PdfAClaim.Part4);
        var needsFlags = flags.IsNumber || session.Part == PdfAClaim.Part1 || !exempt;
        var newFlags = (value | Print) & ~(Invisible | Hidden | NoView | ToggleNoView);
        if (!needsFlags || (flags.IsNumber && newFlags == value))
        {
            return;
        }

        copy.Set(KnownName.F, PdfValue.FromInteger(newFlags));
        tally.Flags++;
    }

    /// <summary>Removes the colour arrays, which only an RGB output intent may describe.</summary>
    /// <param name="copy">The annotation copy.</param>
    /// <param name="tally">The counts.</param>
    private static void RemoveColours(PdfDictionary copy, PdfAConversionAnnotationTally tally)
    {
        var removedC = copy.Remove(KnownName.C);
        var removedIc = copy.Remove(KnownName.IC);
        tally.Colours += removedC || removedIc ? 1 : 0;
    }

    /// <summary>Determines whether an annotation's rectangle has no width and no height.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsZeroSize(PdfDictionary annotation) =>
        annotation.TryGetRectangle(KnownName.Rect, out var rect) && rect.Width == 0 && rect.Height == 0;

    /// <summary>Stores a fixed annotation: an indirect one is replaced, a direct one goes in the array.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="item">The array item.</param>
    /// <param name="annotation">The annotation as read.</param>
    /// <param name="fixedAnnotation">The annotation to keep.</param>
    /// <returns>The array item to keep.</returns>
    private static PdfValue Store(PdfObjectStore store, PdfValue item, PdfDictionary annotation, PdfDictionary fixedAnnotation)
    {
        if (ReferenceEquals(annotation, fixedAnnotation) || !item.IsReference)
        {
            return item.IsReference ? item : PdfValue.FromDictionary(fixedAnnotation);
        }

        StoreEditing.Replace(store, item.AsReference(), PdfValue.FromDictionary(fixedAnnotation));
        return item;
    }

    /// <summary>Removes popups whose parent annotation was removed.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="kept">The kept annotations, changed in place.</param>
    /// <param name="removed">The object numbers of the removed annotations.</param>
    private static void RemoveOrphanPopups(PdfObjectStore store, PdfArray kept, HashSet<int> removed)
    {
        for (var i = kept.Count - 1; removed.Count > 0 && i >= 0; i--)
        {
            if (StoreReading.Resolve(store, kept.GetRaw(i)).AsDictionary() is { } annotation
                && annotation.IsName(KnownName.Subtype, KnownName.Popup)
                && annotation.GetRaw(KnownName.Parent) is { IsReference: true } parent
                && removed.Contains(parent.AsReference().Number))
            {
                kept.RemoveAt(i);
            }
        }
    }

    /// <summary>Determines whether any direct annotation in the array was replaced by a copy.</summary>
    /// <param name="kept">The new array.</param>
    /// <param name="original">The original array.</param>
    /// <returns><see langword="true"/> when an item differs.</returns>
    private static bool HasChanged(PdfArray kept, PdfArray original)
    {
        for (var i = 0; i < kept.Count; i++)
        {
            var item = kept.GetRaw(i);
            if (!item.IsReference && !ReferenceEquals(item.AsDictionary(), original.GetRaw(i).AsDictionary()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes the page's new annotation array.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="pageId">The page object.</param>
    /// <param name="page">The page dictionary.</param>
    /// <param name="annotsRaw">The page's /Annots value.</param>
    /// <param name="kept">The new array.</param>
    private static void WriteAnnots(PdfObjectStore store, PdfObjectId pageId, PdfDictionary page, PdfValue annotsRaw, PdfArray kept)
    {
        if (annotsRaw.IsReference)
        {
            StoreEditing.Replace(store, annotsRaw.AsReference(), PdfValue.FromArray(kept));
            return;
        }

        var copy = page.Clone();
        copy.Set(KnownName.Annots, PdfValue.FromArray(kept));
        StoreEditing.Replace(store, pageId, PdfValue.FromDictionary(copy));
    }

    /// <summary>Records what the fixes did, or why they stop the conversion.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="tally">The counts.</param>
    private static void Report(PdfAConversionSession session, PdfAConversionAnnotationTally tally)
    {
        foreach (var subtype in tally.Forbidden)
        {
            session.RefuseIfBroken(
                string.Create(CultureInfo.InvariantCulture, $"The document has a {subtype} annotation, which {PdfALevels.Label(session.Level)} does not allow."),
                PdfARule.AnnotationTypePdfA2,
                PdfARule.AnnotationTypePdfA1,
                PdfARule.AnnotationTypePdfA4,
                PdfARule.AnnotationTypePdfA4E,
                PdfARule.AnnotationTypePdfA4F);
        }

        if (tally.HiddenFields > 0)
        {
            session.RefuseIfBroken("The form has hidden fields. PDF/A needs every field to be visible.", PdfARule.AnnotationFlags, PdfARule.AnnotationFlagsPdfA1);
        }

        if (tally.Transparent > 0)
        {
            session.RefuseIfBroken("Some annotations are see-through, and PDF/A-1 does not allow transparency. Choose PDF/A-2 or later.", PdfARule.AnnotationOpacity);
        }

        if (tally.Removed > 0)
        {
            session.Changed(
                string.Create(CultureInfo.InvariantCulture, $"Removed {tally.Removed} hidden annotations, which PDF/A does not allow; they were not shown."),
                PdfARule.AnnotationFlags,
                PdfARule.AnnotationFlagsPdfA1);
        }

        if (tally.Flags > 0)
        {
            session.Changed("Set annotations to print and to stay visible.", PdfARule.AnnotationFlags, PdfARule.AnnotationFlagsPdfA1, PdfARule.AnnotationFlagsPresent);
        }

        if (tally.Colours > 0)
        {
            session.Changed("Removed annotation colour entries, as the output intent is not RGB; the saved appearances still show the colours.", PdfARule.AnnotationColorNeedsRgbIntent);
        }

        PdfAConversionAppearances.Report(session, tally);
    }
}
