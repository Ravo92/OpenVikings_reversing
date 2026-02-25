namespace OpenVikings.Dexter.Struct
{
    internal readonly struct ThreadSlot
    {
        internal ThreadSlot(bool inUse, bool enabled, bool registered, long osThreadId, Action? entryPoint, Thread? thread)
        {
            InUse = inUse;
            Enabled = enabled;
            Registered = registered;
            OsThreadId = osThreadId;
            EntryPoint = entryPoint;
            Thread = thread;
        }

        internal bool InUse { get; }
        internal bool Enabled { get; }
        internal bool Registered { get; }
        internal long OsThreadId { get; }
        internal Action? EntryPoint { get; }
        internal Thread? Thread { get; }

        internal ThreadSlot WithThread(Thread thread)
        {
            return new ThreadSlot(InUse, Enabled, Registered, OsThreadId, EntryPoint, thread);
        }

        internal ThreadSlot WithEnabled(bool enabled)
        {
            return new ThreadSlot(InUse, enabled, Registered, OsThreadId, EntryPoint, Thread);
        }

        internal ThreadSlot WithRegistered(bool registered)
        {
            return new ThreadSlot(InUse, Enabled, registered, OsThreadId, EntryPoint, Thread);
        }

        internal ThreadSlot WithInUse(bool inUse)
        {
            return new ThreadSlot(inUse, Enabled, Registered, OsThreadId, EntryPoint, Thread);
        }

        internal ThreadSlot WithOsThreadId(long osThreadId)
        {
            return new ThreadSlot(InUse, Enabled, Registered, osThreadId, EntryPoint, Thread);
        }
    }
}
