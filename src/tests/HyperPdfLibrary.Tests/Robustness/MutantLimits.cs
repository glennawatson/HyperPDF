// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Bounded parsing and hang deadlines for one damaged document.</summary>
/// <param name="Parsing">Time until cooperative cancellation.</param>
/// <param name="Hard">Time until even an uncooperative worker is reported as hung.</param>
internal readonly record struct MutantLimits(TimeSpan Parsing, TimeSpan Hard);
