using System;
using System.Collections.Generic;

namespace JLVisionLib;

/// <summary>
/// Tracks one synchronous native procedure and the managed arrays pinned for its inputs.
/// Generated operator wrappers use this guard so exceptions cannot strand a procedure.
/// </summary>
internal sealed class JlNativeProcedureScope : IDisposable
{
    [ThreadStatic]
    private static JlNativeProcedureScope current;

    private readonly JlNativeProcedureScope previous;
    private readonly IntPtr procedure;
    private readonly Dictionary<JlTupleImplementation, int> pins = new Dictionary<JlTupleImplementation, int>();
    private bool completed;
    private bool disposed;

    private JlNativeProcedureScope(IntPtr procedure)
    {
        this.procedure = procedure;
        previous = current;
        current = this;
    }

    internal static JlNativeProcedureScope Begin(IntPtr procedure)
    {
        if (procedure == IntPtr.Zero)
        {
            throw new ArgumentException("A native procedure handle is required.", nameof(procedure));
        }
        return new JlNativeProcedureScope(procedure);
    }

    internal static void NotePin(JlTupleImplementation tuple)
    {
        if (current == null)
        {
            return;
        }
        current.pins.TryGetValue(tuple, out int count);
        current.pins[tuple] = checked(count + 1);
    }

    internal static void NoteUnpin(JlTupleImplementation tuple)
    {
        for (JlNativeProcedureScope scope = current; scope != null; scope = scope.previous)
        {
            if (scope.pins.TryGetValue(tuple, out int count) && count > 0)
            {
                if (count == 1)
                    scope.pins.Remove(tuple);
                else
                    scope.pins[tuple] = count - 1;
                return;
            }
        }
    }

    /// <summary>Destroy the procedure once; any native status exception still propagates.</summary>
    internal void Complete(int result)
    {
        if (completed || disposed)
        {
            throw new InvalidOperationException("The native procedure has already completed.");
        }

        // PostCall performs destruction even when native IO cleanup fails.
        // Mark complete first, since PostCall may throw after destroying the handle.
        completed = true;
        JlNativeApi.PostCall(procedure, result);
    }

    internal static void CompleteFailedCall(IntPtr procedure, int result)
    {
        for (JlNativeProcedureScope scope = current; scope != null; scope = scope.previous)
        {
            if (scope.procedure == procedure && !scope.completed && !scope.disposed)
            {
                scope.Complete(result);
                return;
            }
        }
        JlNativeApi.PostCall(procedure, result);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;

        try
        {
            if (!completed)
            {
                // This is only the exceptional path. Never mask the original
                // managed exception with a secondary native cleanup failure.
                try
                {
                    JlNativeApi.HLIClearAllIOCT(procedure);
                }
                catch
                {
                }
                try
                {
                    JlNativeApi.DestroyProcedure(procedure, JlNativeApi.Jl_MSG_FAIL);
                }
                catch
                {
                }
            }
        }
        finally
        {
            // Destroy the native procedure before unpinning any arrays used by it.
            foreach (KeyValuePair<JlTupleImplementation, int> pin in new List<KeyValuePair<JlTupleImplementation, int>>(pins))
            {
                for (int i = 0; i < pin.Value; i++)
                {
                    try
                    {
                        pin.Key.UnpinTuple();
                    }
                    catch
                    {
                        // Best effort during an already exceptional path.
                    }
                }
            }
            pins.Clear();
            current = previous;
        }
    }
}
