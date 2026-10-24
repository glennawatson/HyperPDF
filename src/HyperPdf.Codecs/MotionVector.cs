// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>A motion vector in quarter samples.</summary>
/// <param name="X">The horizontal part.</param>
/// <param name="Y">The vertical part.</param>
internal readonly record struct MotionVector(int X, int Y);
