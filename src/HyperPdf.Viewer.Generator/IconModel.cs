// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Generator;

/// <summary>One icon as read from its SVG file. Every member is a string, so two readings of an unchanged file are equal and nothing is generated again.</summary>
/// <param name="Name">The icon's name: the file's name without its extension.</param>
/// <param name="Summary">What the icon shows, from the file's title.</param>
/// <param name="Tone">The kind of action the icon stands for, from the file's data-tone attribute.</param>
/// <param name="Path">The outline as absolute M, L, C, A and Z commands.</param>
/// <param name="Error">What is wrong with the file, or empty.</param>
[DebuggerDisplay("IconModel: {Name}")]
internal sealed record IconModel(string Name, string Summary, string Tone, string Path, string Error);
