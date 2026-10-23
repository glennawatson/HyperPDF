// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that the engine runs a document's page and document additional actions through the safe action runner.</summary>
public sealed class FormEventActionTests
{
    /// <summary>Verifies that the will-close event runs the catalog's action, which the sample sets to the named action Close.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WillClose_RunsTheCatalogsAction()
    {
        using var input = new HyperPdfTestDocument(FormRuntimeSamples.Create(string.Empty));
        var host = new NamedActionHost();

        var result = await Actions(input).RunDocumentEventAsync(FormDocumentEvent.WillClose, host, CancellationToken.None);

        await Assert.That(result).IsEqualTo(FormActionResult.Ran);
        await Assert.That(host.Names.ToArray()).IsEquivalentTo(["Close"]);
    }

    /// <summary>Verifies that an event the document has no action for runs nothing.</summary>
    /// <param name="documentEvent">The event with no action.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(FormDocumentEvent.WillSave)]
    [Arguments(FormDocumentEvent.DidSave)]
    [Arguments(FormDocumentEvent.WillPrint)]
    [Arguments(FormDocumentEvent.DidPrint)]
    public async Task DocumentEventWithoutAnAction_RunsNothing(FormDocumentEvent documentEvent)
    {
        using var input = new HyperPdfTestDocument(FormRuntimeSamples.Create(string.Empty));
        var host = new NamedActionHost();

        var result = await Actions(input).RunDocumentEventAsync(documentEvent, host, CancellationToken.None);

        await Assert.That(result).IsEqualTo(FormActionResult.None);
        await Assert.That(host.Names.Count).IsEqualTo(0);
    }

    /// <summary>Verifies that a page's open actions run, and that its close actions run nothing when the page has none.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PageActions_RunOpenAndCloseActionsSeparately()
    {
        using var input = new HyperPdfTestDocument(FormRuntimeSamples.Create(string.Empty));
        var host = new NamedActionHost();
        var actions = Actions(input);

        var closed = await actions.RunPageClosedAsync(0, host, CancellationToken.None);
        var opened = await actions.RunPageOpenedAsync(0, host, CancellationToken.None);

        await Assert.That(closed).IsEqualTo(FormActionResult.None);
        await Assert.That(opened).IsEqualTo(FormActionResult.Ran);
        await Assert.That(host.Names.ToArray()).IsEquivalentTo(["FirstPage"]);
    }

    /// <summary>Gets the document's form actions.</summary>
    /// <param name="input">The document.</param>
    /// <returns>The actions.</returns>
    private static IFormActions Actions(HyperPdfTestDocument input) => DocumentFeature<IFormActions>.Cast(input.Document)!;

    /// <summary>A host that records the named actions it is asked to run, and runs them.</summary>
    [DebuggerDisplay("NamedActionHost")]
    private sealed class NamedActionHost : IFormActionHost
    {
        /// <summary>Gets the names of the actions run, in order.</summary>
        internal ConcurrentQueue<string> Names { get; } = new();

        /// <inheritdoc/>
        public ValueTask<bool> RunNamedAsync(string name, int sourcePage, CancellationToken cancellationToken)
        {
            Names.Enqueue(name);
            return ValueTask.FromResult(true);
        }

        /// <inheritdoc/>
        public ValueTask<bool> OpenAsync(LinkTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        /// <inheritdoc/>
        public ValueTask<bool> SubmitAsync(FormSubmission submission, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        /// <inheritdoc/>
        public ValueTask<bool> ImportAsync(string? file, IFormDataImporter importer, CancellationToken cancellationToken) => ValueTask.FromResult(false);
    }
}
