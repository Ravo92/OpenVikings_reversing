using OpenVikings.NXBasics;

namespace OpenVikings.NXBaseGui
{
    internal sealed class InitDesktopVars
    {
        // These lists mirror the multiple CListBase members initialized in CDesktop::SVars::SVars().
        internal CListBase<CBaseElement> ListA { get; }
        internal CListBase<CBaseElement> ListB { get; }
        internal CListBase<CBaseElement> ListC { get; }
        internal CListBase<CBaseElement> ListD { get; }

        internal CListBase<CBaseElement> ListE { get; }
        internal CListBase<CBaseElement> ListF { get; }

        internal CListBase<CBaseElement> ListG { get; }
        internal CListBase<CBaseElement> ListH { get; }

        internal CListBase<CBaseElement> ListI { get; }
        internal CListBase<CBaseElement> ListJ { get; }

        internal CListBase<CBaseElement> ListK { get; }
        internal CListBase<CBaseElement> ListL { get; }

        internal CListBase<CBaseElement> ListM { get; }

        // Aliases for known semantics from RE
        internal CListBase<CBaseElement> BackgroundListA
        {
            get { return ListA; }
        }

        internal CListBase<CBaseElement> BackgroundListB
        {
            get { return ListB; }
        }

        internal InitDesktopVars()
        {
            ListA = new CListBase<CBaseElement>();
            ListB = new CListBase<CBaseElement>();
            ListC = new CListBase<CBaseElement>();
            ListD = new CListBase<CBaseElement>();

            ListE = new CListBase<CBaseElement>();
            ListF = new CListBase<CBaseElement>();

            ListG = new CListBase<CBaseElement>();
            ListH = new CListBase<CBaseElement>();

            ListI = new CListBase<CBaseElement>();
            ListJ = new CListBase<CBaseElement>();

            ListK = new CListBase<CBaseElement>();
            ListL = new CListBase<CBaseElement>();

            ListM = new CListBase<CBaseElement>();
        }
    }
}