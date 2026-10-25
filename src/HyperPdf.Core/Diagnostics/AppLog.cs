// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace HyperPdf.Core.Diagnostics;

/// <summary>
/// Where the app reports what went wrong and nobody is waiting to be told, such as a failed background save or a page that
/// could not be prepared. Everything goes through Microsoft.Extensions.Logging to the debug provider, so nothing is written
/// to the console, which would clutter the output of tests and of programs that start the app.
/// </summary>
public static partial class AppLog
{
    /// <summary>The logger for the app's reports; the factory is made once, lives as long as the process and is never disposed.</summary>
    private static readonly ILogger Logger = LoggerFactory.Create(static builder => builder.AddDebug()).CreateLogger("HyperPDF");

    /// <summary>Reports that something failed.</summary>
    /// <param name="exception">The failure.</param>
    /// <param name="message">What was being done.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(Exception exception, string message) => LogError(Logger, exception, message);

    /// <summary>Reports a failure that has nobody to tell, such as an error that ends a subscription.</summary>
    /// <param name="exception">The failure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(Exception exception) => LogError(Logger, exception, "A failure had nobody to tell.");

    /// <summary>Reports a problem the app carried on from.</summary>
    /// <param name="exception">The problem, or <see langword="null"/> when there is none to show.</param>
    /// <param name="message">What happened.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Warning(Exception? exception, string message) => LogWarning(Logger, exception, message);

    /// <summary>Reports something worth knowing that is not a problem.</summary>
    /// <param name="message">What happened.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Information(string message) => LogInformation(Logger, message);

    /// <summary>Writes an error.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="message">What was being done.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private static partial void LogError(ILogger logger, Exception exception, string message);

    /// <summary>Writes a warning.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The problem, or <see langword="null"/>.</param>
    /// <param name="message">What happened.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    private static partial void LogWarning(ILogger logger, Exception? exception, string message);

    /// <summary>Writes an information line.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">What happened.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "{Message}")]
    private static partial void LogInformation(ILogger logger, string message);
}
