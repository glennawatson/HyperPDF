// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.App.Tests;
using TUnit.Core.Executors;
using TUnit.Core.Interfaces;

[assembly: NotInParallel]
[assembly: TestExecutor<HeadlessTestExecutor>]

namespace HyperPdf.App.Tests;

/// <summary>Runs every test on the headless Avalonia UI thread.</summary>
public sealed class HeadlessTestExecutor : ITestExecutor
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask ExecuteTest(TestContext context, Func<ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExecuteAsync(action, context.Execution.CancellationToken);
    }

    /// <summary>Dispatches a test and stops waiting when its cancellation is requested.</summary>
    /// <param name="action">The test body.</param>
    /// <param name="cancellationToken">Cancels the dispatch and its caller's wait.</param>
    /// <returns>The asynchronous test dispatch.</returns>
    internal static async ValueTask ExecuteAsync(Func<ValueTask> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        // Dispatch does not cancel queued work promptly, and a running UI action may ignore its token.
        // Cancel the caller's wait as well; this cannot stop a blocked action on the shared UI thread.
        _ = await HeadlessSession.Instance.Dispatch(
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action().ConfigureAwait(false);
                return true;
            },
            cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
