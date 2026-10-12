// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>A hand-built file that claims PDF/A-1b, the rule it exercises and whether it breaks that rule.</summary>
/// <param name="Name">A short name for messages and file names.</param>
/// <param name="File">The file.</param>
/// <param name="Rule">The rule exercised.</param>
/// <param name="Fails">Whether the file breaks the rule.</param>
[DebuggerDisplay("PdfA1Case: {Name}")]
internal sealed record PdfA1Case(string Name, byte[] File, PdfARule Rule, bool Fails);
