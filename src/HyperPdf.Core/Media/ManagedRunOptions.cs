// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperCodecs;

namespace HyperPdf.Core.Media;

/// <summary>What one run of a file needs: the file, the sound and pictures it plays, and the clock it plays by.</summary>
/// <param name="Container">The file.</param>
/// <param name="Sound">What plays the sound and keeps its clock, or <see langword="null"/> for a run without sound.</param>
/// <param name="Picture">What shows the pictures, or <see langword="null"/> for a run without them.</param>
/// <param name="Clock">The clock to play by.</param>
/// <param name="ReduceMotion">Whether to show a picture only once a second.</param>
/// <param name="Start">The position to start from, in seconds.</param>
[DebuggerDisplay("ManagedRunOptions: from {Start}")]
internal sealed record ManagedRunOptions(MediaContainer Container, ManagedSound? Sound, ManagedPicture? Picture, PlaybackClock Clock, bool ReduceMotion, double Start);
