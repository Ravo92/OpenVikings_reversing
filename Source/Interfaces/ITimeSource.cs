using System;
using System.Collections.Generic;
using System.Text;

namespace OpenVikings.Interfaces
{
    public interface ITimeSource
    {
        /// <summary>
        /// Returns a monotonically increasing time value in milliseconds (best effort).
        /// </summary>
        int TimeMilliseconds();
    }
}