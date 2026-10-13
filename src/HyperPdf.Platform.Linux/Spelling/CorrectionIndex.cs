// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Linux.Spelling;

/// <summary>
/// Finds the closest words to a misspelling with the symmetric delete method: every word is indexed under the hashes of
/// its first letters with up to two of them deleted, and a misspelling's own deletes find the words to compare.
/// The index lives in arrays rented from <see cref="ArrayPool{T}.Shared"/> and handed back on dispose, so building it
/// again allocates nothing once the pool holds them. Lookups reuse the index's buffers, so one caller at a time; they
/// allocate nothing beyond the results.
/// </summary>
[DebuggerDisplay("CorrectionIndex: {_indexed} words")]
internal sealed class CorrectionIndex : IDisposable
{
    /// <summary>The most letters changed between a misspelling and a correction.</summary>
    internal const int MaxEditDistance = 2;

    /// <summary>The longest word indexed or looked up; it bounds the stack used for comparisons.</summary>
    internal const int MaxWordLength = 64;

    /// <summary>The leading letters indexed; later letters are compared but not indexed, which keeps the index small.</summary>
    private const int PrefixLength = DeleteHasher.PrefixLength;

    /// <summary>The letters deleted to make each pair delete.</summary>
    private const int PairDeleted = 2;

    /// <summary>The most deletes of a prefix: itself, each letter deleted, and each pair deleted (1 + 7 + 21).</summary>
    private const int MaxDeletes = 1 + PrefixLength + (PrefixLength * (PrefixLength - 1) / PairDeleted);

    /// <summary>The numbers of letters deleted: none, one and two.</summary>
    private const int Levels = MaxEditDistance + 1;

    /// <summary>The low bits of a posting that hold the word's number; the bits above hold its length.</summary>
    private const int NumberBits = 24;

    /// <summary>Picks a word's number out of a posting.</summary>
    private const int NumberMask = (1 << NumberBits) - 1;

    /// <summary>The most words indexed, far more than any word list holds.</summary>
    private const int MaxWords = 1 << NumberBits;

    /// <summary>The bits in a hash.</summary>
    private const int HashBits = 32;

    /// <summary>The entries aimed for in each directory bucket, as a power of two: sixteen, one cache line.</summary>
    private const int EntriesPerBucketBits = 4;

    /// <summary>The most directory bits, which keeps the directory to a few megabytes.</summary>
    private const int MaxDirectoryBits = 20;

    /// <summary>Flips a hash's sign bit so signed order becomes unsigned order.</summary>
    private const uint SignBit = 0x8000_0000;

    /// <summary>The bits in a word of the lower-case marks.</summary>
    private const int MarkBits = 64;

    /// <summary>The matches room starts with.</summary>
    private const int FirstMatchRoom = 64;

    /// <summary>How many times larger the matches grow when full.</summary>
    private const int MatchGrowth = 2;

    /// <summary>The words, as the list wrote them.</summary>
    private readonly WordPool _pool;

    /// <summary>The words that are already lower case, one bit each, so those need no copy to compare.</summary>
    private readonly ulong[] _lowerMarks;

    /// <summary>The delete hashes, grouped by directory bucket in the order words were added.</summary>
    private readonly int[] _hashes;

    /// <summary>The word under each entry of <see cref="_hashes"/>: its number with its length above it.</summary>
    private readonly int[] _postings;

    /// <summary>Where the entries of each top-bits run begin; one longer than the buckets.</summary>
    private readonly int[] _directory;

    /// <summary>How far a hash shifts right to give its directory bucket.</summary>
    private readonly int _directoryShift;

    /// <summary>The lookup that last compared each word, so a word found under several keys is compared once.</summary>
    private readonly int[] _seen;

    /// <summary>The misspelling's ASCII letter positions, all zero between lookups.</summary>
    private readonly ulong[] _asciiMasks;

    /// <summary>The misspelling's other letters.</summary>
    private readonly char[] _otherLetters;

    /// <summary>The positions of the misspelling's other letters.</summary>
    private readonly ulong[] _otherMasks;

