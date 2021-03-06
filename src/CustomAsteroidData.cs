using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;

namespace Starstrider42.CustomAsteroids
{
    /// <summary>
    /// Represents asteroid-specific information not included in the stock game.
    /// </summary>
    public sealed class CustomAsteroidData : PartModule
    {
        /// <summary>The name of the composition class to use.</summary>
        /// <remarks>Intended for use by code.</remarks>
        [KSPField]
        public string composition;
        /// <summary>The name of the composition class to use.</summary>
        /// <remarks>Intended for display to the user.</remarks>
        [KSPField (guiActive = true, guiActiveEditor = true,
                   guiName = "#autoLOC_CustomAsteroids_GuiClass")]
        public string displayComposition;

        /// <summary>
        /// The unique ID of the asteroid set that created this asteroid.
        /// </summary>
        /// <value>The name of the set, or null if inapplicable or unknown.</value>
        /// <remarks>The returned name is <em>not</em> guaranteed to match any currently
        /// loaded asteroid config.</remarks>
        public string parentSet {
            get {
                return CustomAsteroidRegistry.Instance.LookupAsteroid (vessel).parentSet;
            }
        }

        public CustomAsteroidData ()
        {
            composition = "Stony";
            displayComposition = Localizer.Format ("#autoLOC_CustomAsteroids_CompStony");
        }

#if DEBUG
        [KSPEvent (
            guiActive = true,
            guiActiveUnfocused = true,
            unfocusedRange = 1000,
            externalToEVAOnly = false,
            guiName = "#autoLOC_CustomAsteroids_GuiDebugResource")]
        public void dumpResources ()
        {
            ModuleAsteroidInfo summary = part.FindModuleImplementing<ModuleAsteroidInfo> ();
            List<ModuleAsteroidResource> resources =
                part.FindModulesImplementing<ModuleAsteroidResource> ();

            if (summary != null) {
                double resourceMass = summary.currentMassVal - summary.massThresholdVal;

                System.Text.StringBuilder report = new System.Text.StringBuilder ();
                report.AppendLine (Localizer.Format (
                        "#autoLOC_CustomAsteroids_LogResourceHeader",
                        part.FindModuleImplementing<ModuleAsteroid> ().AsteroidName));
                report.AppendLine (Localizer.Format ("#autoLOC_CustomAsteroids_GuiClass")
                                   + ": " + composition);
                report.AppendLine (Localizer.Format (
                        "#autoLOC_CustomAsteroids_LogResourceSummary",
                        string.Format ("{0:F1}", resourceMass),
                        string.Format ("{0:F1}", summary.currentMassVal),
                        string.Format ("{0:P1}", resourceMass / summary.currentMassVal)));
                report.AppendLine ();

                foreach (ModuleAsteroidResource resource in resources) {
                    report.AppendLine (Localizer.Format (
                        "#autoLOC_CustomAsteroids_LogResourceName",
                        resource.resourceName));
                    report.AppendLine (Localizer.Format (
                            "#autoLOC_CustomAsteroids_LogResourceAmount",
                            string.Format ("{0:F1}", resource.abundance * resourceMass),
                            string.Format ("{0:P1}", resource.displayAbundance)));
                    if (resource.abundance < 1e-6) {
                        report.AppendLine (Localizer.Format (
                            "#autoLOC_CustomAsteroids_LogResourceFreq",
                            string.Format ("{0:D}", resource.presenceChance)));
                    }
                    report.AppendLine (Localizer.Format (
                        "#autoLOC_CustomAsteroids_LogResourceDistrib",
                        string.Format ("{0:D}", resource.lowRange),
                        string.Format ("{0:D}", resource.highRange)));
                    report.AppendLine ();
                }

                Debug.Log (report.ToString ());
                ScreenMessages.PostScreenMessage (
                    Localizer.Format ("#autoLOC_CustomAsteroids_LogResourceEnd"),
                    5.0f, ScreenMessageStyle.UPPER_CENTER);
            }
        }
#endif
    }
}
