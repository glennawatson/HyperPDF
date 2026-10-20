// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Media;

/// <summary>The FFmpeg programs the viewer uses to play sound and video. They are found on the computer and never shipped with the viewer.</summary>
/// <param name="Ffmpeg">The path of <c>ffmpeg</c>, which decodes video and captions.</param>
/// <param name="Ffplay">The path of <c>ffplay</c>, which plays sound.</param>
/// <param name="Ffprobe">The path of <c>ffprobe</c>, which reports a file's length and size.</param>
[DebuggerDisplay("MediaTools: {Ffmpeg}")]
public sealed record MediaTools(string Ffmpeg, string Ffplay, string Ffprobe);