    /// <summary>The number of words the pool holds that are indexed or skipped.</summary>
    private readonly int _wordCount;

    /// <summary>The number of words indexed.</summary>
    private readonly int _indexed;

    /// <summary>The length of the shortest indexed word.</summary>
    private readonly int _shortest;

    /// <summary>The length of the longest indexed word.</summary>
    private readonly int _longest;

    /// <summary>The correction strings made so far, by word number, so a correction offered again makes none.</summary>
    private Dictionary<int, string>? _strings;

    /// <summary>The closest words found by the current lookup.</summary>
    private int[] _matches;

    /// <summary>The number of words in <see cref="_matches"/>.</summary>
    private int _matchCount;

    /// <summary>The current lookup's number, matched against <see cref="_seen"/>.</summary>
    private int _lookup;

    /// <summary>Initializes a new instance of the <see cref="CorrectionIndex"/> class.</summary>
    /// <param name="pool">The words; words longer than <see cref="MaxWordLength"/> are left out. The pool must outlive the index.</param>
    internal CorrectionIndex(WordPool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
        _wordCount = Math.Min(pool.Count, MaxWords);
        _lowerMarks = Rent<ulong>((_wordCount + MarkBits - 1) / MarkBits);
        _seen = Rent<int>(_wordCount);
        _asciiMasks = Rent<ulong>(LetterMasks.AsciiLetters);
        _otherLetters = ArrayPool<char>.Shared.Rent(MaxWordLength);
        _otherMasks = Rent<ulong>(MaxWordLength);
        _matches = ArrayPool<int>.Shared.Rent(FirstMatchRoom);
        _shortest = int.MaxValue;
        var estimate = 0L;
        for (var number = 0; number < _wordCount; number++)
        {
            var length = pool.Word(number).Length;
            if (length > MaxWordLength)
            {
                continue;
            }

            _indexed++;
            _shortest = Math.Min(_shortest, length);
            _longest = Math.Max(_longest, length);
            estimate += DeletesOf(length);
        }

        var bits = Math.Clamp(BitOperations.Log2((ulong)Math.Max(estimate, 1)) - EntriesPerBucketBits, 1, MaxDirectoryBits);
        _directoryShift = HashBits - bits;
        _directory = Rent<int>((1 << bits) + 1);
        var total = CountEntries(_directory);
        _hashes = ArrayPool<int>.Shared.Rent(Math.Max(total, 1));
        _postings = ArrayPool<int>.Shared.Rent(Math.Max(total, 1));
        FillEntries();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        ArrayPool<ulong>.Shared.Return(_lowerMarks);
        ArrayPool<int>.Shared.Return(_hashes);
        ArrayPool<int>.Shared.Return(_postings);
        ArrayPool<int>.Shared.Return(_directory);
        ArrayPool<int>.Shared.Return(_seen);
        ArrayPool<ulong>.Shared.Return(_asciiMasks);
        ArrayPool<char>.Shared.Return(_otherLetters);
        ArrayPool<ulong>.Shared.Return(_otherMasks);
        ArrayPool<int>.Shared.Return(_matches);
    }

    /// <summary>Counts the changes between two words: letters added, removed, replaced, or two neighbours swapped.</summary>
    /// <param name="first">The first word, no longer than <see cref="MaxWordLength"/>.</param>
    /// <param name="second">The second word, no longer than <see cref="MaxWordLength"/>.</param>
    /// <param name="limit">The most changes that matter.</param>
    /// <returns>The changes, or more than <paramref name="limit"/> once past it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Distance(ReadOnlySpan<char> first, ReadOnlySpan<char> second, int limit) =>
        new LetterMasks(first, stackalloc ulong[LetterMasks.AsciiLetters], stackalloc char[MaxWordLength], stackalloc ulong[MaxWordLength]).Distance(second, limit);

