// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Codes the 1-bit images of a document that share their marks with one JBIG2 symbol dictionary in a /JBIG2Globals stream.
/// The stream is added to the working copy before the file is laid out, so the writer carries it like any other object.
/// Images used as masks, and images the shared coding would not make smaller, are left to the per-image coding.
/// </summary>
internal static class SharedJbig2Pass
{
    /// <summary>The fewest images worth sharing a dictionary.</summary>
    private const int MinimumImages = 2;

    /// <summary>The fixed settings for lossless shared symbol selection.</summary>
    private static readonly Jbig2EncodeOptions SharedOptions = new() { Mode = Jbig2EncodeMode.Smallest };

    /// <summary>The fixed settings for explicitly requested lossy sharing.</summary>
    private static readonly Jbig2EncodeOptions LossyOptions = new() { Mode = Jbig2EncodeMode.LossySymbolMerging };

    /// <summary>Codes the document's 1-bit images with a shared dictionary when that is smaller.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="scanner">What the analysis learned, including which images pages draw.</param>
    /// <param name="mode">Whether lossy symbol merging may be tried.</param>
    /// <param name="report">The report.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    internal static void Run(PdfObjectStore store, ContentUsageScanner scanner, PdfJbig2Mode mode, OptimizeReportBuilder report, CancellationToken cancellationToken)
    {
        var candidates = Gather(store, scanner, cancellationToken);
        if (candidates.Count < MinimumImages)
        {
            return;
        }

        var (inputs, separate) = CreateInputs(candidates, store.ScratchPools, cancellationToken);
        var best = Jbig2Encoder.EncodeShared(inputs, SharedOptions, cancellationToken);
        if (mode == PdfJbig2Mode.Lossy)
        {
            var lossy = Jbig2Encoder.EncodeShared(inputs, LossyOptions, cancellationToken);
            best = Total(lossy) < Total(best) ? lossy : best;
        }

        if (best.Globals.Length == 0 || Total(best) >= separate)
        {
            return;
        }

        Apply(store, candidates, best, report);
    }

    /// <summary>Builds shared inputs and measures separate codings without keeping unused output copies.</summary>
    /// <param name="candidates">The source images and their decoded rows.</param>
    /// <param name="pools">The document's original encoding owner.</param>
    /// <param name="cancellationToken">Cancels between images and encoding segments.</param>
    /// <returns>The shared inputs and their separate byte cost.</returns>
    private static SharedInputs CreateInputs(List<SharedCandidate> candidates, ScratchPools pools, CancellationToken cancellationToken)
    {
        var inputs = new Jbig2PageInput[candidates.Count];
        var data = default(PooledBuffer);
        var globals = default(PooledBuffer);
        long separate = 0;
        try
        {
            for (var i = 0; i < inputs.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (_, stream, rows, width, height) = candidates[i];
                inputs[i] = new(rows, width, height);
                _ = Jbig2Encoder.EncodeInto(rows, new(new(width, height), Jbig2EncodeOptions.Default), ref data, ref globals, pools, cancellationToken);
                separate += Math.Min(stream.RawLength, data.Length);
            }

            return new(inputs, separate);
        }
        finally
        {
            data.Dispose();
            globals.Dispose();
        }
    }

    /// <summary>Gets the bytes of a shared coding.</summary>
    /// <param name="encoding">The coding.</param>
    /// <returns>The globals and all images.</returns>
    private static long Total(Jbig2SharedEncoding encoding)
    {
        long total = encoding.Globals.Length;
        foreach (var page in encoding.Pages)
        {
            total += page.Length;
        }

        return total;
    }

