// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Draws a form XObject on a page, in front of the page's content or behind it. The page keeps its own content streams,
/// wrapped so the form starts from a clean graphics state, and gets a copy of its resources with the form added, so
/// resources that other pages share are never changed (ISO 32000-2 7.8.3, 8.10).
/// </summary>
internal static class PdfPageStamping
{
    /// <summary>Splits the spare room around placed content into two equal margins.</summary>
    private const float CentreDivisor = 2;

    /// <summary>The prefix of the resource names given to stamped forms.</summary>
    private const string FormPrefix = "HpForm";

    /// <summary>The prefix of the resource names given to the graphics states of stamped forms.</summary>
    private const string StatePrefix = "HpState";

    /// <summary>Gets the matrix that places the upright content of a form on a page, at the page's lower left, scaled as chosen.</summary>
    /// <param name="source">The page the form was made from.</param>
    /// <param name="target">The page the form is drawn on.</param>
    /// <param name="fit">How the form is placed on the target.</param>
    /// <returns>The matrix from the form's space to the target's user space.</returns>
    internal static Matrix3x2 GetPlacement(PdfPage source, PdfPage target, PdfOverlayFit fit)
    {
        PdfPageImporter.GetDisplayedSize(source.CropBox, source.Rotation, out var sourceWidth, out var sourceHeight);
        PdfPageImporter.GetDisplayedSize(target.CropBox, target.Rotation, out var targetWidth, out var targetHeight);

        // Unless it is fitted, a form keeps its physical size: one unit of it is source.UserUnit points and one unit of the target is target.UserUnit points.
        var scale = fit == PdfOverlayFit.Fit ? Math.Min(targetWidth / sourceWidth, targetHeight / sourceHeight) : source.UserUnit / target.UserUnit;
        var placement = Matrix3x2.CreateScale(scale);
        if (fit != PdfOverlayFit.None)
        {
            placement *= Matrix3x2.CreateTranslation((targetWidth - (sourceWidth * scale)) / CentreDivisor, (targetHeight - (sourceHeight * scale)) / CentreDivisor);
        }

        return ToUserSpace(placement, target);
    }

    /// <summary>Turns a matrix that places content on the upright page into one in the page's user space.</summary>
    /// <param name="upright">The matrix, in the page's upright space with its origin at the lower left of the crop box.</param>
    /// <param name="target">The page.</param>
    /// <returns>The matrix in user space.</returns>
    internal static Matrix3x2 ToUserSpace(Matrix3x2 upright, PdfPage target)
    {
        _ = Matrix3x2.Invert(PdfPageImporter.GetPageMatrix(target.CropBox, target.Rotation), out var inverse);
        return upright * inverse;
    }

    /// <summary>Draws a form on a page dictionary that is being edited.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="page">The page as it was before any edit, for its inherited resources.</param>
    /// <param name="copy">The copy of the page dictionary being changed.</param>
    /// <param name="request">What to draw and where.</param>
    internal static void Stamp(PdfDocument document, PdfEditTransaction transaction, PdfPage page, PdfDictionary copy, PdfStampRequest request)
    {
        var store = document.Objects;
        var resources = (copy.GetDictionary(KnownName.Resources) ?? page.Resources)?.Clone() ?? new PdfDictionary(store);
        var forms = resources.GetDictionary(KnownName.XObject)?.Clone() ?? new PdfDictionary(store);
        var formName = UniqueName(forms, FormPrefix, store.Names);
        forms.Set(formName, PdfValue.FromReference(request.Form));
        resources.Set(KnownName.XObject, PdfValue.FromDictionary(forms));
        var stateName = default(PdfName);
        if (request.GraphicsState.IsValid)
        {
            var states = resources.GetDictionary(KnownName.ExtGState)?.Clone() ?? new PdfDictionary(store);
            stateName = UniqueName(states, StatePrefix, store.Names);
            states.Set(stateName, PdfValue.FromReference(request.GraphicsState));
            resources.Set(KnownName.ExtGState, PdfValue.FromDictionary(states));
        }

        copy.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        var draw = WriteDraw(store.Names, request, formName, stateName);
        if (request.Behind)
        {
            Wrap(transaction, store, copy, draw, default);
            return;
        }

        // The old content ends in the state it left; the Q closes the q before the drawing starts clean.
        var close = "Q\n"u8;
        var tail = new byte[draw.Length + close.Length];
        close.CopyTo(tail);
        draw.CopyTo(tail, close.Length);
        Wrap(transaction, store, copy, "q\n"u8, tail);
    }

