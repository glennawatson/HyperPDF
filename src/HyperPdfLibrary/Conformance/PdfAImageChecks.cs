// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks image XObjects and the images they carry: their soft masks, stencil masks and alternates, their colour spaces
/// and JPEG 2000 data. Each indirect image is checked once in each role, as veraPDF does.
/// </summary>
internal static class PdfAImageChecks
{
    /// <summary>The first PDF/A part, which does not allow 16-bit images.</summary>
    private const int Part1 = 1;

    /// <summary>The bits per component 16, allowed from PDF/A-2.</summary>
    private const long SixteenBits = 16;

    /// <summary>The bits per component 2.</summary>
    private const long TwoBits = 2;

    /// <summary>The bits per component 4.</summary>
    private const long FourBits = 4;

    /// <summary>The bits per component 8.</summary>
    private const long EightBits = 8;

    /// <summary>Visits an image painted by <c>Do</c>.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="image">The image resource.</param>
    /// <param name="frame">Where it is painted.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VisitImage(PdfAColourState state, in PdfAResource image, in PdfAContentFrame frame) =>
        Visit(state, new(image.Raw, image.Resources), PdfAVisitKind.Image, frame);

    /// <summary>Checks the entries every kind of image is checked for: interpolation, alternates, OPI and bit depth.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="image">The image dictionary, or an inline image's entries.</param>
    /// <param name="interpolate">The interpolation entry.</param>
    /// <param name="bitsPerComponent">The bits per component entry.</param>
    /// <param name="isMask">Whether the image is a stencil mask, whose bit depth is checked by its own rule.</param>
    internal static void CheckEntries(PdfAColourState state, PdfDictionary image, PdfValue interpolate, PdfValue bitsPerComponent, bool isMask)
    {
        var context = state.Context;
        context.Require(!interpolate.AsBoolean(false), PdfARule.ImageInterpolate, null);
        context.Require(!image.ContainsKey(KnownName.Alternates), PdfARule.ImageAlternates, null);
        context.Require(!image.ContainsKey(KnownName.OPI), PdfARule.ImageOpi, null);
        if (isMask || !bitsPerComponent.IsNumber)
        {
            return;
        }

        var bits = bitsPerComponent.AsInteger();
        var allowed = bits is 1 or TwoBits or FourBits or EightBits || (bits == SixteenBits && context.Part != Part1);
        context.Require(allowed, context.Part == Part1 ? PdfARule.ImageBitsPerComponentPdfA1 : PdfARule.ImageBitsPerComponent, null);
    }

    /// <summary>Visits an image in one of its roles.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="image">The image, possibly a reference, and the resources it was found in.</param>
    /// <param name="kind">The role.</param>
    /// <param name="frame">Where it is painted.</param>
    private static void Visit(PdfAColourState state, in PdfAResource image, PdfAVisitKind kind, in PdfAContentFrame frame)
    {
        if (StoreReading.Resolve(state.Store, image.Raw).AsStream() is not { } stream
            || (image.Id.IsValid && !state.Visited.Add(new(image.Id, kind, 0))))
        {
            return;
        }

        var dictionary = stream.Dictionary;
        CheckEntries(state, dictionary, dictionary.Get(KnownName.Interpolate), dictionary.Get(KnownName.BitsPerComponent), kind == PdfAVisitKind.MaskImage);
        if (kind == PdfAVisitKind.MaskImage)
        {
            var bits = dictionary.Get(KnownName.BitsPerComponent);
            state.Context.Require(!bits.IsNumber || bits.AsInteger() == 1, PdfARule.MaskBitsPerComponent, null);
        }

        VisitLinks(state, stream, image.Resources, kind, frame);
    }

