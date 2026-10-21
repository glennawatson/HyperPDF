// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks the arrow key steps.</summary>
public sealed class ScreenStepTests
{
    /// <summary>An arrow moves a whole step in its direction.</summary>
    /// <param name="key">The arrow.</param>
    /// <param name="x">The expected horizontal sign.</param>
    /// <param name="y">The expected vertical sign.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ViewerKey.Left, -1, 0)]
    [Arguments(ViewerKey.Right, 1, 0)]
    [Arguments(ViewerKey.Up, 0, -1)]
    [Arguments(ViewerKey.Down, 0, 1)]
    public async Task ArrowMovesAWholeStep(ViewerKey key, int x, int y)
    {
        var step = ScreenStep.FromArrow(key, ViewerModifiers.None);

        await Assert.That(step.X).IsEqualTo(x * SignatureMarkLayout.MoveStep);
        await Assert.That(step.Y).IsEqualTo(y * SignatureMarkLayout.MoveStep);
    }

    /// <summary>Shift makes the step fine.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShiftMakesAFineStep()
    {
        var step = ScreenStep.FromArrow(ViewerKey.Right, ViewerModifiers.Shift);

        await Assert.That(step.X).IsEqualTo(SignatureMarkLayout.FineMoveStep);
    }

    /// <summary>Other keys and other modifiers give no step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherKeysAndModifiersGiveNoStep()
    {
        await Assert.That(ScreenStep.FromArrow(ViewerKey.Enter, ViewerModifiers.None).IsEmpty).IsTrue();
        await Assert.That(ScreenStep.FromArrow(ViewerKey.Left, ViewerModifiers.Control).IsEmpty).IsTrue();
        await Assert.That(ScreenStep.FromArrow(ViewerKey.Left, ViewerModifiers.Shift | ViewerModifiers.Alt).IsEmpty).IsTrue();
    }
}
