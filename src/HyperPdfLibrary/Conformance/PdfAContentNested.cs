// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Follows content operators into the objects they draw: form and image XObjects, tiling patterns and Type 3 glyphs.
/// Nested content resolves names through its own resources first, then the page's.
/// </summary>
internal static class PdfAContentNested
{
    /// <summary>The paint type of an uncoloured tiling pattern, whose content sets no colour.</summary>
    private const int UncolouredPattern = 2;

    /// <summary>Follows a <c>Do</c> into the XObject it paints.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream that paints it.</param>
    /// <param name="xobject">The resolved XObject.</param>
    internal static void UseXObject(PdfAGraphicsWalk walk, PdfAContentState state, PdfValue xobject)
    {
        if (xobject.AsDictionary() is not { } dictionary)
        {
            return;
        }

        switch (dictionary.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Form:
            {
                UseForm(walk, xobject, state.Scope, state.Graphics, state.Where);
                break;
            }

            case KnownName.Image:
            {
                CheckImage(walk, dictionary, state.Where);
                break;
            }

            default:
            {
                if (walk.FirstVisit(dictionary))
                {
                    PdfAGraphicsObjects.CheckXObject(walk, dictionary, state.Where);
                }

                break;
            }
        }
    }

    /// <summary>Checks a form XObject or appearance stream, then reads its content.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="form">The resolved form.</param>
    /// <param name="scope">The resources of the content that uses it.</param>
    /// <param name="graphics">The graphics state it inherits.</param>
    /// <param name="where">Where it is used.</param>
    internal static void UseForm(PdfAGraphicsWalk walk, PdfValue form, PdfAResourceScope scope, PdfAGraphicsSnapshot graphics, string where)
    {
        var dictionary = form.AsDictionary()!;
        PdfAGraphicsObjects.CheckForm(walk, dictionary, where);
        if (form.AsStream() is not { } stream)
        {
            return;
        }

        var inner = new PdfAContentState(scope.Extend(dictionary.Get(KnownName.Resources)), graphics, NameOf(stream, where));
        PdfAContentScan.Scan(walk, form, stream, inner);
    }

    /// <summary>Follows an <c>scn</c> or <c>SCN</c> into the pattern it names, reading a tiling pattern's content.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream that uses it.</param>
    /// <param name="name">The pattern name, or none.</param>
    /// <returns><see langword="true"/> when the name gives a pattern, so the colour space stays a pattern space.</returns>
    internal static bool UsePattern(PdfAGraphicsWalk walk, PdfAContentState state, PdfName name)
    {
        if (name.IsNone)
        {
            return false;
        }

        var pattern = PdfAResourceLookup.Find(walk, state, KnownName.Pattern, name);
        if (pattern.AsStream() is { } stream && stream.Dictionary.Get(KnownName.PatternType).AsInteger(0) == PdfAColorSpaces.TilingPattern)
        {
            var dictionary = stream.Dictionary;
            var graphics = state.Graphics with
            {
                FillPattern = false,
                StrokePattern = false,
                ProcessColor = dictionary.Get(KnownName.PaintType).AsInteger(0) != UncolouredPattern,
            };

            var inner = new PdfAContentState(state.Scope.Extend(dictionary.Get(KnownName.Resources)), graphics, NameOf(stream, state.Where));
            PdfAContentScan.Scan(walk, pattern, stream, inner);
        }

        return !pattern.IsNull;
    }

    /// <summary>Follows text shown with a Type 3 font into the glyph procedures.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream that shows the text.</param>
    internal static void ShowText(PdfAGraphicsWalk walk, PdfAContentState state)
    {
        var font = state.Graphics.Font;
        if (font is null || !font.IsName(KnownName.Subtype, KnownName.Type3) || !walk.FirstVisit(font))
        {
            return;
        }

        var fontScope = state.Scope.Extend(font.Get(KnownName.Resources));
        var procedures = font.GetDictionary(KnownName.CharProcs);
        for (var i = 0; procedures is not null && i < procedures.Count; i++)
        {
            var procedure = StoreReading.Resolve(walk.Store, procedures.GetValueAt(i));
            if (procedure.AsStream() is { } stream)
            {
                var scope = stream.Dictionary.ContainsKey(KnownName.Resources) ? state.Scope.Extend(stream.Dictionary.Get(KnownName.Resources)) : fontScope;
                PdfAContentScan.Scan(walk, procedure, stream, new(scope, state.Graphics, NameOf(stream, state.Where)));
            }
            else if (procedure.AsArray() is { } array)
            {
                PdfAContentScan.Scan(walk, procedure, array, new(fontScope, state.Graphics, state.Where));
            }
        }
    }

    /// <summary>Checks an image XObject and the images it carries: its soft mask, stencil mask and alternates.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="image">The image dictionary.</param>
    /// <param name="where">Where it is used.</param>
    private static void CheckImage(PdfAGraphicsWalk walk, PdfDictionary image, string where)
    {
        if (!walk.FirstVisit(image))
        {
            return;
        }

        PdfAGraphicsObjects.CheckXObject(walk, image, where);
        if (image.GetStream(KnownName.SMask) is { } softMask)
        {
            CheckImage(walk, softMask.Dictionary, where);
        }

        if (image.GetStream(KnownName.Mask) is { } mask)
        {
            CheckImage(walk, mask.Dictionary, where);
        }

        var alternates = image.GetArray(KnownName.Alternates);
        for (var i = 0; alternates is not null && i < alternates.Count; i++)
        {
            if (alternates.GetDictionary(i)?.GetStream(KnownName.Image) is { } alternate)
            {
                CheckImage(walk, alternate.Dictionary, where);
            }
        }
    }

    /// <summary>Names a stream for the report: its object number when it has one.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="fallback">The name to use for a stream without an object number.</param>
    /// <returns>The name.</returns>
    private static string NameOf(PdfStream stream, string fallback) => stream.Id.Number > 0 ? PdfAContexts.Object(stream.Id.Number) : fallback;
}
