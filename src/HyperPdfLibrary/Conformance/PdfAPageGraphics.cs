// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The page part of the PDF/A graphics walk: page boundaries (ISO 19005-2, 6.1.13), the page group, the page content,
/// and the annotations' blend modes, 3D streams (ISO 19005-4, 6.1.6.1) and appearance streams.
/// </summary>
internal static class PdfAPageGraphics
{
    /// <summary>The smallest page boundary extent, in units.</summary>
    private const double MinBoxExtent = 3;

    /// <summary>The largest page boundary extent, in units.</summary>
    private const double MaxBoxExtent = 14_400;

    /// <summary>The index of a box's left edge.</summary>
    private const int Left = 0;

    /// <summary>The index of a box's bottom edge.</summary>
    private const int Bottom = 1;

    /// <summary>The index of a box's right edge.</summary>
    private const int Right = 2;

    /// <summary>The index of a box's top edge.</summary>
    private const int Top = 3;

    /// <summary>How many page tree levels are followed for inherited attributes.</summary>
    private const int MaxTreeDepth = 64;

    /// <summary>Checks one page.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="page">The page.</param>
    internal static void Check(PdfAGraphicsWalk walk, PdfPage page)
    {
        var where = PdfAContexts.Page(page.Index);
        var dictionary = page.Dictionary;
        CheckBox(walk, Inherited(dictionary, KnownName.MediaBox).AsArray(), where);
        CheckBox(walk, Inherited(dictionary, KnownName.CropBox).AsArray(), where);
        CheckBox(walk, dictionary.GetArray(KnownName.BleedBox), where);
        CheckBox(walk, dictionary.GetArray(KnownName.TrimBox), where);
        CheckBox(walk, dictionary.GetArray(KnownName.ArtBox), where);
        PdfAGraphicsObjects.CheckGroup(walk, dictionary.Get(KnownName.Group), where);
        var resources = Inherited(dictionary, KnownName.Resources);
        var scope = new PdfAResourceScope(resources.Kind == PdfKind.Dictionary ? resources.AsDictionary() : null, !resources.IsNull, !dictionary.ContainsKey(KnownName.Resources), true, null);
        var raw = dictionary.GetRaw(KnownName.Contents);
        var contents = StoreReading.Resolve(walk.Store, raw);
        PdfAContentScan.Scan(walk, contents, ContentKey(raw, contents), new(scope, PdfAGraphicsSnapshot.Initial, where));
        CheckAnnotations(walk, dictionary, scope, where);
    }

    /// <summary>Gets what identifies page content so it is checked once: the stream, or an indirect array.</summary>
    /// <param name="raw">The <c>/Contents</c> value as written.</param>
    /// <param name="contents">The resolved value.</param>
    /// <returns>The key, or null for a direct array, which is checked with each page.</returns>
    private static object? ContentKey(PdfValue raw, PdfValue contents)
    {
        if (contents.AsStream() is { } stream)
        {
            return stream;
        }

        return raw.IsReference ? contents.AsArray() : null;
    }

