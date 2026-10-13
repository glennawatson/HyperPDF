// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>A named destination: a name that links and bookmarks can use instead of a page and position.</summary>
/// <param name="Name">The name.</param>
/// <param name="Destination">Where the name leads; <see langword="null"/> when it names a page the document does not have.</param>
[DebuggerDisplay("PdfNamedDestination: {Name}")]
public sealed record PdfNamedDestination(string Name, PdfDestination? Destination);
