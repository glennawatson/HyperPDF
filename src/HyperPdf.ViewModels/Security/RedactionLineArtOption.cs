// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Redaction;

namespace HyperPdf.ViewModels.Security;

/// <summary>One choice for what happens to drawings under a mark.</summary>
/// <param name="Choice">The choice.</param>
/// <param name="Name">The name shown.</param>
/// <param name="Description">What it does, in plain words.</param>
[DebuggerDisplay("RedactionLineArtOption: {Name}")]
public sealed record RedactionLineArtOption(RedactionLineArtChoice Choice, string Name, string Description);
