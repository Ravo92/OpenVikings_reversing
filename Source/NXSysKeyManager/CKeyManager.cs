using OpenVikings.Dexter;
using OpenVikings.NXSys;

namespace OpenVikings.NXSysKeyManager
{
    internal sealed class CKeyManager
    {
        internal static CKeyManager? sTheObjectPtr;

        private readonly DexterOS _dexterOs;
        private readonly LinkedList<SKeyMessage> _queue;
        private uint _lastPushTimeMs;

        internal CKeyManager(DexterOS dexterOs)
        {
            _dexterOs = dexterOs;
            _queue = new LinkedList<SKeyMessage>();
            sTheObjectPtr = this;

            // Equivalent to MemorySet(this, 0, 8) for the non-reference fields that matter here.
            _lastPushTimeMs = 0;
        }

        // decompile: *(int*)(this + 0x10)
        private int _pendingMessageCount;

        internal bool HasMessages
        {
            get { return _pendingMessageCount != 0; }
        }

        internal void Dispose()
        {
            _queue.Clear();
            sTheObjectPtr = null;
            _lastPushTimeMs = 0;
        }

        internal void PushKey(byte param1, byte param2, bool param3, short param4)
        {
            int modifierBits = 0;

            if (_dexterOs.KeyState(0x6C) || _dexterOs.KeyState(0x6D) || _dexterOs.KeyState(0x08))
            {
                modifierBits += 1;
            }

            if (_dexterOs.KeyState(0x6E) || _dexterOs.KeyState(0x6F) || _dexterOs.KeyState(0x09))
            {
                modifierBits += 2;
            }

            SKeyMessage message = new(key0: param2, key1: param3, modifiers: modifierBits, extra: param4);

            _queue.AddLast(message);
            _pendingMessageCount = _queue.Count;
            _lastPushTimeMs = NXSysTime.XWS_Time_GetMilliSeconds();
        }

        internal void PushKeyBackAgain(in SKeyMessage message)
        {
            _queue.AddFirst(message);
            _pendingMessageCount = _queue.Count;
        }

        internal bool GetKey(out SKeyMessage message)
        {
            if (_queue.Count == 0)
            {
                message = default;
                _pendingMessageCount = 0;
                return false;
            }

            LinkedListNode<SKeyMessage> node = _queue.First!;
            message = node.Value;
            _queue.RemoveFirst();
            _pendingMessageCount = _queue.Count;

            return message.ExtraLowByte != 0;
        }

        internal byte GetKeyboardMessage(out SKeyMessage message)
        {
            if (_queue.Count == 0)
            {
                message = default;
                _pendingMessageCount = 0;
                return 0;
            }

            LinkedListNode<SKeyMessage> node = _queue.First!;
            message = node.Value;
            _queue.RemoveFirst();
            _pendingMessageCount = _queue.Count;

            // 1 = down, 2 = up (decompile: (extraLow==0)+1)
            return message.ExtraLowByte == 0 ? (byte)2 : (byte)1;
        }

        internal bool IsKeyInList(byte param1, byte param2)
        {
            if (_queue.Count == 0)
            {
                return false;
            }

            LinkedListNode<SKeyMessage>? node = _queue.First;
            while (node != null)
            {
                SKeyMessage msg = node.Value;

                // Mirrors: if (puVar1[8] != 0) and matches either key slot when nonzero filter is provided
                if (msg.ExtraLowByte != 0)
                {
                    bool match0 = param1 != 0 && msg.Key0 == param1;
                    bool match1 = param2 != 0 && msg.Key1AsByte == param2;

                    if (match0 || match1)
                    {
                        return true;
                    }
                }

                node = node.Next;
            }

            return false;
        }

        internal void Init()
        {
            _queue.Clear();
            _lastPushTimeMs = 0;
        }
    }

    internal readonly struct SKeyMessage
    {
        internal readonly byte Key0;

        // Stored as bool for convenience; original layout uses 1 byte at offset 1.
        internal readonly bool Key1;

        // Stored at offset +4 in the original 0x0C allocation.
        internal readonly int Modifiers;

        // Original decomp writes only the low byte at offset +8, but the parameter is short.
        internal readonly short Extra;

        internal SKeyMessage(byte key0, bool key1, int modifiers, short extra)
        {
            Key0 = key0;
            Key1 = key1;
            Modifiers = modifiers;
            Extra = extra;
        }

        internal byte Key1AsByte => Key1 ? (byte)1 : (byte)0;

        internal byte ExtraLowByte => unchecked((byte)Extra);
    }
}