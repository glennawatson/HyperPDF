// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>A colorant a page paints, as the separation view lists it.</summary>
/// <param name="Name">The colorant's name: Cyan, Magenta, Yellow or Black for a process colorant, otherwise the name a Separation or DeviceN space gives it.</param>
/// <param name="Kind">The kind of colorant.</param>
[DebuggerDisplay("PdfColorant: {Name} ({Kind})")]
public sealed record PdfColorant(string Name, PdfColorantKind Kind);