    /// <summary>Finds the drawn 1-bit images that are not masks and reads their rows.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="scanner">What the analysis learned.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The candidate images.</returns>
    private static List<SharedCandidate> Gather(PdfObjectStore store, ContentUsageScanner scanner, CancellationToken cancellationToken)
    {
        var numbers = new List<int>(scanner.Images.Keys);
        numbers.Sort();
        var masks = CollectMasks(store, numbers);
        var candidates = new List<SharedCandidate>();
        foreach (var number in numbers)
        {
            if (!masks.Contains(number) && Image(store, number) is { } stream && TryRead(stream, number, cancellationToken) is { } candidate)
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    /// <summary>Finds the images that other images use as soft or stencil masks.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="numbers">The drawn images' object numbers.</param>
    /// <returns>The mask images' object numbers.</returns>
    private static HashSet<int> CollectMasks(PdfObjectStore store, List<int> numbers)
    {
        var masks = new HashSet<int>();
        foreach (var number in numbers)
        {
            var dictionary = Image(store, number)?.Dictionary;
            AddMask(masks, dictionary?.GetRaw(KnownName.SMask) ?? default);
            AddMask(masks, dictionary?.GetRaw(KnownName.Mask) ?? default);
        }

        return masks;
    }

    /// <summary>Gets an image stream by object number.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The image, or <see langword="null"/> when the object is not an image stream.</returns>
    private static PdfStream? Image(PdfObjectStore store, int number) =>
        StoreReading.GetObject(store, new(number, 0)).AsStream() is { } stream && stream.Dictionary.GetName(KnownName.Subtype).Is(KnownName.Image) ? stream : null;

    /// <summary>Records an image used as a mask.</summary>
    /// <param name="masks">The mask object numbers.</param>
    /// <param name="raw">The /SMask or /Mask value.</param>
    private static void AddMask(HashSet<int> masks, PdfValue raw)
    {
        if (raw.IsReference)
        {
            _ = masks.Add(raw.AsReference().Number);
        }
    }

    /// <summary>Reads the rows of a 1-bit image that JBIG2 can replace.</summary>
    /// <param name="stream">The image.</param>
    /// <param name="number">Its object number.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The candidate, or <see langword="null"/> when the image is not a plain 1-bit image or is already JBIG2, JPEG or JPEG 2000.</returns>
    private static SharedCandidate? TryRead(PdfStream stream, int number, CancellationToken cancellationToken)
    {
        var dictionary = stream.Dictionary;
        var codec = ImageFilters.Codec(dictionary);
        var width = dictionary.GetInt32(KnownName.Width, 0);
        var height = dictionary.GetInt32(KnownName.Height, 0);
        if (width <= 0 || height <= 0 || codec is CodecKind.Jbig2 or CodecKind.Jpeg2000 or CodecKind.Jpeg or CodecKind.Crypt || !BilevelRecoder.IsBilevel(dictionary))
        {
            return null;
        }

        var decoded = default(PooledBuffer);
        var rows = default(PooledBuffer);
        try
        {
            var size = new PixelSize(width, height, 1);
            var read = BilevelRecoder.TryReadRows(stream, ImageFilters.CodecParameters(dictionary), size, ref decoded, ref rows, cancellationToken);
            return read ? new SharedCandidate(number, stream, rows.ToArray(), width, height) : null;
        }
        finally
        {
            decoded.Dispose();
            rows.Dispose();
        }
    }

    /// <summary>Adds the globals stream and replaces each image with its JBIG2 stream.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="candidates">The images.</param>
    /// <param name="encoding">The shared coding.</param>
    /// <param name="report">The report.</param>
    private static void Apply(PdfObjectStore store, List<SharedCandidate> candidates, Jbig2SharedEncoding encoding, OptimizeReportBuilder report)
    {
        var globals = StoreEditing.Add(store, PdfValue.FromStream(new(new PdfDictionary(store), encoding.Globals)));
        var coding = encoding.IsLossless ? "losslessly" : "with lossy symbol merging";
        for (var i = 0; i < candidates.Count; i++)
        {
            var (number, stream, _, width, height) = candidates[i];
            var copy = BilevelEncoder.Prepare(stream.Dictionary);
            copy.Set(KnownName.Filter, PdfValue.FromName(KnownName.JBIG2Decode));
            var parameters = new PdfDictionary(store);
            parameters.Set(KnownName.JBIG2Globals, PdfValue.FromReference(globals));
            copy.Set(KnownName.DecodeParms, PdfValue.FromDictionary(parameters));
            StoreEditing.Replace(store, new(number, 0), PdfValue.FromStream(new(copy, encoding.Pages[i])));

            // The shared dictionary is counted once, against the first image.
            var after = encoding.Pages[i].Length + (i == 0 ? encoding.Globals.Length : 0);
            var note = string.Create(CultureInfo.InvariantCulture, $"1-bit image {width}x{height} saved {coding} as JBIG2 with a symbol dictionary shared by {candidates.Count} images.");
            report.Changed(PdfOptimizeCategory.Images, number, note, stream.RawLength, after);
        }
    }

    /// <summary>The page inputs and cost of coding them independently.</summary>
    /// <param name="Pages">The shared page inputs.</param>
    /// <param name="SeparateBytes">The total separate byte cost.</param>
    private readonly record struct SharedInputs(Jbig2PageInput[] Pages, long SeparateBytes);
}
