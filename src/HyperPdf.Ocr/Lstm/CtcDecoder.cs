// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Lstm;

/// <summary>
/// Reads the characters off a network's output by taking the likeliest code at each step. A code that repeats is one
/// character; the same code twice in a row is two only when the network gives the blank code between them.
/// </summary>
public static class CtcDecoder
{
    /// <summary>Decodes a line.</summary>
    /// <param name="outputs">The network's output, one row of steps with a probability for each code.</param>
    /// <param name="coder">The coder that turns codes into characters.</param>
    /// <param name="blank">The code that means no character.</param>
    /// <param name="characters">Receives the characters.</param>
    /// <param name="scratch">The working memory.</param>
    public static void Decode(FeatureMap outputs, CharacterCoder coder, int blank, List<RecognizedCharacter> characters, RecognitionScratch scratch)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(coder);
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(scratch);
        var pending = scratch.StartCharacter(coder.Start());
        var previous = -1;
        for (var step = 0; step < outputs.Steps; step++)
        {
            var (code, probability) = Likeliest(outputs.Step(step));
            if (code == blank)
            {
                previous = code;
                continue;
            }

            if (code == previous)
            {
                pending.Extend(step, probability);
                continue;
            }

            previous = code;
            Advance(pending, coder, code, step, probability, characters);
        }

        Flush(pending, characters);
    }

    /// <summary>Finds the likeliest code in a step.</summary>
    /// <param name="step">The probabilities.</param>
    /// <returns>The code and its probability.</returns>
    private static CodeChoice Likeliest(ReadOnlySpan<float> step)
    {
        var best = 0;
        for (var i = 1; i < step.Length; i++)
        {
            if (step[i] > step[best])
            {
                best = i;
            }
        }

        return new(best, step[best]);
    }

    /// <summary>Takes a new code, either continuing the character in progress or starting the next.</summary>
    /// <param name="pending">The character in progress.</param>
    /// <param name="coder">The coder.</param>
    /// <param name="code">The new code.</param>
    /// <param name="step">The step the code was seen at.</param>
    /// <param name="probability">The probability of the code.</param>
    /// <param name="characters">Receives a finished character.</param>
    private static void Advance(PendingCharacter pending, CharacterCoder coder, int code, int step, float probability, List<RecognizedCharacter> characters)
    {
        if (pending.InProgress && pending.Node.TryGetChild(code, out var next))
        {
            pending.Continue(next, step, probability);
            return;
        }

        Flush(pending, characters);
        pending.Reset(coder.Start());
        if (pending.Node.TryGetChild(code, out var first))
        {
            pending.Begin(first, step, probability);
        }
    }

    /// <summary>Adds the character in progress, if it spells one.</summary>
    /// <param name="pending">The character in progress.</param>
    /// <param name="characters">Receives the character.</param>
    private static void Flush(PendingCharacter pending, List<RecognizedCharacter> characters)
    {
        if (pending.InProgress && pending.Node.Character >= 0)
        {
            characters.Add(new(pending.Node.Character, pending.FirstStep, pending.LastStep, pending.Confidence));
        }
    }
}
