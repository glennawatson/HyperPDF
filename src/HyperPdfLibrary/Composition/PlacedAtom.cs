// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>An atom of a line with the place it is drawn at.</summary>
/// <param name="Atom">The atom.</param>
/// <param name="X">The x where the atom starts on the page.</param>
/// <param name="Width">The width the atom takes, which for a space includes the share of a justified line and for a tab is the width it skips.</param>
[DebuggerDisplay("PlacedAtom: {Atom}, x {X}, width {Width}")]
internal readonly record struct PlacedAtom(Atom Atom, float X, float Width)
{
    /// <summary>Gets the x where the atom ends.</summary>
    internal float End => X + Width;
}
