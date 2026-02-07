using OpenVikings.Interfaces;
using OpenVikings.SystemHandles;

namespace OpenVikings.Engine
{
    internal sealed class TitleScreenHandler : ITitleScreenHandler
    {
        public void Startup()
        {
            // Show intro bitmap/video and allow skip via AnyUserInput.
            IntroOutroHandler.ShowIntroBmpAndArmSkip();
        }

        public void Shutdown()
        {
            IntroOutroHandler.ClearAll();
        }

        public bool Update()
        {
            // TODO: When IntroOutroHandler exposes a "Done" flag, switch to MainMenu here.
            return true;
        }
    }
}