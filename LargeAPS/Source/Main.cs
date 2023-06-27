using System;

using HarmonyLib;

using BrilliantSkies.Modding;
using BrilliantSkies.Ftd.Modes.Changing;


namespace LargeAPS
{
    /// <summary>
    /// The mod's entry point
    /// </summary>
    public class Main : GamePlugin_PostLoad
    {
        /// <summary>All the Harmony patches</summary>
        static private Harmony m_HarmonyPatches = null;

        /// <summary>Have we already applied the patches?</summary>
        static private Boolean m_IsPatched = false;

        /// <summary>Used in FtD's log to indicate the name of the loaded plugin</summary>
        public String name { get { return "LargeAPS"; } }

        /// <summary>Not used in FtD</summary>
        public Version version { get { return new Version(1, 0); } }

        /// <summary>
        /// Called by FtD when the plugin is loaded
        /// </summary>
        public void OnLoad()
        {
            // Create Harmony
            m_HarmonyPatches = new Harmony("LargeAPS");

            // Create the delayed-patching system by patching the main-menu displaying method
            var original = typeof(MainMenuChangeSequence).GetMethod("StartFromChange");
            var postfix = typeof(Main).GetMethod("DelayedPatching");
            m_HarmonyPatches.Patch(original, postfix: new HarmonyMethod(postfix));
        }

        /// <summary>
        /// Called when all the 'OnLoad()' methods of all the plugins have been called
        ///
        /// Specific from 'GamePlugin_PostLoad'
        /// </summary>
        /// <returns>'true' if no problem, 'false' if any problem</returns>
        public Boolean AfterAllPluginsLoaded()
        {
            return true;
        }

        /// <summary>
        /// Not called by FtD
        /// </summary>
        public void OnSave()
        {
        }

        /// <summary>
        /// Will patch the methods when the menu is displayed for the first time, that way all the necessary resources should be loaded
        /// 
        /// This has to be done because some UI requires specific styles (for example the 'ApsTab' UI), and there will be a crash if they are patched before the styles' resources are loaded
        /// </summary>
        /// <param name="__instance">The instance</param>
        static public void DelayedPatching(MainMenuChangeSequence __instance)
        {
            if (!m_IsPatched)
            {
                // Will apply all the patches in this plugin
                m_HarmonyPatches.PatchAll();
                m_IsPatched = true;
            }
        }
    }
}
