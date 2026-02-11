using OpenVikings.Interfaces;

namespace OpenVikings.NMasterPropertyManager
{
    // NMasterPropertyManager::CPropertyManager
    internal sealed class CPropertyManager : IDisposable, IPropertyManager
    {
        internal static CPropertyManager? sTheObjectPtr;

        private readonly LinkedList<PropertyEntry> _properties;
        private readonly Dictionary<string, LinkedListNode<PropertyEntry>> _byKey;

        private bool _disposed;

        // NMasterPropertyManager::CPropertyManager::CPropertyManager()
        internal CPropertyManager()
        {
            sTheObjectPtr = this;

            _properties = new LinkedList<PropertyEntry>();
            _byKey = new Dictionary<string, LinkedListNode<PropertyEntry>>(StringComparer.Ordinal);
        }

        // NMasterPropertyManager::CPropertyManager::~CPropertyManager()
        ~CPropertyManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _byKey.Clear();
            _properties.Clear();

            sTheObjectPtr = null;
            _disposed = true;
        }

        // IPropertyManager.Exists(string)
        public bool Exists(string key)
        {
            if (key == null)
            {
                return false;
            }

            return _byKey.ContainsKey(key);
        }

        // IPropertyManager.Remove(string)
        public void Remove(string key)
        {
            if (key == null)
            {
                return;
            }

            RemoveCore(key);
        }

        // Mirrors: Property_DoesExists(char const*) const
        internal bool Property_DoesExists(string key)
        {
            return Exists(key);
        }

        // Mirrors: Property_Remove(char const*) -> returns 1/0 in the original
        internal bool Property_Remove(string key)
        {
            if (key == null)
            {
                return false;
            }

            return RemoveCore(key);
        }

        private bool RemoveCore(string key)
        {
            if (!_byKey.TryGetValue(key, out LinkedListNode<PropertyEntry>? node))
            {
                return false;
            }

            _byKey.Remove(key);
            _properties.Remove(node);
            return true;
        }

        // Mirrors: Property_GetPtr / l_Property_GetPtr
        internal PropertyEntry? Property_GetPtr(string key)
        {
            if (key == null)
            {
                return null;
            }

            if (_byKey.TryGetValue(key, out LinkedListNode<PropertyEntry>? node))
            {
                return node.Value;
            }

            return null;
        }

        // Mirrors: Property_Add(char const*, bool, int, char const*)
        // isInt: true => IntValue is valid, false => StringValue is valid (as in the original)
        internal void Property_Add(string key, bool isInt, int intValue, string? stringValue)
        {
            if (key == null)
            {
                return;
            }

            if (_byKey.TryGetValue(key, out LinkedListNode<PropertyEntry>? existingNode))
            {
                PropertyEntry existing = existingNode.Value;
                existing.IsInt = isInt;
                existing.IntValue = intValue;
                existing.StringValue = stringValue;
                return;
            }

            PropertyEntry entry = new(key, isInt, intValue, stringValue);
            LinkedListNode<PropertyEntry> node = _properties.AddLast(entry);
            _byKey.Add(key, node);
        }

        // Mirrors: Property_GetIntegerValue(char const*) const
        internal int Property_GetIntegerValue(string key)
        {
            PropertyEntry? entry = Property_GetPtr(key);
            if (entry == null)
            {
                return 0;
            }

            if (!entry.IsInt)
            {
                return 0;
            }

            return entry.IntValue;
        }

        // Mirrors: Property_GetStringValuePtr(char const*) const
        internal string? Property_GetStringValuePtr(string key)
        {
            PropertyEntry? entry = Property_GetPtr(key);
            if (entry == null)
            {
                return null;
            }

            return entry.StringValue;
        }

        // Enumeration in insertion order (useful for save routines)
        internal IEnumerable<PropertyEntry> EnumerateInOrder()
        {
            LinkedListNode<PropertyEntry>? node = _properties.First;
            while (node != null)
            {
                yield return node.Value;
                node = node.Next;
            }
        }

        // Represents NMasterPropertyManager::SProperty
        internal sealed class PropertyEntry
        {
            internal string Key { get; }

            internal bool IsInt { get; set; }

            internal int IntValue { get; set; }

            internal string? StringValue { get; set; }

            internal PropertyEntry(string key, bool isInt, int intValue, string? stringValue)
            {
                Key = key;
                IsInt = isInt;
                IntValue = intValue;
                StringValue = stringValue;
            }
        }
    }
}