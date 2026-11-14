// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Head.Linux;

/// <summary>One scroll step in notch units.</summary>
/// <param name="Sideways">The sideways amount, positive to the left.</param>
/// <param name="Upward">The up and down amount, positive upward.</param>
[DebuggerDisplay("WheelStep ({Sideways}, {Upward})")]
internal readonly record struct WheelStep(int Sideways, int Upward);
