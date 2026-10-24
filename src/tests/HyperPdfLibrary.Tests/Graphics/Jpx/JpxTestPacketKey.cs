// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>A packet's place: its layer, resolution, component and precinct, and the precinct's reference grid position.</summary>
/// <param name="Layer">The layer.</param>
/// <param name="Resolution">The resolution.</param>
/// <param name="Component">The component.</param>
/// <param name="Precinct">The precinct.</param>
/// <param name="X">The column where the decoder's position walk meets the precinct.</param>
/// <param name="Y">The row where the decoder's position walk meets the precinct.</param>
internal readonly record struct JpxTestPacketKey(int Layer, int Resolution, int Component, int Precinct, long X, long Y);
