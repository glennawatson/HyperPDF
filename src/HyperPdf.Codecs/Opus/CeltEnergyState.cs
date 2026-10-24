// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The band energies a CELT decoder keeps from frame to frame: the current ones, which predict the next frame, and the history that conceals a collapse of energy.</summary>
internal sealed class CeltEnergyState
{
    /// <summary>The log energy that stands for silence.</summary>
    internal const float SilentEnergy = -28F;

    /// <summary>The numbers kept for each: both channels of every band.</summary>
    private const int Entries = 2 * CeltEnergy.Bands;

    /// <summary>Gets the log energy of every band of both channels from the frame decoded last, which the next frame is predicted from.</summary>
    internal float[] Current { get; } = new float[Entries];

    /// <summary>Gets the log energy of the frame before the last, kept to detect an energy collapse.</summary>
    internal float[] Previous { get; } = CreateSilent();

    /// <summary>Gets the log energy of the frame two before the last.</summary>
    internal float[] BeforePrevious { get; } = CreateSilent();

    /// <summary>Gets the lowest recent log energy of every band, which a lost frame is concealed with.</summary>
    internal float[] Background { get; } = new float[Entries];

    /// <summary>Puts the energies back to their state before any frame was decoded.</summary>
    internal void Reset()
    {
        Array.Clear(Current);
        Array.Clear(Background);
        Array.Fill(Previous, SilentEnergy);
        Array.Fill(BeforePrevious, SilentEnergy);
    }

    /// <summary>Makes an array of silent energies.</summary>
    /// <returns>The array.</returns>
    private static float[] CreateSilent()
    {
        var energies = new float[Entries];
        Array.Fill(energies, SilentEnergy);
        return energies;
    }
}
