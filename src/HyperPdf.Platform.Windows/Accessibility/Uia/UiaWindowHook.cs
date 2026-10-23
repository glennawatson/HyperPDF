// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Diagnostics;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>
/// Answers UI Automation's request for a window's root element with this app's own, by sitting in front of the window's procedure.
/// Any failure passes the request on to the window's own procedure, so a screen reader still gets the UI framework's elements.
/// </summary>
[DebuggerDisplay("UiaWindowHook: {_window}")]
internal sealed unsafe class UiaWindowHook : IDisposable
{
    /// <summary>The message a window gets when something asks for its accessible object.</summary>
    private const uint GetObjectMessage = 0x003D;

    /// <summary>The message a window gets as it is destroyed, last of all.</summary>
    private const uint NcDestroyMessage = 0x0082;

    /// <summary>The object identifier UI Automation sends to ask for the root provider (<c>UiaRootObjectId</c>).</summary>
    private const int UiaRootObjectId = -25;

    /// <summary>The number that tells this subclass from any other on the window's procedure.</summary>
    private const nuint SubclassId = 0x48595044;

    /// <summary>The window's number; the hook borrows the window and does not own it.</summary>
    private readonly long _window;

    /// <summary>The elements to answer with.</summary>
    private readonly UiaElements _elements;

    /// <summary>Called once the window has gone, so the owner can forget the hook.</summary>
    private readonly Action<long> _gone;

    /// <summary>Keeps the hook alive and gives the procedure a way back to it.</summary>
    private GCHandle _self;

    /// <summary>Set to 1 once the handle has been let go of.</summary>
    private int _released;

    /// <summary>Initializes a new instance of the <see cref="UiaWindowHook"/> class.</summary>
    /// <param name="window">The window's number.</param>
    /// <param name="elements">The elements to answer with.</param>
    /// <param name="gone">Called once the window has gone.</param>
    private UiaWindowHook(long window, UiaElements elements, Action<long> gone)
    {
        _window = window;
        _elements = elements;
        _gone = gone;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Volatile.Read(ref _released) != 0)
        {
            return;
        }

        _ = NativeMethods.UiaReturnRawElementProvider((nint)_window, 0, 0, 0);

        // Removing the subclass only works from the window's own thread. On any other it stays, with its handle, until the window is
        // destroyed and the hook lets go of itself.
        if (NativeMethods.RemoveWindowSubclass((nint)_window, &SubclassProcedure, SubclassId) != 0)
        {
            Release();
        }
    }

    /// <summary>Puts the hook in front of a window's procedure. Call it on the window's own thread.</summary>
    /// <param name="window">The window's number.</param>
    /// <param name="elements">The elements to answer with.</param>
    /// <param name="gone">Called once the window has gone.</param>
    /// <returns>The hook, or <see langword="null"/> when Windows refused it.</returns>
    internal static UiaWindowHook? Install(long window, UiaElements elements, Action<long> gone)
    {
        var hook = new UiaWindowHook(window, elements, gone);
        hook._self = GCHandle.Alloc(hook);
        if (NativeMethods.SetWindowSubclass((nint)window, &SubclassProcedure, SubclassId, (nuint)GCHandle.ToIntPtr(hook._self)) == 0)
        {
            hook._self.Free();
            return null;
        }

        // A screen reader that already asked the window for its elements holds the UI framework's; make it ask again.
        _ = NativeMethods.UiaReturnRawElementProvider((nint)window, 0, 0, 0);
        return hook;
    }

    /// <summary>The window procedure that sees each message before the window's own.</summary>
    /// <param name="window">The window.</param>
    /// <param name="message">The message.</param>
    /// <param name="wordParameter">The first parameter.</param>
    /// <param name="longParameter">The second parameter.</param>
    /// <param name="subclassId">The number the subclass was added with.</param>
    /// <param name="referenceData">The hook's handle.</param>
    /// <returns>The result of the message.</returns>
    [UnmanagedCallersOnly]
    private static nint SubclassProcedure(nint window, uint message, nuint wordParameter, nint longParameter, nuint subclassId, nuint referenceData)
    {
        try
        {
            var answer = Handle(message, wordParameter, longParameter, referenceData);
            if (answer != 0)
            {
                return answer;
            }
        }
        catch (Exception ex)
        {
            // A failure must not escape into Windows; the window's own procedure answers instead.
            AppLog.Error(ex, "Answering UI Automation for a window failed.");
        }

        return NativeMethods.DefSubclassProc(window, message, wordParameter, longParameter);
    }

    /// <summary>Handles a message this hook cares about.</summary>
    /// <param name="message">The message.</param>
    /// <param name="wordParameter">The first parameter.</param>
    /// <param name="longParameter">The second parameter.</param>
    /// <param name="referenceData">The hook's handle.</param>
    /// <returns>The result when the hook answered, or 0 to leave the message to the window's own procedure.</returns>
    private static nint Handle(uint message, nuint wordParameter, nint longParameter, nuint referenceData)
    {
        if (GCHandle.FromIntPtr((nint)referenceData).Target is not UiaWindowHook hook)
        {
            return 0;
        }

        if (message == NcDestroyMessage)
        {
            hook.Forget();
            return 0;
        }

        return message == GetObjectMessage && (int)longParameter == UiaRootObjectId ? hook.Answer(wordParameter, longParameter) : 0;
    }

    /// <summary>Answers a request for the window's root element.</summary>
    /// <param name="wordParameter">The message's first parameter.</param>
    /// <param name="longParameter">The message's second parameter.</param>
    /// <returns>The result to return from the message, or 0 when the tree has no element for the window.</returns>
    private nint Answer(nuint wordParameter, nint longParameter)
    {
        if (_elements.RootOfWindow(_window) is not { } root)
        {
            return 0;
        }

        var pointer = NativeMethods.ComPointerOf<IRawElementProviderSimple>(root);
        try
        {
            return NativeMethods.UiaReturnRawElementProvider((nint)_window, wordParameter, longParameter, pointer);
        }
        finally
        {
            _ = Marshal.Release(pointer);
        }
    }

    /// <summary>Takes the hook off a window that is being destroyed.</summary>
    private void Forget()
    {
        _ = NativeMethods.RemoveWindowSubclass((nint)_window, &SubclassProcedure, SubclassId);
        _gone(_window);
        Release();
    }

    /// <summary>Lets go of the handle that keeps the hook alive, once, however many threads end the hook.</summary>
    private void Release()
    {
        if (Interlocked.Exchange(ref _released, 1) == 0)
        {
            _self.Free();
        }
    }
}
