namespace OpenVikings.NC2Logic
{
    internal delegate bool LogicCallback(uint callbackType, uint a, uint b, uint c, uint d);

    internal sealed class CCallbackManager
    {
        // The engine uses callback IDs like 1, 0x14, 0x2B, etc.
        // We model them as uint keys.
        private readonly CallbackList[] _lists;

        internal CCallbackManager(int maxCallbackTypes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCallbackTypes);

            _lists = new CallbackList[maxCallbackTypes];

            int i;
            for (i = 0; i < _lists.Length; i++)
            {
                _lists[i] = new CallbackList();
            }
        }

        internal void RegisterCallback(uint callbackType, LogicCallback callback, uint userData, int priority)
        {
            // English: Mirrors NC2Logic::CCallbackManager::RegisterCallback

            int index = (int)(callbackType & 0xffffffffu);
            if ((uint)index >= (uint)_lists.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(callbackType));
            }

            CallbackList list = _lists[index];

            // English: If an entry with same callback + userData exists, remove it first.
            list.RemoveIfExists(callback, userData);

            CallbackNode node = new CallbackNode(priority, callback, userData);

            // English: Insert descending by priority (higher priority first).
            list.InsertSorted(node);
        }

        internal bool UnRegisterCallback(uint callbackType, LogicCallback callback, uint userData)
        {
            // English: Mirrors NC2Logic::CCallbackManager::UnRegisterCallback

            int index = (int)(callbackType & 0xffffffffu);
            if ((uint)index >= (uint)_lists.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(callbackType));
            }

            CallbackList list = _lists[index];
            return list.Remove(callback, userData);
        }

        internal void InvokeCallbacks(uint callbackType, uint a, uint b, uint c, uint d)
        {
            int index = (int)(callbackType & 0xffffffffu);
            if ((uint)index >= (uint)_lists.Length)
            {
                return;
            }

            CallbackNode node = _lists[index].Head;

            while (node != null)
            {
                LogicCallback callback = node.Callback;

                bool continuePropagation = callback(callbackType, a, b, c, d);

                if (!continuePropagation)
                {
                    break;
                }

                node = node.Next;
            }
        }

        private sealed class CallbackList
        {
            internal CallbackNode Head { get; private set; }
            internal CallbackNode Tail { get; private set; }

            internal CallbackList()
            {
                Head = null;
                Tail = null;
            }

            internal void RemoveIfExists(LogicCallback callback, uint userData)
            {
                CallbackNode node = Head;
                while (node != null)
                {
                    if (ReferenceEquals(node.Callback, callback) && node.UserData == userData)
                    {
                        RemoveNode(node);
                        return;
                    }

                    node = node.Next;
                }
            }

            internal bool Remove(LogicCallback callback, uint userData)
            {
                CallbackNode node = Head;
                while (node != null)
                {
                    if (ReferenceEquals(node.Callback, callback) && node.UserData == userData)
                    {
                        RemoveNode(node);
                        return true;
                    }

                    node = node.Next;
                }

                return false;
            }

            internal void InsertSorted(CallbackNode node)
            {
                if (Head == null)
                {
                    Head = node;
                    Tail = node;
                    return;
                }

                // English: If new node has higher priority than head, insert at front.
                if (Head.Priority < node.Priority)
                {
                    node.Next = Head;
                    Head.Prev = node;
                    Head = node;
                    return;
                }

                // English: Walk until we find a next node with lower priority.
                CallbackNode current = Head;
                while (true)
                {
                    CallbackNode next = current.Next;

                    if (next == null)
                    {
                        // Insert at end.
                        current.Next = node;
                        node.Prev = current;
                        Tail = node;
                        return;
                    }

                    if (node.Priority > next.Priority)
                    {
                        // Insert between current and next.
                        current.Next = node;
                        node.Prev = current;

                        node.Next = next;
                        next.Prev = node;
                        return;
                    }

                    current = next;
                }
            }

            private void RemoveNode(CallbackNode node)
            {
                CallbackNode prev = node.Prev;
                CallbackNode next = node.Next;

                if (prev == null)
                {
                    // Removing head
                    Head = next;
                }
                else
                {
                    prev.Next = next;
                }

                if (next == null)
                {
                    // Removing tail
                    Tail = prev;
                }
                else
                {
                    next.Prev = prev;
                }

                node.Prev = null;
                node.Next = null;
            }
        }

        private sealed class CallbackNode
        {
            internal int Priority { get; }
            internal LogicCallback Callback { get; }
            internal uint UserData { get; }

            internal CallbackNode Prev { get; set; }
            internal CallbackNode Next { get; set; }

            internal CallbackNode(int priority, LogicCallback callback, uint userData)
            {
                Priority = priority;
                Callback = callback ?? throw new ArgumentNullException(nameof(callback));
                UserData = userData;

                Prev = null;
                Next = null;
            }
        }
    }
}