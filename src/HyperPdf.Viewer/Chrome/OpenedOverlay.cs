// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>A dialog that was opened, and the answer it will give.</summary>
/// <typeparam name="T">The kind of answer.</typeparam>
/// <param name="Overlay">The dialog as the window shows it.</param>
/// <param name="Answer">The answer: one value when the dialog closes, then the end.</param>
[DebuggerDisplay("OpenedOverlay: {Overlay.Kind}")]
internal sealed record OpenedOverlay<T>(Overlay Overlay, IObservable<T> Answer);
