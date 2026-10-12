// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Archiving;

/// <summary>Something a PDF/A copy changed, or a reason it could not be made.</summary>
/// <param name="Description">What happened, in plain words.</param>
/// <param name="Clause">The clause of the standard behind it, such as 6.2.11.4.1; empty when none applies.</param>
[DebuggerDisplay("ArchiveNote: {Clause} {Description}")]
public sealed record ArchiveNote(string Description, string Clause);
