// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Platform;

/// <summary>What happened to a print job.</summary>
/// <param name="Sent">Whether the printer's queue accepted the job.</param>
/// <param name="Detail">Why it was not sent, or an empty string.</param>
[DebuggerDisplay("PrintOutcome: Sent={Sent} {Detail}")]
public readonly record struct PrintOutcome(bool Sent, string Detail);
