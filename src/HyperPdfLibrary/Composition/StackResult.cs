// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>
/// Blocks laid out in a fixed width without page breaks: how tall they came to and the commands that draw them, with the top
/// left corner at the origin and the content going down from it.
/// </summary>
/// <param name="Height">The height of the stack in points.</param>
/// <param name="Commands">The commands, whose y values are zero or less.</param>
[DebuggerDisplay("StackResult: {Height} pt, {Commands.Count} commands")]
internal sealed record StackResult(float Height, List<DrawCommand> Commands)
{
    /// <summary>Moves the commands so the stack's top left corner is at a point.</summary>
    /// <param name="x">The x of the corner.</param>
    /// <param name="y">The y of the corner.</param>
    /// <returns>The moved commands.</returns>
    internal List<DrawCommand> PlaceAt(float x, float y)
    {
        var moved = new List<DrawCommand>(Commands.Count);
        foreach (var command in Commands)
        {
            moved.Add(command.Offset(x, y));
        }

        return moved;
    }
}
