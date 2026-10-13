// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>The settings that change what recorded content shows, so a cached recording is only reused under the same ones.</summary>
/// <param name="LayerVersion">The optional content version, which changes when a layer is switched.</param>
/// <param name="ZoomClass">The layer zoom class, which decides layers with zoom limits.</param>
/// <param name="Printing">Whether optional content follows print usage.</param>
[DebuggerDisplay("RecordingMode: version {LayerVersion} zoom {ZoomClass} printing {Printing}")]
internal readonly record struct RecordingMode(int LayerVersion, int ZoomClass, bool Printing);
