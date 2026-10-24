// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>One entry of an edit list.</summary>
/// <param name="Segment">How long the edit lasts, in movie ticks.</param>
/// <param name="MediaTime">Where in the media the edit starts, in media ticks, or -1 for an edit that shows nothing.</param>
internal readonly record struct Mp4EditEntry(long Segment, long MediaTime);
