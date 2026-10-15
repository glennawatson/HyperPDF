// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Finds objects whose content is identical: streams by their dictionary and decrypted raw bytes, and fonts, font
/// descriptors, encodings, graphics states, colour spaces and number arrays by their serialised form. References are
/// compared after earlier merges, so passes repeat until nothing more merges: identical font programs first, then the
/// descriptors pointing at them, then the fonts. Objects whose identity matters, such as pages, annotations, fields,
/// structure elements and marked XObjects, are never merged.
/// </summary>
[DebuggerDisplay("DuplicateFinder: {_candidates.Count} candidates")]
internal sealed class DuplicateFinder
{
    /// <summary>The most passes; each merges one more level of objects that refer to merged ones.</summary>
    private const int MaxPasses = 6;

    /// <summary>The bytes an object's header and trailer take, counted for a dropped object.</summary>
    private const int ObjectOverhead = 20;

    /// <summary>The largest number table an idle finder keeps; a bigger one is dropped when the finder is given back.</summary>
    private const int MaxKeptTable = 1 << 20;

    /// <summary>Idle finders, kept with the lists and tables they grew.</summary>
    private readonly ReusableSlots<DuplicateFinder> _idle;

    /// <summary>The objects that may be merged, by old number, in the order they were reached.</summary>
    private readonly List<int> _candidates = [];

    /// <summary>The digest of each candidate stream's data, by old number.</summary>
    private readonly Dictionary<int, ContentHash> _dataHashes = [];

    /// <summary>The first object with each digest in the current pass.</summary>
    private readonly Dictionary<ContentHash, int> _seen = [];

    /// <summary>The store the objects come from.</summary>
    private PdfObjectStore? _store;

    /// <summary>The number each object's references are written as while hashing: its original's.</summary>
    private int[] _canonical = [];

    /// <summary>The original of each duplicate, by old number; zero when the object is no duplicate.</summary>
    private int[] _aliases = [];

    /// <summary>1 once the finder has been given back, so a second dispose does not offer it twice.</summary>
    private int _returned;

    /// <summary>Initializes a new instance of the <see cref="DuplicateFinder"/> class.</summary>
    /// <param name="idle">The owning service's duplicate-finder pool.</param>
    private DuplicateFinder(ReusableSlots<DuplicateFinder> idle) => _idle = idle;

    /// <summary>Gets the original of each duplicate by old number, zero for objects that are not duplicates. It may be longer than the store.</summary>
    internal int[] Aliases => _aliases;

    /// <summary>Finds the duplicates among the reachable objects.</summary>
    /// <param name="store">The store.</param>
    /// <param name="graph">The reachable objects.</param>
    /// <param name="report">Receives the savings.</param>
    /// <param name="cancellationToken">Stops the search.</param>
    /// <returns>The finder holding the duplicates, which the caller disposes when it is done with <see cref="Aliases"/>.</returns>
    internal static DuplicateFinder Find(PdfObjectStore store, OptimizerGraph graph, OptimizeReportBuilder report, CancellationToken cancellationToken)
    {
        var idle = graph.Resources.Duplicates;
        var finder = idle.Take() ?? new(idle);
        Volatile.Write(ref finder._returned, 0);
        try
        {
            finder.Begin(store);
            for (var number = 1; number <= graph.Count; number++)
            {
                if (IsCandidate(graph.GetValue(number)))
                {
                    finder._candidates.Add(graph.GetOldNumber(number));
                }
            }

            for (var pass = 0; pass < MaxPasses && finder.MergePass(cancellationToken); pass++)
            {
                // Each pass lets objects that refer to merged objects merge in turn.
            }

            finder.Report(report);
            return finder;
        }
        catch
        {
            finder.Return();
            throw;
        }
    }

    /// <summary>Gives the finder back to the pool, emptied.</summary>
    internal void Return()
    {
        if (Interlocked.Exchange(ref _returned, 1) != 0)
        {
            return;
        }

        _candidates.Clear();
        _dataHashes.Clear();
        _seen.Clear();
        _store = null;
        if (_aliases.Length > MaxKeptTable)
        {
            _aliases = [];
            _canonical = [];
        }

        _ = _idle.Offer(this);
    }

    /// <summary>Determines whether an object may be merged with an identical one.</summary>
    /// <param name="value">The object.</param>
    /// <returns><see langword="true"/> for streams without structure keys, fonts, descriptors, graphics states, colour spaces and number arrays.</returns>
    private static bool IsCandidate(PdfValue value) => value.Kind switch
    {
        PdfKind.Stream => IsMergeableStream(value.AsStream()!.Dictionary),
        PdfKind.Dictionary => IsMergeableDictionary(value.AsDictionary()!),
        PdfKind.Array => IsMergeableArray(value.AsArray()!),
        _ => false,
    };

    /// <summary>Determines whether a stream may be merged: not a structure or cross-reference stream.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    private static bool IsMergeableStream(PdfDictionary dictionary) =>
        !dictionary.ContainsKey(KnownName.StructParent)
        && !dictionary.ContainsKey(KnownName.StructParents)
        && !dictionary.IsName(
        KnownName.Type,
        KnownName.XRef) && !dictionary.IsName(
        KnownName.Type,
        KnownName.ObjStm);

