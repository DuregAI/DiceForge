using System;

namespace Diceforge.Audio
{
    [Flags]
    public enum MusicContext
    {
        None = 0,
        Menu = 1 << 0,
        Tutorial = 1 << 1,
        Gameplay = 1 << 2,
        Map = 1 << 3,
        All = Menu | Tutorial | Gameplay | Map
    }
}
