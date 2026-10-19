// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Sends form data to a server. The library reads it; sending is left to the host.</summary>
/// <param name="Url">The target URL, or null.</param>
/// <param name="Fields">The field names to include (or exclude, when bit 1 of <paramref name="Flags"/> is set); empty means all fields.</param>
/// <param name="Flags">The <c>/Flags</c> value.</param>
[DebuggerDisplay("SubmitFormAction: {Url}")]
public sealed record SubmitFormAction(string? Url, string[] Fields, int Flags);
