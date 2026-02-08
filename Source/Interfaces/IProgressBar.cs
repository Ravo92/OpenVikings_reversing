namespace OpenVikings.Interfaces
{
    internal interface IProgressBar
    {
        void Init(bool isColdStart);
        void Exit();
    }
}
