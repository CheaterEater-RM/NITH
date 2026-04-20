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
        /// Set only during the last-ditch vanilla fallback in Patch 2 when every NITH
        /// path has failed — allows vanilla center-finding to punch through roofs as a
        /// final resort rather than silently losing the raid.
        /// Must always be cleared in a finally block.
        /// </summary>
        public static bool BypassRoofCheck = false;
    }
}
