// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Linux.Audio;

/// <summary>Source generated entry points of PulseAudio's simple API (libpulse-simple, LGPL, loaded at run time), which PipeWire also provides.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The native library name, resolved through <see cref="Core.Platform.NativeLibraries"/>.</summary>
    internal const string Library = "pulse-simple";

    /// <summary>Native <c>pa_simple_new</c>.</summary>
    /// <param name="server">The server, or null for the default.</param>
    /// <param name="name">The UTF-8 application name.</param>
    /// <param name="direction">1 for playback.</param>
    /// <param name="device">The device, or null for the default.</param>
    /// <param name="streamName">The UTF-8 stream description.</param>
    /// <param name="spec">The sample format.</param>
    /// <param name="channelMap">The channel map, or null.</param>
    /// <param name="attributes">The buffer attributes, or null for defaults.</param>
    /// <param name="error">Receives the error code.</param>
    /// <returns>The stream.</returns>
    [LibraryImport(Library, EntryPoint = "pa_simple_new")]
    internal static partial PulseStreamHandle PaSimpleNew(
        byte* server,
        byte* name,
        int direction,
        byte* device,
        byte* streamName,
        PulseSampleSpec* spec,
        void* channelMap,
        PulseBufferAttributes* attributes,
        out int error);

    /// <summary>Native <c>pa_simple_write</c>.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="data">The samples.</param>
    /// <param name="bytes">The byte count.</param>
    /// <param name="error">Receives the error code.</param>
    /// <returns>0 on success.</returns>
    [LibraryImport(Library, EntryPoint = "pa_simple_write")]
    internal static partial int PaSimpleWrite(PulseStreamHandle stream, void* data, nuint bytes, out int error);

    /// <summary>Native <c>pa_simple_get_latency</c>.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="error">Receives the error code.</param>
    /// <returns>The microseconds of audio still to play.</returns>
    [LibraryImport(Library, EntryPoint = "pa_simple_get_latency")]
    internal static partial ulong PaSimpleGetLatency(PulseStreamHandle stream, out int error);

    /// <summary>Native <c>pa_simple_flush</c>.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="error">Receives the error code.</param>
    /// <returns>0 on success.</returns>
    [LibraryImport(Library, EntryPoint = "pa_simple_flush")]
    internal static partial int PaSimpleFlush(PulseStreamHandle stream, out int error);

    /// <summary>Native <c>pa_simple_free</c>.</summary>
    /// <param name="stream">The stream.</param>
    [LibraryImport(Library, EntryPoint = "pa_simple_free")]
    internal static partial void PaSimpleFree(nint stream);
}
