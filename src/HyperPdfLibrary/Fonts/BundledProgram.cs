// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts;

/// <summary>A parsed bundled font program and code maps shared within one font resource owner.</summary>
/// <param name="Program">The parsed program, which never changes.</param>
/// <param name="Maps">The code maps built for faces made from the program.</param>
internal sealed record BundledProgram(TrueTypeProgram Program, SubstituteMapStore Maps);
