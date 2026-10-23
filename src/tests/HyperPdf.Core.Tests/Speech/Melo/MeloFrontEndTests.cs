// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using HyperPdf.Speech.Melo;

namespace HyperPdf.Core.Tests.Speech.Melo;

/// <summary>
/// Checks the C# port of MeloTTS's English front end against what the Python original made of the same sentences:
/// the same phones, tones, languages, BERT tokens and alignment, and the same spelling-to-sound guesses.
/// </summary>
public sealed class MeloFrontEndTests
{
    /// <summary>Gets the reference, read once.</summary>
    private static MeloReference Reference { get; } =
        JsonSerializer.Deserialize(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "melo-reference.json")), MeloReferenceJsonContext.Default.MeloReference)!;

    /// <summary>Gets the reference sentences' indexes.</summary>
    /// <returns>The indexes.</returns>
    public static IEnumerable<int> Cases() => Enumerable.Range(0, Reference.Cases.Length);

    /// <summary>Gets the reference words' indexes.</summary>
    /// <returns>The indexes.</returns>
    public static IEnumerable<int> Predictions() => Enumerable.Range(0, Reference.Predictions.Length);

    /// <summary>A sentence prepares exactly as MeloTTS prepares it.</summary>
    /// <param name="index">The sentence.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task PreparesLikeMeloTts(int index)
    {
        await MeloModelFixture.EnsureAsync();
        var expected = Reference.Cases[index];
        var frontEnd = MeloFrontEnd.Load(MeloModelFixture.Directory);
        var input = new MeloInput();

        frontEnd.Prepare(expected.Text, input);

        await Assert.That(string.Join(' ', input.Phones)).IsEqualTo(string.Join(' ', expected.Ids));
        await Assert.That(string.Join(' ', input.Tones)).IsEqualTo(string.Join(' ', expected.Tones));
        await Assert.That(string.Join(' ', input.Languages)).IsEqualTo(string.Join(' ', expected.Languages));
        await Assert.That(string.Join(' ', input.Tokens)).IsEqualTo(string.Join(' ', expected.TokenIds));
        await Assert.That(string.Join(' ', input.PhonesPerToken)).IsEqualTo(string.Join(' ', expected.WordToPhones));
        await Assert.That(input.PhonesPerToken.Sum()).IsEqualTo(input.Phones.Count);
    }

    /// <summary>The spelling-to-sound network guesses unknown words exactly as g2p_en does.</summary>
    /// <param name="index">The word.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Predictions))]
    public async Task GuessesLikeG2pEn(int index)
    {
        await MeloModelFixture.EnsureAsync();
        var expected = Reference.Predictions[index];
        var spelling = new SpellingToSound(await File.ReadAllBytesAsync(Path.Combine(MeloModelFixture.Directory, MeloModel.SpellingFile)));
        var phones = new List<string>();

        spelling.Predict(expected.Word, phones);

        await Assert.That(string.Join(' ', phones)).IsEqualTo(string.Join(' ', expected.Phones));
    }
}
