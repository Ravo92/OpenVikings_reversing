using OpenVikings.NXBasics;
using System;

namespace OpenVikings.Engine
{
    internal sealed class EngineDisplay2D : IDisposable
    {
        private readonly CBitmap _bitmap;
        private readonly bool _interactive;

        private readonly object _staticVars;

        private UserInteractionTargetData _interactionData;
        private bool _disposed;

        internal EngineDisplay2D(CBitmap bitmap, bool interactive, object staticVars)
        {
            _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
            _interactive = interactive;

            // Mirrors passing mStaticVars as an opaque pointer.
            _staticVars = staticVars;

            _interactionData = null;
        }

        internal void EnableInteraction(UserInteractionTargetData data)
        {
            _interactionData = data;
        }

        internal void SetDrawState(int stateId, bool enabled)
        {
            _ = stateId;
            _ = enabled;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _interactionData = null;
            _disposed = true;
        }
    }
}