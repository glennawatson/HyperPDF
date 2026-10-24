// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.MediaFoundation;

/// <summary>Where a transform puts what it outputs (<c>MFT_OUTPUT_DATA_BUFFER</c>). The transform writes the status and events through the pointer it is given.</summary>
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("OutputDataBuffer: stream {StreamId}, status {Status}")]
internal readonly unsafe record struct OutputDataBuffer
{
    /// <summary>The stream the output is for.</summary>
    private readonly uint _streamId;

    /// <summary>The sample the transform fills, or 0 for it to supply one.</summary>
    private readonly nint _sample;

    /// <summary>The flags about the output the transform sets.</summary>
    private readonly uint _status;

    /// <summary>The events the transform raised, or 0.</summary>
    private readonly nint _events;

    /// <summary>Initializes a new instance of the <see cref="OutputDataBuffer"/> struct that asks the transform to fill a sample.</summary>
    /// <param name="sample">The sample, which stays the caller's.</param>
    internal OutputDataBuffer(ComHandle sample)
    {
        _streamId = 0;
        _sample = (nint)sample.Pointer;
        _status = 0;
        _events = 0;
    }

    /// <summary>Gets the stream the output is for.</summary>
    internal uint StreamId => _streamId;

    /// <summary>Gets the flags the transform set about the output.</summary>
    internal uint Status => _status;

    /// <summary>Gets a value indicating whether the transform is to fill a sample the caller supplied.</summary>
    internal bool HasSample => _sample != 0;

    /// <summary>Lets go of the events the transform raised, if any.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleaseEvents() => Com.Release((void*)_events);
}