    /// <summary>Checks the annotations of a page.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="page">The page dictionary.</param>
    /// <param name="scope">The page's resources.</param>
    /// <param name="where">The page's name in the report.</param>
    private static void CheckAnnotations(PdfAGraphicsWalk walk, PdfDictionary page, PdfAResourceScope scope, string where)
    {
        var annotations = page.GetArray(KnownName.Annots);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            var annotation = annotations.Get(i);
            if (annotation.Kind != PdfKind.Dictionary)
            {
                continue;
            }

            var dictionary = annotation.AsDictionary()!;
            if (walk.Context.Part == PdfAGraphicsObjects.Part4)
            {
                PdfAGraphicsObjects.CheckBlendMode(walk, dictionary.Get(KnownName.BM), where);
            }

            if (dictionary.IsName(KnownName.Subtype, KnownName.ThreeD))
            {
                CheckThreeD(walk, dictionary, where);
            }

            CheckAppearances(walk, dictionary.Get(KnownName.AP), scope, where);
        }
    }

    /// <summary>Reads the normal, down and rollover appearance streams of an annotation.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="appearances">The resolved <c>/AP</c> value.</param>
    /// <param name="scope">The page's resources.</param>
    /// <param name="where">The page's name in the report.</param>
    private static void CheckAppearances(PdfAGraphicsWalk walk, PdfValue appearances, PdfAResourceScope scope, string where)
    {
        if (appearances.Kind != PdfKind.Dictionary)
        {
            return;
        }

        var dictionary = appearances.AsDictionary()!;
        CheckAppearance(walk, dictionary.Get(KnownName.N), scope, where);
        CheckAppearance(walk, dictionary.Get(KnownName.D), scope, where);
        CheckAppearance(walk, dictionary.Get(KnownName.R), scope, where);
    }

    /// <summary>Reads one appearance entry: a stream, or a dictionary of streams by state.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="entry">The resolved entry.</param>
    /// <param name="scope">The page's resources.</param>
    /// <param name="where">The page's name in the report.</param>
    private static void CheckAppearance(PdfAGraphicsWalk walk, PdfValue entry, PdfAResourceScope scope, string where)
    {
        if (entry.AsStream() is not null)
        {
            PdfAContentNested.UseForm(walk, entry, scope, PdfAGraphicsSnapshot.Initial, where);
            return;
        }

        if (entry.Kind != PdfKind.Dictionary)
        {
            return;
        }

        var states = entry.AsDictionary()!;
        for (var i = 0; i < states.Count; i++)
        {
            var appearance = StoreReading.Resolve(walk.Store, states.GetValueAt(i));
            if (appearance.AsStream() is not null)
            {
                PdfAContentNested.UseForm(walk, appearance, scope, PdfAGraphicsSnapshot.Initial, where);
            }
        }
    }

    /// <summary>Checks the stream of a 3D annotation, reached directly or through a 3D reference dictionary.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="where">The page's name in the report.</param>
    private static void CheckThreeD(PdfAGraphicsWalk walk, PdfDictionary annotation, string where)
    {
        var names = walk.Names;
        var data = annotation.Get(names.ThreeDData);
        if (data.Kind == PdfKind.Dictionary && data.AsDictionary()!.GetName(KnownName.Type) == names.ThreeDReference)
        {
            data = data.AsDictionary()!.Get(names.ThreeDData);
        }

        if (data.AsStream() is not { } stream || !stream.Dictionary.IsName(KnownName.Type, KnownName.ThreeD))
        {
            return;
        }

        var subtype = stream.Dictionary.GetName(KnownName.Subtype);
        walk.Context.Require(subtype == names.U3d || subtype == names.Prc, PdfARule.ThreeDStreamSubtype, where);
    }

    /// <summary>Checks that a page boundary is between 3 and 14 400 units in each direction; a missing number counts as 0.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="box">The box array, or null when absent.</param>
    /// <param name="where">The page's name in the report.</param>
    private static void CheckBox(PdfAGraphicsWalk walk, PdfArray? box, string where)
    {
        if (box is null)
        {
            return;
        }

        var height = Math.Abs(Edge(box, Top) - Edge(box, Bottom));
        var width = Math.Abs(Edge(box, Right) - Edge(box, Left));
        walk.Context.Require(height is >= MinBoxExtent and <= MaxBoxExtent && width is >= MinBoxExtent and <= MaxBoxExtent, PdfARule.BoundingBoxSize, where);
    }

    /// <summary>Reads one edge of a box.</summary>
    /// <param name="box">The box.</param>
    /// <param name="index">The edge index.</param>
    /// <returns>The coordinate, or 0 when missing or not a number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Edge(PdfArray box, int index) => box.Get(index).AsNumber(0);

    /// <summary>Reads an inheritable page attribute from the page or the nearest page tree node that has it.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="key">The attribute.</param>
    /// <returns>The resolved value, or null when no node has it.</returns>
    private static PdfValue Inherited(PdfDictionary page, KnownName key)
    {
        var node = page;
        for (var depth = 0; node is not null && depth < MaxTreeDepth; depth++)
        {
            if (!node.GetRaw(key).IsNull)
            {
                return node.Get(key);
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return default;
    }
}
