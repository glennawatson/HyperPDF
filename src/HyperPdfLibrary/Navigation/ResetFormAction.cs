// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Resets form fields to their defaults.</summary>
/// <param name="Fields">The field names to reset (or to keep, when bit 1 of <paramref name="Flags"/> is set); empty means all fields.</param>
/// <param name="Flags">The <c>/Flags</c> value.</param>
[DebuggerDisplay("ResetFormAction: {Fields.Length} fields")]
public sealed record ResetFormAction(string[] Fields, int Flags);