    /// <summary>Adds the words closest to a misspelling, up to two letters away, in word list order.</summary>
    /// <param name="word">The misspelling in lower case, no longer than <see cref="MaxWordLength"/>.</param>
    /// <param name="found">Receives the words.</param>
    /// <param name="limit">The most words added.</param>
    internal void Lookup(ReadOnlySpan<char> word, List<string> found, int limit)
    {
        ArgumentNullException.ThrowIfNull(found);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(word.Length, MaxWordLength);

        // Every word is more changes away than allowed when the index is empty or holds only longer or shorter words.
        if (_indexed == 0 || word.Length > _longest + MaxEditDistance || word.Length + MaxEditDistance < _shortest)
        {
            return;
        }

        Span<char> scratch = stackalloc char[MaxWordLength];
        FindMatches(word, scratch);

        // Word numbers follow the word list, which is in alphabetical order on Linux desktops.
        var matches = _matches.AsSpan(0, _matchCount);
        if (matches.Length > 1)
        {
            matches.Sort();
        }

        foreach (var number in matches[..Math.Min(limit, matches.Length)])
        {
            found.Add(TermString(number, scratch));
        }
    }

    /// <summary>Rents a zeroed array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="length">The elements needed.</param>
    /// <returns>An array at least that long, with its first elements zero.</returns>
    private static T[] Rent<T>(int length)
    {
        var array = ArrayPool<T>.Shared.Rent(Math.Max(length, 1));
        array.AsSpan(0, length).Clear();
        return array;
    }

    /// <summary>Gets the most delete hashes a word makes, before repeated ones are dropped.</summary>
    /// <param name="length">The word's length.</param>
    /// <returns>The hashes.</returns>
    private static int DeletesOf(int length)
    {
        var prefix = Math.Min(length, PrefixLength);
        return 1 + prefix + (prefix * (prefix - 1) / PairDeleted);
    }

    /// <summary>Writes the distinct hashes of a word's first letters with none, one and two of them deleted.</summary>
    /// <param name="word">The word.</param>
    /// <param name="hashes">Receives the hashes; <see cref="MaxDeletes"/> long.</param>
    /// <returns>How many were written.</returns>
    private static int DeleteHashes(ReadOnlySpan<char> word, Span<int> hashes)
    {
        var prefix = word[..Math.Min(word.Length, PrefixLength)];
        var count = 0;
        for (var level = 0; level < Levels; level++)
        {
            count += LevelHashes(prefix, level, hashes[count..]);
        }

        return count;
    }

    /// <summary>Writes the distinct hashes of a prefix with a number of its letters deleted.</summary>
    /// <param name="prefix">The prefix, no longer than <see cref="PrefixLength"/>.</param>
    /// <param name="level">The letters deleted: none, one or two.</param>
    /// <param name="hashes">Receives the hashes.</param>
    /// <returns>How many were written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int LevelHashes(ReadOnlySpan<char> prefix, int level, Span<int> hashes) =>
        Distinct(hashes[..DeleteHasher.Write(prefix, level, hashes)]);

    /// <summary>Sorts hashes and moves the distinct ones to the front; repeated letters give repeated deletes.</summary>
    /// <param name="hashes">The hashes.</param>
    /// <returns>How many are distinct.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Distinct(Span<int> hashes)
    {
        hashes.Sort();
        var distinct = 0;
        foreach (var hash in hashes)
        {
            if (distinct > 0 && hashes[distinct - 1] == hash)
            {
                continue;
            }

            hashes[distinct] = hash;
            distinct++;
        }

        return distinct;
    }

    /// <summary>Maps a hash to an unsigned number in the same order, so its top bits pick its bucket.</summary>
    /// <param name="hash">The hash.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Ordered(int hash) => (uint)hash ^ SignBit;

    /// <summary>Finds the next entry in a bucket with the same hash.</summary>
    /// <param name="run">The bucket's hashes.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="after">The position of the entry just handled.</param>
    /// <returns>The next position, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NextEqual(ReadOnlySpan<int> run, int hash, int after)
    {
        var next = after + 1;
        var found = run[next..].IndexOf(hash);
        return found < 0 ? -1 : next + found;
    }

