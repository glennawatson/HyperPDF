// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Speech.Melo;

/// <summary>What MeloTTS reads for one piece of text: phones with blanks between them, their tones and language, and the BERT tokens with how many phones each covers.</summary>
[DebuggerDisplay("MeloInput: {Phones.Count} phones, {Tokens.Count} tokens")]
internal sealed class MeloInput
{
    /// <summary>Gets the phone symbol ids, with a blank before, between and after.</summary>
    internal List<long> Phones { get; } = [];

    /// <summary>Gets each phone's tone id.</summary>
    internal List<long> Tones { get; } = [];

    /// <summary>Gets each phone's language id.</summary>
    internal List<long> Languages { get; } = [];

    /// <summary>Gets the BERT token ids, with the start and end tokens.</summary>
    internal List<long> Tokens { get; } = [];

    /// <summary>Gets how many phones each BERT token covers.</summary>
    internal List<int> PhonesPerToken { get; } = [];

    /// <summary>Empties the input for the next piece of text.</summary>
    internal void Clear()
    {
        Phones.Clear();
        Tones.Clear();
        Languages.Clear();
        Tokens.Clear();
        PhonesPerToken.Clear();
    }
}
