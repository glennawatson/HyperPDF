// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.Viewer.Icons;

namespace HyperPdf.Viewer.Chrome;

/// <summary>One button of a bar under the toolbar.</summary>
/// <param name="Icon">The icon drawn before the words, or <see cref="ChromeIcon.None"/> for the words alone.</param>
/// <param name="Entry">The words, the command and its parameter, and whether it can run now.</param>
[DebuggerDisplay("BarAction: {Entry}")]
internal sealed record BarAction(ChromeIcon Icon, MenuEntry Entry);
