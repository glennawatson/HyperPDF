// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Media;

/// <summary>A sound output for a computer that cannot play sound. It never plays anything, so media plays silently.</summary>
[DebuggerDisplay("NullStreamingAudioOutput")]
public sealed class NullStreamingAudioOutput : IStreamingAudioOutput
{
    /// <summary>Gets the one instance.</summary>
    public static NullStreamingAudioOutput Instance { get; } = new();

    /// <inheritdoc />
    public bool IsAvailable => false;

    /// <inheritdoc />
    public TimeSpan Latency => TimeSpan.Zero;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Start(int sampleRate, int channels) => false;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Write(ReadOnlySpan<float> samples, CancellationToken cancellationToken) => false;

    /// <inheritdoc />
    public void Flush()
    {
    }

    /// <inheritdoc />
    public void Close()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
