// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>The character a decoder is part way through reading, which may take several codes.</summary>
[DebuggerDisplay("PendingCharacter: character {Node.Character}, steps {FirstStep}-{LastStep}")]
internal sealed class PendingCharacter
{
    /// <summary>The lowest of the highest probabilities of the codes finished so far.</summary>
    private float _finishedPeak;

    /// <summary>The highest probability seen for the code being read.</summary>
    private float _currentPeak;

    /// <summary>Initializes a new instance of the <see cref="PendingCharacter"/> class.</summary>
    /// <param name="start">The start of the tree of code runs.</param>
    internal PendingCharacter(CharacterCoder.CodeNode start) => Node = start;

    /// <summary>Gets the place in the tree the codes so far have reached.</summary>
    internal CharacterCoder.CodeNode Node { get; private set; }

    /// <summary>Gets a value indicating whether any code has been taken.</summary>
    internal bool InProgress { get; private set; }

    /// <summary>Gets the step of the first code.</summary>
    internal int FirstStep { get; private set; }

    /// <summary>Gets the step of the latest code.</summary>
    internal int LastStep { get; private set; }

    /// <summary>Gets how sure the network was: the lowest of the codes' highest probabilities.</summary>
    internal float Confidence => Math.Min(_finishedPeak, _currentPeak);

    /// <summary>Forgets the character and goes back to the start of the tree.</summary>
    /// <param name="start">The start of the tree.</param>
    internal void Reset(CharacterCoder.CodeNode start)
    {
        Node = start;
        InProgress = false;
    }

    /// <summary>Takes the first code of a character.</summary>
    /// <param name="node">The place in the tree the code reaches.</param>
    /// <param name="step">The step of the code.</param>
    /// <param name="probability">The code's probability.</param>
    internal void Begin(CharacterCoder.CodeNode node, int step, float probability)
    {
        Node = node;
        InProgress = true;
        FirstStep = step;
        LastStep = step;
        _finishedPeak = 1F;
        _currentPeak = probability;
    }

    /// <summary>Takes a further code of the same character.</summary>
    /// <param name="node">The place in the tree the code reaches.</param>
    /// <param name="step">The step of the code.</param>
    /// <param name="probability">The code's probability.</param>
    internal void Continue(CharacterCoder.CodeNode node, int step, float probability)
    {
        Node = node;
        LastStep = step;
        _finishedPeak = Confidence;
        _currentPeak = probability;
    }

    /// <summary>Extends the latest code over another step.</summary>
    /// <param name="step">The step.</param>
    /// <param name="probability">The probability at the step.</param>
    internal void Extend(int step, float probability)
    {
        if (!InProgress)
        {
            return;
        }

        LastStep = step;
        _currentPeak = Math.Max(_currentPeak, probability);
    }
}
