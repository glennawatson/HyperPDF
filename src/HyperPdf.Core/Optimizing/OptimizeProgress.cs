// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Optimizing;

/// <summary>How far an optimisation has got.</summary>
/// <param name="Step">The current step.</param>
/// <param name="Completed">The items of the step done so far.</param>
/// <param name="Total">The items in the step; zero when unknown.</param>
[DebuggerDisplay("OptimizeProgress: {Step} {Completed}/{Total}")]
public readonly record struct OptimizeProgress(OptimizeStep Step, int Completed, int Total)
{
    /// <summary>Gets the share of the step done, from 0 to 1; 0 when the total is unknown.</summary>
    public double Fraction => Total > 0 ? Math.Clamp((double)Completed / Total, 0, 1) : 0;
}