    /// <summary>Visits what an image links to, by its role: colour space, alternates, intent, JPEG 2000 data and masks.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="stream">The image.</param>
    /// <param name="foundIn">The resources the image was found in, which names in an array colour space are read from.</param>
    /// <param name="kind">The role.</param>
    /// <param name="frame">Where it is painted.</param>
    private static void VisitLinks(PdfAColourState state, PdfStream stream, PdfDictionary? foundIn, PdfAVisitKind kind, in PdfAContentFrame frame)
    {
        var image = stream.Dictionary;
        var jpx = PdfAJpxChecks.IsJpx(state, image) ? PdfAJpxReader.Read(state, stream) : null;
        if (kind == PdfAVisitKind.Image)
        {
            VisitColourSpace(state, image, foundIn, jpx, frame);
        }

        if (kind != PdfAVisitKind.SoftMaskImage)
        {
            VisitAlternates(state, image, foundIn, frame);
        }

        if (kind != PdfAVisitKind.MaskImage)
        {
            PdfARenderingIntents.Check(state, image.GetRaw(KnownName.Intent));
        }

        if (jpx is not null)
        {
            PdfAJpxChecks.Check(state, jpx, !StoreReading.Resolve(state.Store, image.GetRaw(KnownName.ColorSpace)).IsNull);
        }

        if (kind != PdfAVisitKind.SoftMaskImage)
        {
            Visit(state, new(image.GetRaw(KnownName.SMask), null), PdfAVisitKind.SoftMaskImage, frame);
        }

        if (kind == PdfAVisitKind.Image)
        {
            Visit(state, new(image.GetRaw(KnownName.Mask), null), PdfAVisitKind.MaskImage, frame);
        }
    }

    /// <summary>Visits the alternate images of an image.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="image">The image dictionary.</param>
    /// <param name="foundIn">The resources the image was found in.</param>
    /// <param name="frame">Where it is painted.</param>
    private static void VisitAlternates(PdfAColourState state, PdfDictionary image, PdfDictionary? foundIn, in PdfAContentFrame frame)
    {
        var alternates = image.GetArray(KnownName.Alternates);
        for (var i = 0; alternates is not null && i < alternates.Count; i++)
        {
            if (alternates.GetDictionary(i) is { } alternate)
            {
                Visit(state, new(alternate.GetRaw(KnownName.Image), foundIn), PdfAVisitKind.Image, frame);
            }
        }
    }

    /// <summary>
    /// Visits an image's colour space. A stencil mask paints with the fill colour space; a named space is looked up like
    /// any other; a JPEG 2000 image without <c>/ColorSpace</c> uses the space its data declares.
    /// </summary>
    /// <param name="state">The walk.</param>
    /// <param name="image">The image dictionary.</param>
    /// <param name="foundIn">The resources the image was found in.</param>
    /// <param name="jpx">The JPEG 2000 header, or null.</param>
    /// <param name="frame">Where it is painted.</param>
    private static void VisitColourSpace(PdfAColourState state, PdfDictionary image, PdfDictionary? foundIn, PdfAJpxHeader? jpx, in PdfAContentFrame frame)
    {
        if (image.Get(KnownName.ImageMask).AsBoolean(false))
        {
            PdfAColourChecks.Visit(state, frame.Paint.Fill);
            return;
        }

        var raw = image.GetRaw(KnownName.ColorSpace);
        var value = StoreReading.Resolve(state.Store, raw);
        if (NamedSpace(state, value) is { IsNone: false } name)
        {
            PdfAColourChecks.Visit(state, PdfAResources.ColourSpace(state, frame.Scope, name));
            return;
        }

        if (value.AsArray() is not null)
        {
            PdfAColourChecks.Visit(state, PdfAColourSpaceParsing.Parse(state, raw, foundIn, false));
            return;
        }

        if (jpx?.Space is { } declared)
        {
            PdfAJpxSpaceChecks.Visit(state, declared, frame.Scope);
        }
    }

    /// <summary>Gets the name of a colour space given as a name or a one-element array holding a name.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="value">The colour space value.</param>
    /// <returns>The name, or none.</returns>
    private static PdfName NamedSpace(PdfAColourState state, PdfValue value)
    {
        if (value.TryGetName(out var name))
        {
            return name;
        }

        return value.AsArray() is { Count: 1 } array && StoreReading.Resolve(state.Store, array.GetRaw(0)).TryGetName(out var only) ? only : default;
    }
}
