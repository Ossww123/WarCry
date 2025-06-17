#if ST_MM_2 && ST_MM_2_BIOMES

namespace ArctiumStudios.SplineTools
{
    public interface IBiomePortalExit<out T> where T : class
    {
        T Enter();
    }
}

#endif
