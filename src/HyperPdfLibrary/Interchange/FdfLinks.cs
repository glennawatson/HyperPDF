// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Interchange;

/// <summary>The numbers of the objects an annotation of an FDF file refers to; 0 for none.</summary>
/// <param name="Parent">The annotation it answers.</param>
/// <param name="Stream">The embedded file it attaches.</param>
/// <param name="Popup">The pop-up window.</param>
[DebuggerDisplay("FdfLinks: parent {Parent} stream {Stream} popup {Popup}")]
internal readonly record struct FdfLinks(int Parent, int Stream, int Popup);
