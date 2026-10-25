// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>The Type 2 transient array starts at zero, whatever the pooled scratch held before.</summary>
public sealed class Type2TransientArrayTests
{
    /// <summary>The operator number of get.</summary>
    private const int Get = 21;

    /// <summary>The operator number of rmoveto.</summary>
    private const int RMoveTo = 21;

    /// <summary>The operator number of rlineto.</summary>
    private const int RLineTo = 5;

    /// <summary>The operator number of endchar.</summary>
    private const int EndChar = 14;

    /// <summary>A value an earlier glyph could have left in the pooled scratch.</summary>
    private const float Leftover = 123F;

    /// <summary>Reading a slot no put wrote gives zero, not a value left in the scratch.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetOfAnUnwrittenSlotReadsZero()
    {
        var charstring = new CharstringWriter(false).Numbers(0).EscapeOp(Get).Numbers(0).Op(RMoveTo).Numbers(1, 1).Op(RLineTo).Op(EndChar).ToArray();
        var scratch = new float[CharstringLimits.ScratchSize];
        Array.Fill(scratch, Leftover);
        var recorder = new OutlineRecorder();
        var interpreter = new Type2Interpreter(default, default, new CffPrivate(default, 0, 0), false, scratch);

        interpreter.Run(charstring, 0, 0, ref recorder);

        await Assert.That(recorder.Commands[0]).IsEqualTo("M 0 0");
    }
}
