// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Media;

/// <summary>One caption: the words shown for a stretch of the media.</summary>
/// <param name="Start">When the caption appears.</param>
/// <param name="End">When the caption goes away.</param>
/// <param name="Text">The words, on one or more lines.</param>
[DebuggerDisplay("CaptionCue: {Start} {Text}")]
public sealed record CaptionCue(TimeSpan Start, TimeSpan End, string Text);
