// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Speech;

/// <summary>A sound output for desktops with no way to play sound; Read Aloud explains that sound is unavailable.</summary>
[DebuggerDisplay("NullAudioOutput: No sound output")]
public sealed class NullAudioOutput : IAudioOutput
{
    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PlayAsync(SpeechAudio audio, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to release.
    }
}
