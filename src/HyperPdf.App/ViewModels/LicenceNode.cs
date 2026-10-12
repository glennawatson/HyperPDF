// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Licences;

namespace HyperPdf.App.ViewModels;

/// <summary>A row of the licence tree: a licence heading with its components below it, or one component.</summary>
/// <param name="Title">The text shown, such as "MIT (12)" or "Avalonia 12.0.0".</param>
/// <param name="Entry">The component, or <see langword="null"/> for a licence heading.</param>
/// <param name="Children">The component rows below a heading; empty for a component.</param>
[DebuggerDisplay("LicenceNode: {Title}")]
public sealed record LicenceNode(string Title, NoticeEntry? Entry, IReadOnlyList<LicenceNode> Children);
