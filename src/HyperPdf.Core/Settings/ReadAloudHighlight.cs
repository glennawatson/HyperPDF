// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>How much of the text being read aloud is marked.</summary>
public enum ReadAloudHighlight
{
    /// <summary>The sentence being read.</summary>
    Sentence = 0,

    /// <summary>The sentence, and within it the word being read.</summary>
    SentenceAndWord = 1,
}
