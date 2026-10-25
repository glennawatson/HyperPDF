// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Output;

/// <summary>One choice of which pages to export.</summary>
/// <param name="Kind">The choice.</param>
/// <param name="Name">The name shown.</param>
[DebuggerDisplay("ExportPagesScopeChoice: {Name}")]
public sealed record ExportPagesScopeChoice(ExportPagesScopeKind Kind, string Name);
