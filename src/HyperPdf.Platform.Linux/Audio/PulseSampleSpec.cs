// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Linux.Audio;

/// <summary>Native <c>pa_sample_spec</c>: the sample format, rate and channel count.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct PulseSampleSpec : IEquatable<PulseSampleSpec>
{
    /// <summary>The sample format.</summary>
    private readonly int _format;

    /// <summary>The samples per second.</summary>
    private readonly uint _rate;

    /// <summary>The channel count.</summary>
    private readonly byte _channels;

    /// <summary>Initializes a new instance of the <see cref="PulseSampleSpec"/> struct.</summary>
    /// <param name="format">The sample format.</param>
    /// <param name="rate">The samples per second.</param>
    /// <param name="channels">The channel count.</param>
    internal PulseSampleSpec(int format, uint rate, byte channels)
    {
        _format = format;
        _rate = rate;
        _channels = channels;
    }

    /// <summary>Compares two specs.</summary>
    /// <param name="left">The left spec.</param>
    /// <param name="right">The right spec.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(PulseSampleSpec left, PulseSampleSpec right) => left.Equals(right);

    /// <summary>Compares two specs.</summary>
    /// <param name="left">The left spec.</param>
    /// <param name="right">The right spec.</param>
    /// <returns><see langword="true"/> when different.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(PulseSampleSpec left, PulseSampleSpec right) => !left.Equals(right);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool Equals(PulseSampleSpec other) => _format == other._format && _rate == other._rate && _channels == other._channels;

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is PulseSampleSpec other && Equals(other);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_format, _rate, _channels);
}