    /// <summary>Determines whether a dictionary may be merged: a font, descriptor, encoding or graphics state.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    private static bool IsMergeableDictionary(PdfDictionary dictionary) =>
        dictionary.GetName(KnownName.Type).ToKnownName() is KnownName.Font or
        KnownName.FontDescriptor or
        KnownName.Encoding or
        KnownName.ExtGState;

    /// <summary>Determines whether an array may be merged: a colour space or a list of numbers such as /Widths.</summary>
    /// <param name="array">The array.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    private static bool IsMergeableArray(PdfArray array)
    {
        if (array.Count == 0)
        {
            return false;
        }

        return array.GetRaw(0).Kind == PdfKind.Name ? IsColorSpaceFamily(array.GetRaw(0).AsName()) : IsNumbers(array);
    }

    /// <summary>Determines whether a name starts a colour space array.</summary>
    /// <param name="family">The array's first name.</param>
    /// <returns><see langword="true"/> for the colour space families held in arrays.</returns>
    private static bool IsColorSpaceFamily(PdfName family) =>
        family.ToKnownName() is KnownName.ICCBased or
        KnownName.Indexed or
        KnownName.Separation or
        KnownName.DeviceN or
        KnownName.CalRGB or
        KnownName.CalGray or
        KnownName.Lab;

    /// <summary>Determines whether an array holds only numbers.</summary>
    /// <param name="array">The array.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsNumbers(PdfArray array)
    {
        foreach (var item in array.Items)
        {
            if (!item.IsNumber)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Points the finder at a store and resets its tables.</summary>
    /// <param name="store">The store.</param>
    private void Begin(PdfObjectStore store)
    {
        _store = store;
        if (_aliases.Length < store.Size)
        {
            _aliases = new int[store.Size];
            _canonical = new int[store.Size];
        }
        else
        {
            Array.Clear(_aliases);
        }

        for (var i = 1; i < _canonical.Length; i++)
        {
            _canonical[i] = i;
        }
    }

    /// <summary>Runs one pass: hashes every candidate not yet merged and merges equal ones into the first.</summary>
    /// <param name="cancellationToken">Stops the search.</param>
    /// <returns><see langword="true"/> when anything merged.</returns>
    private bool MergePass(CancellationToken cancellationToken)
    {
        _seen.Clear();
        var merged = false;
        foreach (var number in _candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_aliases[number] > 0)
            {
                continue;
            }

            var digest = Hash(number);
            if (_seen.TryGetValue(digest, out var original))
            {
                _aliases[number] = original;
                _canonical[number] = original;
                merged = true;
                continue;
            }

            _seen[digest] = number;
        }

        Flatten();
        return merged;
    }

    /// <summary>Points every duplicate straight at the object that survives.</summary>
    private void Flatten()
    {
        for (var i = 1; i < _aliases.Length; i++)
        {
            var target = _aliases[i];
            if (target <= 0)
            {
                continue;
            }

            for (var steps = 0; _aliases[target] > 0 && steps < MaxPasses; steps++)
            {
                target = _aliases[target];
            }

            _aliases[i] = target;
            _canonical[i] = target;
        }
    }

    /// <summary>Hashes an object with its references written as their originals' numbers.</summary>
    /// <param name="number">The old object number.</param>
    /// <returns>The digest.</returns>
    private ContentHash Hash(int number)
    {
        var value = StoreReading.GetObject(_store!, new(number, 0));
        var writer = new PdfObjectWriter(_store!.Names);
        try
        {
            writer.SetRenumbering(_canonical);
            if (value.AsStream() is not { } stream)
            {
                writer.WriteValue(value);
                return ContentHash.Of(writer.WrittenSpan);
            }

            writer.WriteDictionaryWithout(stream.Dictionary, KnownName.Length);

            // The stream's digest covers its dictionary's digest followed by its data's digest.
            Span<byte> both = stackalloc byte[ContentHash.Length + ContentHash.Length];
            ContentHash.Of(writer.WrittenSpan).CopyTo(both);
            DataHash(number, stream).CopyTo(both[ContentHash.Length..]);
            return ContentHash.Of(both);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Gets the digest of a stream's decrypted raw bytes, computing it once.</summary>
    /// <param name="number">The old object number.</param>
    /// <param name="stream">The stream.</param>
    /// <returns>The digest.</returns>
    private ContentHash DataHash(int number, PdfStream stream)
    {
        if (_dataHashes.TryGetValue(number, out var known))
        {
            return known;
        }

        using var raw = stream.LeaseRawData();
        var digest = ContentHash.Of(PdfObjectWriter.PlainData(stream, raw.Span));
        _dataHashes[number] = digest;
        return digest;
    }

    /// <summary>Records the merged objects' sizes as saved.</summary>
    /// <param name="report">The report.</param>
    private void Report(OptimizeReportBuilder report)
    {
        foreach (var number in _candidates)
        {
            if (_aliases[number] <= 0)
            {
                continue;
            }

            var value = StoreReading.GetObject(_store!, new(number, 0));
            var size = value.AsStream() is { } stream ? stream.RawLength + ObjectOverhead : ObjectOverhead;
            report.Measure(PdfOptimizeCategory.Duplicates, size, 0);
        }
    }
}
