// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Checks that the device-dependent ExtGState keys (ISO 32000-2 Table 53) reach the graphics state, that <c>q</c> and
/// <c>Q</c> restore them, and that values of the wrong type change nothing.
/// </summary>
public sealed class DeviceFunctionStateTests
{
    /// <summary>The flatness the test state sets.</summary>
    private const float TestFlatness = 7;

    /// <summary>The smoothness the test state sets.</summary>
    private const float TestSmoothness = 0.25F;

    /// <summary>The flatness the Odd state sets.</summary>
    private const float OddFlatness = 3;

    /// <summary>The length of the dashes the Dashed state sets.</summary>
    private const float DashOn = 3;

    /// <summary>The length of the gaps the Dashed state sets.</summary>
    private const float DashOff = 2;

    /// <summary>The value an inverting transfer gives for black.</summary>
    private const byte White = 255;

    /// <summary>The paths painted between state changes.</summary>
    private const string Paint = " 0 0 10 10 re f ";

    /// <summary>A function that inverts its input.</summary>
    private const string Invert = "<< /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >>";

    /// <summary>A function that leaves its input as it is.</summary>
    private const string Same = "<< /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >>";

    /// <summary>The dash lengths the Dashed state sets.</summary>
    private static readonly float[] DashLengths = [DashOn, DashOff];

    /// <summary>The graphics states the tests name.</summary>
    private static readonly string States =
        $"/ExtGState << /All << /FL {TestFlatness} /SM {TestSmoothness} /SA true /OP true /op false /OPM 1 /TR2 {Invert} "
        + "/HT << /Type /Halftone /HalftoneType 1 /Frequency 60 /Angle 45 /SpotFunction /Round >> "
        + "/BG2 << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >> /UCR2 << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 2 >> >> "
        + $"/TrOnly << /TR {Invert} >> /Tr2Wins << /TR {Invert} /TR2 /Default >> /Tr2First << /TR2 /Default /TR {Invert} >> "
        + $"/Ident << /TR /Identity >> /SameFunction << /TR2 {Same} >> /ArrayTr << /TR [{Invert} {Same} {Invert}] >> /ShortTr << /TR [{Invert}] >> "
        + "/Defaults << /HT /Default /BG /Default /UCR /Default /BG2 /Default /UCR2 /Default >> "
        + "/OldKeys << /BG << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 3 >> /UCR << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 4 >> >> "
        + "/Bad << /FL (x) /SM /x /SA /x /OP /x /op (y) /OPM 7 /TK /no /TR2 123 /HT 5 /BG2 true /UCR2 (s) >> "
        + "/BadOpm << /OPM /One >> /Dashed << /D [[3 2] 1] >> /Dashed2 << /D [[3 2] 1] >> /Solid << /D [[] 0] >> >>";

    /// <summary>Every key is read into the state, and <c>Q</c> puts back what was there.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryKeyReachesTheStateAndQRestoresIt()
    {
        var recorder = GraphicsStateRecorder.Run($"{Paint}q /All gs{Paint}Q{Paint}", States);

        var before = recorder.Paints[0];
        var inside = recorder.Paints[1];
        var after = recorder.Paints[2];
        await Assert.That(before.Flatness).IsEqualTo(1F);
        await Assert.That(before.Smoothness).IsEqualTo(0F);
        await Assert.That(before.StrokeAdjustment).IsFalse();
        await Assert.That(before.Transfer).IsNull();
        await Assert.That(before.Halftone).IsNull();
        await Assert.That(before.BlackGeneration).IsNull();
        await Assert.That(before.UndercolorRemoval).IsNull();
        await Assert.That(before.StrokeOverprint).IsFalse();
        await Assert.That(before.OverprintMode).IsEqualTo(0);
        await Assert.That(inside.Flatness).IsEqualTo(TestFlatness);
        await Assert.That(inside.Smoothness).IsEqualTo(TestSmoothness);
        await Assert.That(inside.StrokeAdjustment).IsTrue();
        await Assert.That(inside.StrokeOverprint).IsTrue();
        await Assert.That(inside.FillOverprint).IsFalse();
        await Assert.That(inside.OverprintMode).IsEqualTo(1);
        await Assert.That(inside.Transfer).IsNotNull();
        await Assert.That(inside.Transfer!.Apply(0)).IsEqualTo((uint)((White << 16) | (White << 8) | White));
        await Assert.That(inside.Halftone).IsNotNull();
        await Assert.That(inside.BlackGeneration).IsNotNull();
        await Assert.That(inside.UndercolorRemoval).IsNotNull();
        await Assert.That(after).IsEqualTo(before);
    }

