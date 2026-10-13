// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Reading;

namespace HyperPdf.App.Tests;

/// <summary>A text layout source that holds a page's characters at the engine's layout gate while one is armed.</summary>
/// <param name="inner">The real layout source.</param>
/// <param name="document">The document the feature belongs to.</param>
/// <param name="engine">The engine that owns the gate.</param>
internal sealed class HoldingLayout(ITextLayoutSource inner, IDocument document, PausingEngine engine) : ITextLayoutSource
{
    /// <summary>The longest a held read waits for its release on a pool thread; the call cannot be awaited.</summary>
    private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public IDocument Document => document;

    /// <inheritdoc/>
    public void GetCharacters(int pageIndex, List<PageCharacter> output)
    {
        if (engine.Gate is { Point: PausingEngine.PausePoint.Layout } gate && gate.Page == pageIndex)
        {
            _ = gate.Reached.TrySetResult();

            // The call is synchronous, so the hold on a pool thread polls the release with a bound.
            _ = SpinWait.SpinUntil(() => gate.Released.Task.IsCompleted, HoldLimit);
        }

        inner.GetCharacters(pageIndex, output);
    }
}
