// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>Web capture information (<c>/SpiderInfo</c> and the <c>/IDS</c> and <c>/URLS</c> name trees).</summary>
/// <param name="Version">The web capture version (<c>/V</c>), or 0.</param>
/// <param name="ContentSets">The content sets, each once.</param>
/// <param name="IdEntryCount">The entries in the <c>/IDS</c> name tree.</param>
/// <param name="UrlEntryCount">The entries in the <c>/URLS</c> name tree.</param>
[DebuggerDisplay("PdfWebCapture: {ContentSets.Length} content sets")]
public sealed record PdfWebCapture(double Version, PdfWebCaptureContentSet[] ContentSets, int IdEntryCount, int UrlEntryCount);
