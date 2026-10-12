// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Core.Text.Layout;

namespace HyperPdf.Engine;

/// <summary>The font a text box is written in: an installed font to embed, or a built in one, and the styles drawn in.</summary>
/// <param name="Shaper">Shapes the text.</param>
/// <param name="Program">The installed font to embed, or <see langword="null"/> for a built in font.</param>
/// <param name="Standard">The built in font's shaper, or <see langword="null"/> for an installed font.</param>
/// <param name="FakeBold">Whether bold is drawn by thickening the outlines.</param>
/// <param name="FakeItalic">Whether italic is drawn by slanting.</param>
[DebuggerDisplay("TextBoxFont: {Program?.Face.Family ?? Standard?.Font.ToString()}")]
internal sealed record TextBoxFont(ITextShaper Shaper, FontProgram? Program, StandardTextShaper? Standard, bool FakeBold, bool FakeItalic);
