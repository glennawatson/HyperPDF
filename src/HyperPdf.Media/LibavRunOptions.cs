// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Media;

namespace HyperPdf.Media;

/// <summary>Everything one run of a file needs.</summary>
/// <param name="Source">The open file.</param>
/// <param name="Audio">The sound decoder, or <see langword="null"/> for a file without sound.</param>
/// <param name="Video">The picture decoder, or <see langword="null"/> for a file without a picture.</param>
/// <param name="Sound">The sound output, or <see langword="null"/> when sound cannot be played.</param>
/// <param name="Clock">The clock the run plays by.</param>
/// <param name="Pump">Receives the pictures, or <see langword="null"/>.</param>
/// <param name="Shown">Called after each picture is shown.</param>
/// <param name="ReduceMotion">Whether to show a picture only once a second.</param>
/// <param name="Start">The position to start from, in seconds.</param>
[DebuggerDisplay("LibavRunOptions: from {Start}")]
internal sealed record LibavRunOptions(
    LibavSource Source,
    LibavAudioDecoder? Audio,
    LibavVideoDecoder? Video,
    IStreamingAudioOutput? Sound,
    PlaybackClock Clock,
    FramePump? Pump,
    Action Shown,
    bool ReduceMotion,
    double Start);
