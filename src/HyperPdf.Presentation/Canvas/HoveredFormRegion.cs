// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Forms.Detection;

namespace HyperPdf.Presentation.Canvas;

/// <summary>A printed-form region suggestion currently hovered by the pointer.</summary>
/// <param name="Page">The page index.</param>
/// <param name="Region">The form region.</param>
[DebuggerDisplay("HoveredFormRegion: page {Page}")]
public readonly record struct HoveredFormRegion(int Page, FormRegion Region);
