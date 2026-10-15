// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>What the content scan counts on a page for the accessibility report.</summary>
/// <param name="UntaggedGraphics">The paths and images painted outside marked content with an id and outside artifacts.</param>
/// <param name="InvalidLanguages">The marked content property lists whose <c>/Lang</c> is not a language identifier.</param>
[DebuggerDisplay("PageAccessibilityCounts: {UntaggedGraphics} untagged, {InvalidLanguages} invalid languages")]
internal readonly record struct PageAccessibilityCounts(int UntaggedGraphics, int InvalidLanguages);
