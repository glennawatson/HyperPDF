// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Output;

/// <summary>Tells the failures a copy or sign window can show in its own error line from the ones that are bugs.</summary>
internal static class CopyFailure
{
    /// <summary>The message resource for a document closed under a copy window.</summary>
    private const string DocumentClosedKey = "The document was closed or reloaded while this window was open. Close this window and open it again from the menu.";

    /// <summary>Gets what the window says when its document was closed under it.</summary>
    internal static string DocumentClosedText => UiText.Get(DocumentClosedKey);

    /// <summary>Determines whether a failure is a closed document; the engine throws an <see cref="ObjectDisposedException"/>, a kind of <see cref="InvalidOperationException"/>.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> when the document is gone.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDocumentClosed(Exception exception) => exception is InvalidOperationException;

    /// <summary>Determines whether a failure while writing a copy belongs in the window's error line.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> when the window should show it rather than let it escape.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsExpected(Exception exception) =>
        exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException;

    /// <summary>Describes a failure in plain words.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The text to show.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Describe(Exception exception) => IsDocumentClosed(exception) ? DocumentClosedText : exception.Message;

    /// <summary>Retains an interface failure's key without interpreting exception text as a resource.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns>The message to insert into a localized error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LocalizedMessage Message(Exception exception) =>
        IsDocumentClosed(exception) ? LocalizedMessage.Get(UiText, DocumentClosedKey) : LocalizedMessage.Literal(exception.Message);
}
