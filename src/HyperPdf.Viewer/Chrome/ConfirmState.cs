// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.ViewModels.Dialogs;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What a yes or no dialog shows and whether it was answered.</summary>
/// <param name="Request">What is asked.</param>
/// <param name="Answered">Whether the dialog was answered. Only the first answer counts.</param>
/// <param name="Agreed">Whether the answer was yes.</param>
[DebuggerDisplay("ConfirmState: {Request.Title}, answered {Answered}")]
internal sealed record ConfirmState(ConfirmRequest Request, bool Answered, bool Agreed);