    /// <summary>Counts the entries each directory bucket will hold and turns the counts into start positions, marking lower-case words on the way.</summary>
    /// <param name="directory">The zeroed directory; ends up holding where each bucket's entries start.</param>
    /// <returns>The number of entries.</returns>
    private int CountEntries(int[] directory)
    {
        Span<int> hashes = stackalloc int[MaxDeletes];
        Span<char> scratch = stackalloc char[MaxWordLength];
        for (var number = 0; number < _wordCount; number++)
        {
            var word = _pool.Word(number);
            if (word.Length > MaxWordLength)
            {
                continue;
            }

            var term = scratch[..word.ToLowerInvariant(scratch)];
            if (term.SequenceEqual(word))
            {
                _lowerMarks[number / MarkBits] |= 1UL << (number % MarkBits);
            }

            foreach (var hash in hashes[..DeleteHashes(term, hashes)])
            {
                directory[(int)(Ordered(hash) >> _directoryShift)]++;
            }
        }

        var buckets = (int)(uint.MaxValue >> _directoryShift) + 1;
        var total = 0;
        for (var bucket = 0; bucket < buckets; bucket++)
        {
            var count = directory[bucket];
            directory[bucket] = total;
            total += count;
        }

        directory[buckets] = total;
        return total;
    }

    /// <summary>Places every word's delete entries in their directory buckets, then shifts the directory so each bucket starts where it should.</summary>
    private void FillEntries()
    {
        Span<int> hashes = stackalloc int[MaxDeletes];
        Span<char> scratch = stackalloc char[MaxWordLength];
        for (var number = 0; number < _wordCount; number++)
        {
            var word = _pool.Word(number);
            if (word.Length > MaxWordLength)
            {
                continue;
            }

            var term = Term(number, scratch);
            var posting = (int)((uint)(term.Length << NumberBits) | (uint)number);
            foreach (var hash in hashes[..DeleteHashes(term, hashes)])
            {
                var at = _directory[(int)(Ordered(hash) >> _directoryShift)]++;
                _hashes[at] = hash;
                _postings[at] = posting;
            }
        }

        // Each bucket's cursor ended at the start of the next, so moving them up one gives the starts.
        var buckets = (int)(uint.MaxValue >> _directoryShift) + 1;
        _directory.AsSpan(0, buckets).CopyTo(_directory.AsSpan(1));
        _directory[0] = 0;
    }

    /// <summary>Gets a word in lower case, without copying it when it is already.</summary>
    /// <param name="number">The word's number.</param>
    /// <param name="scratch">Room for the lower-case copy of a word that needs one.</param>
    /// <returns>The word in lower case.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<char> Term(int number, Span<char> scratch)
    {
        var word = _pool.Word(number);
        return (_lowerMarks[number / MarkBits] & (1UL << (number % MarkBits))) != 0 ? word : scratch[..word.ToLowerInvariant(scratch)];
    }

    /// <summary>Gets the lower-case string for a correction, making it the first time it is offered.</summary>
    /// <param name="number">The word's number.</param>
    /// <param name="scratch">Room for the lower-case copy of a word that needs one.</param>
    /// <returns>The word.</returns>
    private string TermString(int number, Span<char> scratch)
    {
        var strings = _strings ??= [];
        ref var text = ref CollectionsMarshal.GetValueRefOrAddDefault(strings, number, out var exists);
        if (!exists)
        {
            text = new(Term(number, scratch));
        }

        return text!;
    }

    /// <summary>Finds the words closest to a misspelling and leaves their numbers in <see cref="_matches"/>.</summary>
    /// <param name="word">The misspelling in lower case.</param>
    /// <param name="scratch">Room for the lower-case copy of a word that needs one.</param>
    private void FindMatches(ReadOnlySpan<char> word, Span<char> scratch)
    {
        var prefix = word[..Math.Min(word.Length, PrefixLength)];
        Span<int> hashes = stackalloc int[MaxDeletes];

        // The misspelling's letter positions are worked out once and compared with every candidate. The tables are kept
        // between lookups, and only the entries this word set are cleared afterwards.
        var masks = new LetterMasks(word, _asciiMasks, _otherLetters, _otherMasks);
        var lookup = NextLookup();
        var best = MaxEditDistance;
        _matchCount = 0;
        try
        {
            // A word d changes away shares a delete with the misspelling's own deletes of at most d letters, so once a
            // word is found the deletes of more letters than its distance cannot find a closer or equal one, and are
            // never made.
            for (var level = 0; level < Levels; level++)
            {
                if (level > best)
                {
                    break;
                }

                var count = LevelHashes(prefix, level, hashes);
                best = Search(masks, hashes[..count], prefix.Length - level, lookup, best, scratch);
            }
        }
        finally
        {
            masks.Release();
        }
    }

