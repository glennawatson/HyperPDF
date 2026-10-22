// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's GraphicsState operations over its owned state.</summary>
internal static class ContentGraphicsState
{
    /// <summary>The most dash lengths kept.</summary>
    internal const int MaxDashEntries = 64;

    /// <summary>The operand index of the third matrix entry.</summary>
    internal const int MatrixC = 2;

    /// <summary>The operand index of the fifth matrix entry.</summary>
    internal const int MatrixE = 4;

    /// <summary>The operand index of the sixth matrix entry.</summary>
    internal const int MatrixF = 5;

    /// <summary>The operand index of the fourth matrix entry.</summary>
    internal const int MatrixD = 3;

    /// <summary>Handles <c>q</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSave(ContentInterpreter self, ref ContentReader reader) => ContentGraphicsState.SaveState(self);

    /// <summary>Handles <c>Q</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpRestore(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.Stack.Count > self.StackFloor)
        {
            ContentGraphicsState.RestoreState(self);
        }
    }

    /// <summary>Handles <c>cm</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpConcatMatrix(ContentInterpreter self, ref ContentReader reader)
    {
        var matrix = new Matrix3x2(
            reader.Number(0),
            reader.Number(1),
            reader.Number(ContentGraphicsState.MatrixC),
            reader.Number(ContentGraphicsState.MatrixD),
            reader.Number(ContentGraphicsState.MatrixE),
            reader.Number(ContentGraphicsState.MatrixF));
        self.State.Ctm = matrix * self.State.Ctm;
    }

    /// <summary>Handles <c>w</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetLineWidth(ContentInterpreter self, ref ContentReader reader) => self.State.LineWidth = Math.Max(0, reader.Number(0));

    /// <summary>Handles <c>J</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetLineCap(ContentInterpreter self, ref ContentReader reader) => self.State.LineCap = (int)reader.Number(0);

    /// <summary>Handles <c>j</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetLineJoin(ContentInterpreter self, ref ContentReader reader) => self.State.LineJoin = (int)reader.Number(0);

    /// <summary>Handles <c>M</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetMiterLimit(ContentInterpreter self, ref ContentReader reader) => self.State.MiterLimit = reader.Number(0);

    /// <summary>Handles <c>d</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetDash(ContentInterpreter self, ref ContentReader reader)
    {
        var array = reader.Operand(0);
        if (array.Kind != ContentOperandKind.Array)
        {
            return;
        }

        ContentGraphicsState.SetDash(self, reader.Body(array), reader.Number(1));
    }

    /// <summary>Handles <c>gs</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetGraphicsState(ContentInterpreter self, ref ContentReader reader)
    {
        var name = reader.Operand(0).Name;
        if (ContentExecution.FindResource(self, Objects.KnownName.ExtGState, name).AsDictionary() is { } dictionary)
        {
            ContentExtendedGraphics.ApplyExtGState(self, dictionary);
        }
    }

    /// <summary>Saves the graphics state and the device's clip.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    internal static void SaveState(ContentInterpreter self)
    {
        self.Stack.Add(self.State);
        self.Device.Save();
    }

    /// <summary>Restores the most recently saved graphics state and device clip.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    internal static void RestoreState(ContentInterpreter self)
    {
        var last = self.Stack.Count - 1;
        self.State = self.Stack[last];
        self.Stack.RemoveAt(last);
        self.Device.Restore();
    }

    /// <summary>Sets the dash pattern from the bytes of a dash array.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "body">The bytes between the brackets.</param>
    /// <param name = "phase">The dash phase.</param>
    internal static void SetDash(ContentInterpreter self, ReadOnlySpan<byte> body, float phase)
    {
        Span<float> lengths = stackalloc float[ContentGraphicsState.MaxDashEntries];
        var count = ContentOperands.ReadNumbers(body, lengths);
        var total = 0F;
        for (var i = 0; i < count; i++)
        {
            total += Math.Abs(lengths[i]);
        }

        // Content often sets the same dash before every stroke; the array is never changed, so the one in place is kept.
        if (count == 0 || total <= 0)
        {
            self.State.Dash = default;
        }
        else if (!self.State.Dash.Span.SequenceEqual(lengths[..count]))
        {
            self.State.Dash = lengths[..count].ToArray();
        }

        self.State.DashPhase = phase;
    }
}
