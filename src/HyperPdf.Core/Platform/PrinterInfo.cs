// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Platform;

/// <summary>A printer the desktop knows about.</summary>
/// <param name="Name">The queue name used to print.</param>
/// <param name="DisplayName">The name shown to people.</param>
/// <param name="IsDefault">Whether it is the default printer.</param>
[DebuggerDisplay("PrinterInfo: {DisplayName}")]
public sealed record PrinterInfo(string Name, string DisplayName, bool IsDefault);
