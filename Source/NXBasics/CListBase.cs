namespace OpenVikings.NXBasics
{
    internal sealed class CListBase<T>
    {
        private readonly LinkedList<T> _list;

        internal CListBase()
        {
            _list = new LinkedList<T>();
        }

        internal void InsertAtStart(T item)
        {
            _list.AddFirst(item);
        }

        internal void InsertAtEnd(T item)
        {
            _list.AddLast(item);
        }

        internal bool Remove(T item)
        {
            return _list.Remove(item);
        }

        internal bool Contains(T item)
        {
            return _list.Contains(item);
        }

        internal T GetStartOrDefault()
        {
            if (_list.First == null)
            {
                return default;
            }

            return _list.First.Value;
        }

        internal T RemoveFromEndOrDefault()
        {
            // Mirrors NXBasics::CListBase::l_Base_RemoveFromEnd
            // Returns default(T) if empty, otherwise removes and returns the last element.

            if (_list.Last == null)
            {
                return default;
            }

            LinkedListNode<T> node = _list.Last;
            T value = node.Value;
            _list.RemoveLast();
            return value;
        }

        internal void Clear()
        {
            _list.Clear();
        }
    }
}