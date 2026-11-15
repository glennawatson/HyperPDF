// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer;

/// <summary>One callback waiting to run on the window thread.</summary>
/// <param name="Callback">The callback.</param>
/// <param name="State">The state handed to the callback.</param>
[DebuggerDisplay("PostedWork")]
internal readonly record struct PostedWork(SendOrPostCallback Callback, object? State);
