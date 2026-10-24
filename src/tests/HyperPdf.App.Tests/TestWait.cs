// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.App.Tests;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Waits for a condition while pumping the headless dispatcher, for the shared services builder.</summary>
internal static class TestWait
{
    /// <summary>Waits until <paramref name="condition"/> is true.</summary>
    /// <param name="condition">The condition, which must come to hold.</param>
    /// <param name="state">Describes the state the condition reads, for the timeout message, or <see langword="null"/>.</param>
    /// <param name="file">The calling file, filled in by the compiler.</param>
    /// <param name="line">The calling line, filled in by the compiler.</param>
    /// <returns><see langword="true"/>, once the condition holds.</returns>
    internal static Task<bool> UntilAsync(Func<bool> condition, Func<string>? state = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
        UiWait.UntilAsync(condition, state, file, line);
}
