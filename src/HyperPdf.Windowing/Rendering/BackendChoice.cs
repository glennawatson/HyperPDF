// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Windowing.Rendering;

/// <summary>The backend a head ended up with.</summary>
/// <param name="Backend">The running backend.</param>
/// <param name="FellBack">Whether the GPU was asked for but could not start.</param>
[DebuggerDisplay("BackendChoice {Backend.Kind}, fell back {FellBack}")]
public readonly record struct BackendChoice(IRenderBackend Backend, bool FellBack);
