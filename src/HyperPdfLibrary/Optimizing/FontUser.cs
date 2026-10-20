// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>A font that uses an embedded program, and the separate objects that hold its names.</summary>
/// <param name="Program">The number of the embedded program the font uses.</param>
/// <param name="Descendant">The object number of the descendant font of a Type 0 font, or zero when it is part of the font's own dictionary or there is none.</param>
/// <param name="Descriptor">The object number of the font descriptor, or zero when it is part of a dictionary.</param>
/// <param name="Name">The font's name without any subset tag, from its descriptor.</param>
[DebuggerDisplay("FontUser: program {Program}")]
internal readonly record struct FontUser(int Program, int Descendant, int Descriptor, string Name);
