namespace OpenVikings.NXBasics
{
    internal sealed class CListBase<T> : IEnumerable<T> where T : class
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

        // Mirrors NXBasics::CListBase::l_Base_RemoveElement
        internal void L_Base_RemoveElement(T item)
        {
            _list.Remove(item);
        }

        // Mirrors NXBasics::CListBase::l_Base_GetStartElement
        internal T? L_Base_GetStartElement()
        {
            return _list.First?.Value;
        }

        // Mirrors NXBasics::CListBase::l_Base_RemoveFromEnd
        internal T? L_Base_RemoveFromEnd()
        {
            if (_list.Last == null)
            {
                return null;
            }

            LinkedListNode<T> node = _list.Last;
            T value = node.Value;
            _list.RemoveLast();
            return value;
        }

        internal void DeleteAllElements()
        {
            // Important: only clears the list. Deletion of objects is handled elsewhere in managed code.
            _list.Clear();
        }

        // Enables correct iteration without inventing list APIs
        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}