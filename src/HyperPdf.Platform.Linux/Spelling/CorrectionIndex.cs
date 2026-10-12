// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Linux.Spelling;

/// <summary>
/// Finds the closest words to a misspelling with the symmetric delete method: every word is indexed under the hashes of
/// its first letters with up to two of them deleted, and a misspelling's own deletes find the words to compare.
/// Lookups reuse the index's buffers, so one caller at a time; they allocate nothing beyond the results.
/// </summary>
[DebuggerDisplay("CorrectionIndex: {_terms.Length} words")]
internal sealed class CorrectionIndex
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

    /// <summary>The bits a packed entry shifts its hash by, above the word number; also the bits in a hash.</summary>
    private const int HashShift = 32;

    /// <summary>The keys aimed for in each directory bucket, as a power of two: sixteen, one cache line.</summary>
    private const int KeysPerBucketBits = 4;

    /// <summary>The most directory bits, which keeps the directory to a few megabytes.</summary>
    private const int MaxDirectoryBits = 20;

    /// <summary>The top hash bits entries are placed by while the index is built.</summary>
    private const int BuildBucketBits = 16;

    /// <summary>How far a hash shifts right to give its build bucket.</summary>
    private const int BuildBucketShift = HashShift - BuildBucketBits;

    /// <summary>Flips a hash's sign bit so signed order becomes unsigned order.</summary>
    private const uint SignBit = 0x8000_0000;

    /// <summary>The words in lower case, by number.</summary>
    private readonly string[] _terms;

    /// <summary>The distinct delete hashes, in ascending order.</summary>
    private readonly int[] _keys;

    /// <summary>Where each key's words start in <see cref="_postings"/>; one longer than the keys.</summary>
    private readonly int[] _starts;

    /// <summary>Where the keys whose hashes start with each run of top bits begin; one longer than the buckets.</summary>
    private readonly int[] _directory;

    /// <summary>How far a hash shifts right to give its directory bucket.</summary>
    private readonly int _directoryShift;

    /// <summary>The words under each key, key by key, each a word number with the word's length above it.</summary>
    private readonly int[] _postings;

    /// <summary>The lookup that last compared each word, so a word found under several keys is compared once.</summary>
    private readonly int[] _seen;

    /// <summary>The closest words found by the current lookup.</summary>
    private readonly List<int> _matches = [];

    /// <summary>The misspelling's ASCII letter positions, all zero between lookups.</summary>
    private readonly ulong[] _asciiMasks = new ulong[LetterMasks.AsciiLetters];

    /// <summary>The misspelling's other letters.</summary>
    private readonly char[] _otherLetters = new char[MaxWordLength];

    /// <summary>The positions of the misspelling's other letters.</summary>
    private readonly ulong[] _otherMasks = new ulong[MaxWordLength];

    /// <summary>The length of the shortest word.</summary>
    private readonly int _shortest;

    /// <summary>The length of the longest word.</summary>
    private readonly int _longest;

    /// <summary>The current lookup's number, matched against <see cref="_seen"/>.</summary>
    private int _lookup;

    /// <summary>Initializes a new instance of the <see cref="CorrectionIndex"/> class.</summary>
    /// <param name="words">The words; words longer than <see cref="MaxWordLength"/> are left out.</param>
    internal CorrectionIndex(IReadOnlyCollection<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        List<string> terms = [with(Math.Min(words.Count, MaxWords))];
        foreach (var word in words)
        {
            if (word.Length <= MaxWordLength && terms.Count < MaxWords)
            {
                // Returns the same string when it is already lower case, as most word list entries are.
                terms.Add(word.ToLowerInvariant());
            }
        }

        _terms = [.. terms];
        _seen = new int[_terms.Length];
        _shortest = int.MaxValue;
        foreach (var term in _terms)
        {
            _shortest = Math.Min(_shortest, term.Length);
            _longest = Math.Max(_longest, term.Length);
        }

        var entries = SortedEntries(_terms);
        (_keys, _starts, _postings) = Group(entries);
        (_directory, _directoryShift) = Directory(_keys);
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
        if (_terms.Length == 0 || word.Length > _longest + MaxEditDistance || word.Length + MaxEditDistance < _shortest)
        {
            return;
        }

        var prefix = word[..Math.Min(word.Length, PrefixLength)];
        Span<int> hashes = stackalloc int[MaxDeletes];

        // The misspelling's letter positions are worked out once and compared with every candidate. The tables are kept
        // between lookups, and only the entries this word set are cleared afterwards.
        var masks = new LetterMasks(word, _asciiMasks, _otherLetters, _otherMasks);
        var lookup = NextLookup();
        var best = MaxEditDistance;
        _matches.Clear();
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
                best = Search(masks, hashes[..count], prefix.Length - level, lookup, best);
            }
        }
        finally
        {
            masks.Release();
        }

        // Word numbers follow the word list, which is in alphabetical order on Linux desktops.
        if (_matches.Count > 1)
        {
            _matches.Sort();
        }

        foreach (var number in CollectionsMarshal.AsSpan(_matches)[..Math.Min(limit, _matches.Count)])
        {
            found.Add(_terms[number]);
        }
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

    /// <summary>Splits sorted packed entries into the distinct hashes, where each starts, and the word numbers.</summary>
    /// <param name="entries">The entries, sorted.</param>
    /// <returns>The keys, starts and postings.</returns>
    private static (int[] Keys, int[] Starts, int[] Postings) Group(ReadOnlySpan<long> entries)
    {
        var keyCount = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0 && HashOf(entries[i]) == HashOf(entries[i - 1]))
            {
                continue;
            }

            keyCount++;
        }

        var keys = GC.AllocateUninitializedArray<int>(keyCount);
        var starts = GC.AllocateUninitializedArray<int>(keyCount + 1);
        var postings = GC.AllocateUninitializedArray<int>(entries.Length);
        var key = -1;
        for (var i = 0; i < entries.Length; i++)
        {
            var hash = HashOf(entries[i]);
            if (key < 0 || keys[key] != hash)
            {
                key++;
                keys[key] = hash;
                starts[key] = i;
            }

            postings[i] = (int)entries[i];
        }

        starts[keyCount] = entries.Length;
        return (keys, starts, postings);
    }

    /// <summary>
    /// Makes every word's delete entries, each a hash packed above the word's length and number, in ascending order.
    /// The entries are counted and placed by the top bits of their hash first, so only small runs need sorting.
    /// </summary>
    /// <param name="terms">The words.</param>
    /// <returns>The entries.</returns>
    private static long[] SortedEntries(string[] terms)
    {
        Span<int> hashes = stackalloc int[MaxDeletes];
        var starts = new int[(1 << BuildBucketBits) + 1];
        var total = 0;
        foreach (var term in terms)
        {
            var count = DeleteHashes(term, hashes);
            total += count;
            foreach (var hash in hashes[..count])
            {
                starts[(Ordered(hash) >> BuildBucketShift) + 1]++;
            }
        }

        for (var bucket = 1; bucket < starts.Length; bucket++)
        {
            starts[bucket] += starts[bucket - 1];
        }

        var entries = GC.AllocateUninitializedArray<long>(total);
        var next = starts.AsSpan(0, starts.Length - 1).ToArray();
        for (var number = 0; number < terms.Length; number++)
        {
            var count = DeleteHashes(terms[number], hashes);
            var posting = (uint)(terms[number].Length << NumberBits) | (uint)number;
            foreach (var hash in hashes[..count])
            {
                var bucket = Ordered(hash) >> BuildBucketShift;
                entries[next[bucket]] = ((long)hash << HashShift) | posting;
                next[bucket]++;
            }
        }

        for (var bucket = 0; bucket < starts.Length - 1; bucket++)
        {
            entries.AsSpan(starts[bucket]..starts[bucket + 1]).Sort();
        }

        return entries;
    }

    /// <summary>Builds the directory from each hash's top bits to the first key with them, with about a cache line of keys each.</summary>
    /// <param name="keys">The keys, in ascending order.</param>
    /// <returns>The directory, one longer than its buckets, and how far a hash shifts to give its bucket.</returns>
    private static (int[] Directory, int Shift) Directory(ReadOnlySpan<int> keys)
    {
        var bits = Math.Clamp(BitOperations.Log2((uint)Math.Max(keys.Length, 1)) - KeysPerBucketBits, 1, MaxDirectoryBits);
        var shift = HashShift - bits;
        var directory = GC.AllocateUninitializedArray<int>((1 << bits) + 1);
        var key = 0;
        for (var bucket = 0; bucket < directory.Length - 1; bucket++)
        {
            while (key < keys.Length && (int)(Ordered(keys[key]) >> shift) < bucket)
            {
                key++;
            }

            directory[bucket] = key;
        }

        directory[^1] = keys.Length;
        return (directory, shift);
    }

    /// <summary>Maps a hash to an unsigned number in the same order, so its top bits pick its bucket.</summary>
    /// <param name="hash">The hash.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Ordered(int hash) => (uint)hash ^ SignBit;

    /// <summary>Gets the hash packed into an entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashOf(long entry) => (int)(entry >> HashShift);

    /// <summary>Finds a hash among the keys through the directory of its top bits.</summary>
    /// <param name="hash">The hash.</param>
    /// <returns>The key's position, or -1 when no word has the hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindKey(int hash)
    {
        var bucket = (int)(Ordered(hash) >> _directoryShift);
        var start = _directory[bucket];

        // A bucket holds a handful of keys, so a vectorised scan beats a binary search.
        var found = _keys.AsSpan(start.._directory[bucket + 1]).IndexOf(hash);
        return found < 0 ? -1 : start + found;
    }

    /// <summary>Compares a misspelling with the words under each of its delete hashes.</summary>
    /// <param name="word">The misspelling's letter positions.</param>
    /// <param name="hashes">The delete hashes.</param>
    /// <param name="keyLength">The length of the deleted prefix the hashes stand for.</param>
    /// <param name="lookup">The lookup's number.</param>
    /// <param name="best">The closest distance so far.</param>
    /// <returns>The closest distance after these words.</returns>
    private int Search(scoped in LetterMasks word, ReadOnlySpan<int> hashes, int keyLength, int lookup, int best)
    {
        foreach (var hash in hashes)
        {
            var key = FindKey(hash);
            if (key >= 0)
            {
                best = Compare(word, _postings.AsSpan(_starts[key].._starts[key + 1]), keyLength, lookup, best);
            }
        }

        return best;
    }

    /// <summary>Compares a misspelling with the words under one key, keeping the closest in <see cref="_matches"/>.</summary>
    /// <param name="word">The misspelling's letter positions.</param>
    /// <param name="postings">The words under the key.</param>
    /// <param name="keyLength">The length of the deleted prefix the key stands for.</param>
    /// <param name="lookup">The lookup's number.</param>
    /// <param name="best">The closest distance so far.</param>
    /// <returns>The closest distance after these words.</returns>
    private int Compare(scoped in LetterMasks word, ReadOnlySpan<int> postings, int keyLength, int lookup, int best)
    {
        foreach (var posting in postings)
        {
            var termLength = posting >>> NumberBits;

            // Each letter of length difference is a change. A word that needed more deletes than the closest distance
            // to reach this key is found under another key if it is close enough; it is not marked as seen for that.
            if (Math.Abs(termLength - word.Length) > best || Math.Min(termLength, PrefixLength) - keyLength > best)
            {
                continue;
            }

            var number = posting & NumberMask;
            if (_seen[number] == lookup)
            {
                continue;
            }

            _seen[number] = lookup;
            var distance = word.Distance(_terms[number], best);
            if (distance > best)
            {
                continue;
            }

            if (distance < best)
            {
                // A closer word replaces the ones found so far, and later words must be at least as close.
                best = distance;
                _matches.Clear();
            }

            _matches.Add(number);
        }

        return best;
    }

    /// <summary>Starts a lookup, clearing the compared marks once the lookup numbers wrap.</summary>
    /// <returns>The lookup's number.</returns>
    private int NextLookup()
    {
        _lookup++;
        if (_lookup == int.MaxValue)
        {
            Array.Clear(_seen);
            _lookup = 1;
        }

        return _lookup;
    }
}
