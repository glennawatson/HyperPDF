// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The commands the controls of the start view run.</summary>
/// <param name="Open">Asks for a file and opens it.</param>
/// <param name="OpenRecent">Opens a document opened lately, which is its parameter.</param>
/// <param name="Remove">Forgets a document opened lately, which is its parameter.</param>
/// <param name="Clear">Forgets every document opened lately.</param>
[DebuggerDisplay("StartActions")]
internal sealed record StartActions(ICommand Open, ICommand OpenRecent, ICommand Remove, ICommand Clear);
