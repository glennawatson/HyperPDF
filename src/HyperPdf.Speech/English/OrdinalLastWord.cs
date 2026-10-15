// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>Passes words on, turning the last one into its ordinal: "twenty one" becomes "twenty first".</summary>
/// <typeparam name="TSink">The sink the words go to.</typeparam>
/// <param name="Inner">The sink the words go to, with whatever it has recorded.</param>
internal record struct OrdinalLastWord<TSink>(TSink Inner) : IWordSink
    where TSink : struct, IWordSink
{
    /// <summary>The latest word, held back until another word follows or the number ends.</summary>
    private string? _pending;

    /// <inheritdoc/>
    public void Add(string word)
    {
        if (_pending is not null)
        {
            Inner.Add(_pending);
        }

        _pending = word;
    }

    /// <summary>Passes on the held back word as an ordinal.</summary>
    internal void Finish()
    {
        if (_pending is null)
        {
            return;
        }

        Inner.Add(NumberWords.OrdinalOf(_pending));
        _pending = null;
    }
}
