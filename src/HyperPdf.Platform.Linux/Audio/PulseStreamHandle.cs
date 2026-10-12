// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Platform.Linux.Audio;

/// <summary>Owns a <c>pa_simple</c> playback stream.</summary>
internal sealed class PulseStreamHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="PulseStreamHandle"/> class.</summary>
    public PulseStreamHandle()
        : base(true)
    {
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        NativeMethods.PaSimpleFree(handle);
        return true;
    }
}
