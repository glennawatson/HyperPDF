// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Diagnostics;

namespace HyperPdf.ViewModels.Services;

/// <summary>The one place the app reports a failure that has nobody to tell, such as an error that ends a subscription; it logs through <see cref="AppLog"/>.</summary>
public static class ErrorTrace
{
    /// <summary>Logs a failure as an error.</summary>
    /// <param name="error">The failure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(Exception error) => AppLog.Error(error);
}
