// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>A move on screen, in points, so arrow keys follow the screen on a rotated page.</summary>
/// <param name="X">The move to the right.</param>
/// <param name="Y">The move down.</param>
[DebuggerDisplay("ScreenStep: ({X}, {Y})")]
public readonly record struct ScreenStep(float X, float Y)
{
    /// <summary>Gets a value indicating whether the step moves nowhere.</summary>
    public bool IsEmpty => X == 0 && Y == 0;

    /// <summary>Gets the step an arrow key makes: a fine step with Shift, and none for any other key or modifier.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns>The step, or an empty one.</returns>
    public static ScreenStep FromArrow(ViewerKey key, ViewerModifiers modifiers)
    {
        if ((modifiers & ~ViewerModifiers.Shift) != 0)
        {
            return default;
        }

        var step = (modifiers & ViewerModifiers.Shift) != 0 ? SignatureMarkLayout.FineMoveStep : SignatureMarkLayout.MoveStep;
        return key switch
        {
            ViewerKey.Left => new(-step, 0F),
            ViewerKey.Right => new(step, 0F),
            ViewerKey.Up => new(0F, -step),
            ViewerKey.Down => new(0F, step),
            _ => default,
        };
    }
}
