namespace OpenVikings.Interfaces
{
    internal interface IKeyManager
    {
        void PushKey(byte virtualKey, bool isText, bool isDown);
        void PushText(char character);
    }
}
