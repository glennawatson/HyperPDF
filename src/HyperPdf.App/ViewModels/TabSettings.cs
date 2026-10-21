// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
using HyperPdf.Core.Rendering;

namespace HyperPdf.App.ViewModels;

/// <summary>The settings that change how an open document draws: the tint over fillable form fields and the page sharpness.</summary>
/// <param name="Highlight">The tint over fillable form fields.</param>
/// <param name="Sharpness">The page sharpness choices.</param>
internal readonly record struct TabSettings(FormHighlight Highlight, RenderSharpness Sharpness);
