namespace OpenVikings.Interfaces
{
    internal interface IPropertyManager
    {
        bool Exists(string key);
        void Remove(string key);
    }
}
