// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Archiving;

namespace HyperPdf.ViewModels.Output;

/// <summary>How a save is to be rewritten as PDF 2.0: the open document's writer, and what the person agreed may be dropped.</summary>
/// <param name="Writer">The open document's PDF 2.0 writer.</param>
/// <param name="Options">What the copy may drop.</param>
[DebuggerDisplay("Pdf20Upgrade: {Options}")]
internal sealed record Pdf20Upgrade(IPdf20CopyWriter Writer, Pdf20CopyOptions Options);
