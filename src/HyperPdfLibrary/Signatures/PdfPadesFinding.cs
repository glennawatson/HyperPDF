// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>The result of one PAdES check.</summary>
/// <param name="Level">The lowest level that needs this check to pass.</param>
/// <param name="Rule">A short stable name for the requirement.</param>
/// <param name="Passed">Whether the signature meets the requirement.</param>
/// <param name="Message">What was found, in plain words.</param>
[DebuggerDisplay("PdfPadesFinding: {Level} {Rule} passed {Passed}")]
public readonly record struct PdfPadesFinding(PdfPadesLevel Level, string Rule, bool Passed, string Message);
