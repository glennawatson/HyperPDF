// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Tests.Speech.Melo;

/// <summary>One sentence as MeloTTS prepared it.</summary>
/// <param name="Text">The normalized text.</param>
/// <param name="Phones">The phone symbols.</param>
/// <param name="Ids">The symbol ids with blanks.</param>
/// <param name="Tones">The tone ids.</param>
/// <param name="Languages">The language ids.</param>
/// <param name="WordToPhones">The phones each BERT token covers.</param>
/// <param name="TokenIds">The BERT token ids.</param>
internal sealed record MeloReferenceCase(string Text, string[] Phones, long[] Ids, long[] Tones, long[] Languages, int[] WordToPhones, long[] TokenIds);