    /// <summary>/TR2 beats /TR wherever it appears; a name clears the function; an identity function is no transfer at all.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Tr2BeatsTrAndNamesClearTheTransfer()
    {
        var recorder = GraphicsStateRecorder.Run($"/TrOnly gs{Paint}/Tr2Wins gs{Paint}/TrOnly gs/Tr2First gs{Paint}/TrOnly gs/Ident gs{Paint}/TrOnly gs/SameFunction gs{Paint}", States);

        await Assert.That(recorder.Paints[0].Transfer).IsNotNull();
        await Assert.That(recorder.Paints[1].Transfer).IsNull();
        await Assert.That(recorder.Paints[2].Transfer).IsNull();
        await Assert.That(recorder.Paints[3].Transfer).IsNull();
        await Assert.That(recorder.Paints[4].Transfer).IsNull();
    }

    /// <summary>An array of functions gives each channel its own table; an array of fewer than three is not usable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArrayTransferHasATablePerChannel()
    {
        var recorder = GraphicsStateRecorder.Run($"/ArrayTr gs{Paint}/ShortTr gs{Paint}", States);

        var transfer = recorder.Paints[0].Transfer!;
        await Assert.That(transfer.Red[0]).IsEqualTo(White);
        await Assert.That(transfer.Green[0]).IsEqualTo((byte)0);
        await Assert.That(transfer.Blue[0]).IsEqualTo(White);
        await Assert.That(transfer.IsUniform).IsFalse();
        await Assert.That(recorder.Paints[1].Transfer).IsSameReferenceAs(transfer);
    }

    /// <summary>/BG and /UCR set the functions, the 2 versions win, and /Default clears them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlackGenerationAndUndercolorRemovalFollowTheirVersions()
    {
        var recorder = GraphicsStateRecorder.Run($"/OldKeys gs{Paint}/All gs/OldKeys gs{Paint}/Defaults gs{Paint}", States);

        var old = recorder.Paints[0];
        var both = recorder.Paints[1];
        await Assert.That(old.BlackGeneration).IsNotNull();
        await Assert.That(old.UndercolorRemoval).IsNotNull();
        await Assert.That(both.BlackGeneration).IsNotNull();
        await Assert.That(both.UndercolorRemoval).IsNotNull();
        await Assert.That(recorder.Paints[2].BlackGeneration).IsNull();
        await Assert.That(recorder.Paints[2].UndercolorRemoval).IsNull();
        await Assert.That(recorder.Paints[2].Halftone).IsNull();
    }

    /// <summary>Values of the wrong type, an out of range mode and a short transfer array leave the state as it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedValuesAreIgnored()
    {
        var recorder = GraphicsStateRecorder.Run($"/All gs{Paint}/Bad gs{Paint}/BadOpm gs{Paint}/ShortTr gs{Paint}", States);

        await Assert.That(recorder.Paints[1]).IsEqualTo(recorder.Paints[0]);
        await Assert.That(recorder.Paints[2]).IsEqualTo(recorder.Paints[0]);
        await Assert.That(recorder.Paints[3].Transfer).IsSameReferenceAs(recorder.Paints[0].Transfer);
    }

    /// <summary>Unknown graphics state names and keys are skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownStatesAndKeysAreSkipped()
    {
        var recorder = GraphicsStateRecorder.Run($"/Missing gs{Paint}/Odd gs{Paint}", "/ExtGState << /Odd << /Unknown 5 /FL 3 >> >>");

        await Assert.That(recorder.Paints[0].Flatness).IsEqualTo(1F);
        await Assert.That(recorder.Paints[1].Flatness).IsEqualTo(OddFlatness);
    }

    /// <summary>A graphics state read again gives the same function table and dash lengths, so reading it allocates nothing new.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedStatesReuseTheirObjects()
    {
        var recorder = GraphicsStateRecorder.Run($"/All gs{Paint}q /All gs{Paint}Q/Dashed gs{Paint}/Dashed2 gs{Paint}/Dashed gs{Paint}/Solid gs{Paint}", States);

        await Assert.That(recorder.Paints[1].Transfer).IsSameReferenceAs(recorder.Paints[0].Transfer);
        await Assert.That(recorder.Paints[2].Dash.AsSpan().ToArray()).IsEquivalentTo(DashLengths);
        await Assert.That(recorder.Paints[2].DashPhase).IsEqualTo(1F);
        await Assert.That(recorder.Paints[4].Dash.Equals(recorder.Paints[2].Dash)).IsTrue();
        await Assert.That(recorder.Paints[5].Dash.IsEmpty).IsTrue();
    }
}
