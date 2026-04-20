using HarmonyLib;
using Verse;

namespace NITH
{
    [StaticConstructorOnStartup]
    public static class NITH_Init
    {
        static NITH_Init()
        {
            new Harmony("com.cheatereater.nith").PatchAll();
        }
    }
}
