// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Archiving;

/// <summary>
/// A save whose file broke the PDF/A level it claims, so the engine fixed the saved file to meet the level. The open
/// document is not changed, so the next save fixes the file again.
/// </summary>
/// <param name="Level">The level the file claims and still meets.</param>
/// <param name="Repairs">What was fixed, in plain words.</param>
[DebuggerDisplay("ArchiveClaimRepair: {Level}, {Repairs.Count} fixes")]
public sealed record ArchiveClaimRepair(ArchiveLevel Level, IReadOnlyList<string> Repairs);
