// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Where the pixels of one interlace pass sit in the picture.</summary>
/// <param name="StartX">The picture column of the pass's first pixel.</param>
/// <param name="StartY">The picture row of the pass's first pixel.</param>
/// <param name="StepX">The columns between the pass's pixels.</param>
/// <param name="StepY">The rows between the pass's rows.</param>
[DebuggerDisplay("PassLayout: from ({StartX}, {StartY}) step ({StepX}, {StepY})")]
internal readonly record struct PassLayout(int StartX, int StartY, int StepX, int StepY);
