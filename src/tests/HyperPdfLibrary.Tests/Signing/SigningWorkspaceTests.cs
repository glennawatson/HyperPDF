// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Editing;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>The private copy that the build attempts of one signature share.</summary>
public sealed class SigningWorkspaceTests
{
    /// <summary>An integer to store in an object.</summary>
    private const int Value = 7;

    /// <summary>Every attempt starts from the copy as it was and numbers its new objects the same way.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttemptsStartFromTheSameCopy()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        using var workspace = PdfSigningWorkspace.Open(document);
        var store = workspace.Document.Objects;

        int first;
        using (var attempt = workspace.BeginAttempt("First"))
        {
            first = attempt.Transaction.Add(PdfValue.FromInteger(Value)).Number;
        }

        var removed = StoreReading.GetObject(store, new(first, 0)).IsNull;
        int second;
        using (var attempt = workspace.BeginAttempt("Second"))
        {
            second = attempt.Transaction.Add(PdfValue.FromInteger(Value)).Number;
        }

        await Assert.That(removed).IsTrue();
        await Assert.That(second).IsEqualTo(first);
    }

    /// <summary>The copy is disposed once, however many times the workspace is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyIsDisposedOnce()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        var workspace = PdfSigningWorkspace.Open(document);

        workspace.Dispose();
        workspace.Dispose();

        await Assert.That(workspace.Document.IsDisposed).IsTrue();
        await Assert.That(document.IsDisposed).IsFalse();
    }
}
