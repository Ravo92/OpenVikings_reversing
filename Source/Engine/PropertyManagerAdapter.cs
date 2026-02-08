using OpenVikings.Interfaces;

namespace OpenVikings.Engine
{
    internal sealed class PropertyManagerAdapter : IPropertyManager
    {
        internal PropertyManagerAdapter()
        {
        }

        public bool Exists(string key)
        {
            // TODO: Forward to your properties/config store.
            throw new NotImplementedException();
        }

        public void Remove(string key)
        {
            // TODO: Forward to your properties/config store.
            throw new NotImplementedException();
        }
    }
}