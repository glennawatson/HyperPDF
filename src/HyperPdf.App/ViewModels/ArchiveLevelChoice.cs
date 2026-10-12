// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Archiving;

namespace HyperPdf.App.ViewModels;

/// <summary>A PDF/A level offered in the PDF/A copy window.</summary>
/// <param name="Level">The level.</param>
/// <param name="Name">The short name, such as PDF/A-2b.</param>
/// <param name="Description">What the level means, in plain words.</param>
[DebuggerDisplay("ArchiveLevelChoice: {Name}")]
public sealed record ArchiveLevelChoice(ArchiveLevel Level, string Name, string Description);
