// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>What the report counted on one page.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="AnnotationCount">The annotations a reader can reach: not pop-ups and not hidden.</param>
/// <param name="TabsFollowStructure">Whether the page's <c>/Tabs</c> is <c>S</c>.</param>
/// <param name="UntaggedGlyphs">The visible glyphs drawn outside marked content and outside artifacts.</param>
/// <param name="ArtifactGlyphs">The visible glyphs drawn as artifacts.</param>
/// <param name="UntaggedGraphics">The paths and images painted outside marked content and outside artifacts.</param>
[DebuggerDisplay("PdfAccessibilityPage: page {PageIndex}, untagged {UntaggedGlyphs} glyphs and {UntaggedGraphics} graphics, artifacts {ArtifactGlyphs}")]
public readonly record struct PdfAccessibilityPage(int PageIndex, int AnnotationCount, bool TabsFollowStructure, int UntaggedGlyphs, int ArtifactGlyphs, int UntaggedGraphics);
