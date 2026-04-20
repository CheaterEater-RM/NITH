namespace NITH
{
    /// <summary>
    /// Shared runtime state flags for NITH patches.
    /// All access is on the main game thread; no locking needed.
    /// </summary>
    public static class NITH_State
    {
        /// <summary>
        /// When true, Patch 1 (CanPhysicallyDropInto) passes all cells through unchanged.
        /// Set only during VanillaFallback center-finding in Patch 2.
        /// Must always be cleared in a finally block.
        /// </summary>
        public static bool BypassRoofCheck = false;

        /// <summary>
        /// When true, both Patch 1 (CanPhysicallyDropInto) and Patch 3 (fallback lambda)
        /// pass all cells through unchanged, allowing pods to land on any non-thick-roof cell.
        /// Set for the duration of Arrive() during a vanilla fallback raid — i.e. when the
        /// map is fully sealed and vanilla roof-punching is the only way to get the raid in.
        /// Cleared by Patch 4 (Arrive finalizer) after pod placement completes.
        /// </summary>
        public static bool BypassPodPlacement = false;
    }
}