    /// <summary>Compares a misspelling with the words under each of its delete hashes.</summary>
    /// <param name="word">The misspelling's letter positions.</param>
    /// <param name="hashes">The delete hashes.</param>
    /// <param name="keyLength">The length of the deleted prefix the hashes stand for.</param>
    /// <param name="lookup">The lookup's number.</param>
    /// <param name="best">The closest distance so far.</param>
    /// <param name="scratch">Room for the lower-case copy of a word that needs one.</param>
    /// <returns>The closest distance after these words.</returns>
    private int Search(scoped in LetterMasks word, ReadOnlySpan<int> hashes, int keyLength, int lookup, int best, Span<char> scratch)
    {
        foreach (var hash in hashes)
        {
            // A bucket holds a handful of entries, so a vectorised scan beats a binary search; equal hashes sit together.
            var bucket = (int)(Ordered(hash) >> _directoryShift);
            var start = _directory[bucket];
            var run = _hashes.AsSpan(start, _directory[bucket + 1] - start);
            for (var at = run.IndexOf(hash); at >= 0; at = NextEqual(run, hash, at))
            {
                best = Compare(word, _postings[start + at], keyLength, lookup, best, scratch);
            }
        }

        return best;
    }

    /// <summary>Compares a misspelling with one word, keeping the closest in <see cref="_matches"/>.</summary>
    /// <param name="word">The misspelling's letter positions.</param>
    /// <param name="posting">The word's number with its length above it.</param>
    /// <param name="keyLength">The length of the deleted prefix the key stands for.</param>
    /// <param name="lookup">The lookup's number.</param>
    /// <param name="best">The closest distance so far.</param>
    /// <param name="scratch">Room for the lower-case copy of a word that needs one.</param>
    /// <returns>The closest distance after this word.</returns>
    private int Compare(scoped in LetterMasks word, int posting, int keyLength, int lookup, int best, Span<char> scratch)
    {
        var termLength = posting >>> NumberBits;

        // Each letter of length difference is a change. A word that needed more deletes than the closest distance
        // to reach this key is found under another key if it is close enough; it is not marked as seen for that.
        if (Math.Abs(termLength - word.Length) > best || Math.Min(termLength, PrefixLength) - keyLength > best)
        {
            return best;
        }

        var number = posting & NumberMask;
        if (_seen[number] == lookup)
        {
            return best;
        }

        _seen[number] = lookup;
        var distance = word.Distance(Term(number, scratch), best);
        if (distance > best)
        {
            return best;
        }

        if (distance < best)
        {
            // A closer word replaces the ones found so far, and later words must be at least as close.
            best = distance;
            _matchCount = 0;
        }

        AddMatch(number);
        return best;
    }

    /// <summary>Adds a word number to the matches, renting more room when they are full.</summary>
    /// <param name="number">The word's number.</param>
    private void AddMatch(int number)
    {
        if (_matchCount == _matches.Length)
        {
            var bigger = ArrayPool<int>.Shared.Rent(_matches.Length * MatchGrowth);
            _matches.AsSpan(0, _matchCount).CopyTo(bigger);
            ArrayPool<int>.Shared.Return(_matches);
            _matches = bigger;
        }

        _matches[_matchCount] = number;
        _matchCount++;
    }

    /// <summary>Starts a lookup, clearing the compared marks once the lookup numbers wrap.</summary>
    /// <returns>The lookup's number.</returns>
    private int NextLookup()
    {
        _lookup++;
        if (_lookup == int.MaxValue)
        {
            _seen.AsSpan(0, _wordCount).Clear();
            _lookup = 1;
        }

        return _lookup;
    }
}
