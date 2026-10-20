// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Media;

/// <summary>What a sound or video file holds.</summary>
/// <param name="Duration">How long it plays, or zero when the file does not say.</param>
/// <param name="Width">The width in pixels of the first video stream, or zero.</param>
/// <param name="Height">The height in pixels of the first video stream, or zero.</param>
/// <param name="HasVideo">Whether the file has a picture.</param>
/// <param name="HasAudio">Whether the file has sound.</param>
/// <param name="HasCaptions">Whether the file has a caption stream.</param>
[DebuggerDisplay("MediaInfo: {Duration} {Width}x{Height}")]
public readonly record struct MediaInfo(TimeSpan Duration, int Width, int Height, bool HasVideo, bool HasAudio, bool HasCaptions);
