// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;

namespace HyperPdf.Vision;

/// <summary>How a caption is generated: how long it may be, which token ends it, and which words never appear.</summary>
/// <param name="MaximumLength">The most tokens, including the start token.</param>
/// <param name="EndToken">The token that ends a caption.</param>
/// <param name="Blocked">Whether each token id may never be chosen.</param>
/// <param name="BlockedSequences">Sequences of two or more tokens whose last token may not follow the rest.</param>
[DebuggerDisplay("GenerationSettings: up to {MaximumLength} tokens")]
internal sealed record GenerationSettings(int MaximumLength, int EndToken, bool[] Blocked, List<int[]> BlockedSequences)
{
    /// <summary>The most tokens a caption may have when the settings do not say.</summary>
    private const int DefaultMaximumLength = 40;

    /// <summary>The token that ends a caption when the settings do not say.</summary>
    private const int DefaultEndToken = 50_256;

    /// <summary>Reads the settings from <c>generation_config.json</c>.</summary>
    /// <param name="json">The file's bytes.</param>
    /// <param name="vocabularySize">The number of token ids.</param>
    /// <returns>The settings.</returns>
    internal static GenerationSettings Parse(byte[] json, int vocabularySize)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var maximum = root.TryGetProperty("max_length", out var length) && length.TryGetInt32(out var value) ? value : DefaultMaximumLength;
        var end = root.TryGetProperty("eos_token_id", out var eos) && eos.TryGetInt32(out var token) ? token : DefaultEndToken;
        var blocked = new bool[vocabularySize];
        List<int[]> sequences = [];
        if (root.TryGetProperty("bad_words_ids", out var bad))
        {
            foreach (var entry in bad.EnumerateArray())
            {
                AddBlocked(entry, blocked, sequences);
            }
        }

        return new(maximum, end, blocked, sequences);
    }

    /// <summary>Picks the most likely token that is allowed.</summary>
    /// <param name="logits">The scores of every token for the next place.</param>
    /// <param name="ids">The tokens so far.</param>
    /// <returns>The token.</returns>
    internal long Pick(ReadOnlySpan<float> logits, long[] ids)
    {
        var best = -1;
        var bestScore = float.NegativeInfinity;
        for (var token = 0; token < logits.Length && token < Blocked.Length; token++)
        {
            if (logits[token] <= bestScore || Blocked[token] || IsBlockedBySequence(ids, token))
            {
                continue;
            }

            best = token;
            bestScore = logits[token];
        }

        return best < 0 ? EndToken : best;
    }

    /// <summary>Records a word to avoid, as one blocked token or a blocked sequence.</summary>
    /// <param name="entry">The JSON array of token ids that spell the word.</param>
    /// <param name="blocked">Receives single blocked tokens.</param>
    /// <param name="sequences">Receives blocked sequences.</param>
    private static void AddBlocked(JsonElement entry, bool[] blocked, List<int[]> sequences)
    {
        var ids = new int[entry.GetArrayLength()];
        var index = 0;
        foreach (var id in entry.EnumerateArray())
        {
            ids[index] = id.GetInt32();
            index++;
        }

        if (ids.Length == 1 && (uint)ids[0] < (uint)blocked.Length)
        {
            blocked[ids[0]] = true;
        }
        else if (ids.Length > 1)
        {
            sequences.Add(ids);
        }
    }

    /// <summary>Determines whether the tokens so far end with all but the last token of a blocked sequence.</summary>
    /// <param name="ids">The tokens so far.</param>
    /// <param name="sequence">The blocked sequence.</param>
    /// <returns><see langword="true"/> when the prefix of the sequence is at the end of the tokens.</returns>
    private static bool EndsWith(long[] ids, int[] sequence)
    {
        var prefix = sequence.Length - 1;
        if (prefix > ids.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix; i++)
        {
            if (ids[ids.Length - prefix + i] != sequence[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether a token would finish a blocked sequence.</summary>
    /// <param name="ids">The tokens so far.</param>
    /// <param name="token">The candidate.</param>
    /// <returns><see langword="true"/> when the token must not be chosen.</returns>
    private bool IsBlockedBySequence(long[] ids, int token)
    {
        foreach (var sequence in BlockedSequences)
        {
            if (sequence[^1] == token && EndsWith(ids, sequence))
            {
                return true;
            }
        }

        return false;
    }
}