    /// <summary>Sets a page's /Contents to its old streams with a stream of operators before them and another after them.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="copy">The page dictionary being changed.</param>
    /// <param name="before">The operators that go first, or empty for none.</param>
    /// <param name="after">The operators that go last, or empty for none.</param>
    internal static void Wrap(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary copy, ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        var parts = new PdfArray(store);
        if (!before.IsEmpty)
        {
            parts.Add(AddStream(transaction, before));
        }

        var existing = copy.Get(KnownName.Contents);
        if (existing.AsArray() is { } streams)
        {
            for (var i = 0; i < streams.Count; i++)
            {
                parts.Add(streams.GetRaw(i));
            }
        }
        else if (existing.AsStream() is not null)
        {
            parts.Add(copy.GetRaw(KnownName.Contents));
        }

        if (!after.IsEmpty)
        {
            parts.Add(AddStream(transaction, after));
        }

        copy.Set(KnownName.Contents, PdfValue.FromArray(parts));
    }

    /// <summary>Stores the edited page dictionaries, in page order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="copies">The edited dictionaries by page index.</param>
    internal static void StoreAll(PdfDocument document, PdfEditTransaction transaction, Dictionary<int, PdfDictionary> copies)
    {
        List<PdfPageSlot>? direct = null;
        foreach (var (index, copy) in copies)
        {
            PdfDocumentPageOperations.StoreEdited(document, transaction, PdfDocumentPages.GetPage(document, index), copy, ref direct);
        }

        PdfDocumentPageOperations.FinishEdited(document, transaction, direct);
    }

    /// <summary>Makes a resource name that is not yet used in a dictionary.</summary>
    /// <param name="dictionary">The resource category.</param>
    /// <param name="prefix">The start of the name.</param>
    /// <param name="names">The document's name table.</param>
    /// <returns>The name.</returns>
    internal static PdfName UniqueName(PdfDictionary dictionary, string prefix, PdfNameTable names)
    {
        var number = 0;
        PdfName name;
        do
        {
            name = names.Intern(string.Create(CultureInfo.InvariantCulture, $"{prefix}{number}"));
            number++;
        }
        while (dictionary.ContainsKey(name));
        return name;
    }

    /// <summary>Writes the operators that draw the form.</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="request">What to draw.</param>
    /// <param name="formName">The form's resource name.</param>
    /// <param name="stateName">The graphics state's resource name, or none.</param>
    /// <returns>The content bytes.</returns>
    private static byte[] WriteDraw(PdfNameTable names, PdfStampRequest request, PdfName formName, PdfName stateName)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            switch (request.Marking)
            {
                case PdfStampMarking.Artifact:
                {
                    builder.WriteRaw("/Artifact BMC\n"u8);
                    break;
                }

                case PdfStampMarking.Watermark:
                {
                    builder.WriteRaw("/Artifact <</Type /Pagination /Subtype /Watermark>> BDC\n"u8);
                    break;
                }

                default:
                {
                    break;
                }
            }

            if (!stateName.IsNone)
            {
                builder.SetGraphicsState(names.GetSpelling(stateName));
            }

            var m = request.Transform;
            builder.Transform(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32);
            builder.DrawXObject(names.GetSpelling(formName));
            if (request.Marking != PdfStampMarking.None)
            {
                builder.EndMarkedContent();
            }

            builder.RestoreState();
            return builder.ToArray();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Adds a content stream to the document.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="data">The content.</param>
    /// <returns>A reference to the stream.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfValue AddStream(PdfEditTransaction transaction, ReadOnlySpan<byte> data) =>
        PdfValue.FromReference(transaction.Add(PdfValue.FromStream(new(new(null), data.ToArray()))));
}
