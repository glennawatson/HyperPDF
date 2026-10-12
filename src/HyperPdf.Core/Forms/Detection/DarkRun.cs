// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms.Detection;

/// <summary>A run of dark pixels along one row of an image.</summary>
/// <param name="Y">The row.</param>
/// <param name="Start">The first dark pixel.</param>
/// <param name="End">The pixel after the last dark one.</param>
[DebuggerDisplay("DarkRun: row {Y}, {Start} to {End}")]
internal readonly record struct DarkRun(int Y, int Start, int End)
{
    /// <summary>Gets the run's length in pixels.</summary>
    public int Length => End - Start;
}
