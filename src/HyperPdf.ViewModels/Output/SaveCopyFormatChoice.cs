// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Output;

/// <summary>One format choice in the "Save a Copy" window, with its plain-English explanation.</summary>
/// <param name="Format">The format to write.</param>
/// <param name="Name">The short name shown on the choice.</param>
/// <param name="Description">What this format does and when to choose it.</param>
[DebuggerDisplay("{Name} ({Format})")]
public sealed record SaveCopyFormatChoice(SaveCopyFormat Format, string Name, string Description);
