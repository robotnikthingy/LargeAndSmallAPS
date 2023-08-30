using System;
using System.Collections.Generic;

using UnityEngine;

using HarmonyLib;

using BrilliantSkies.Blocks.Weapons.Uis;
using BrilliantSkies.Ui.Tips;
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective.Buttons;
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective.Numbers;
using BrilliantSkies.Ui.Consoles.Getters;
using BrilliantSkies.Ui.Consoles.Interpretters.Simple;
using BrilliantSkies.Ui.Consoles.Interpretters.Subjective.Choices;
using BrilliantSkies.Ui.Layouts.DropDowns;
using BrilliantSkies.Core.Serialisation.Parameters.Prototypes;
using BrilliantSkies.Core.Help;
using BrilliantSkies.Core.Serialisation.AsDouble;
using BrilliantSkies.Core.Widgets;


namespace LargeAPS
{
    /// <summary>
    /// The generic part of the APS increaser plugin
    /// </summary>
    public class LargeAPS
    {
        /// <summary>The new maximum APS shell diameter (in mm)</summary>
        static public Single MAX_DIAMETER_MM = 1000.0f;

        /// <summary>The new MINIMUM APS shell diameter (in mm)</summary>
        static public Single MIN_DIAMETER_MM = 1.0f;
    }

    //Doesnt work for some reason, 
    //[HarmonyPatch(typeof(ApsShellUiData))]
    //[HarmonyPatch(MethodType.Constructor, typeof(UInt32))]
    //public class Patch_ApsShellUiData
    //{
    //    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //    {
    //        return Transpilers.Manipulator(instructions, code => code.operand is float f && f == 500f, code => code.operand = 1000f);
    //    }
    //}

    /// <summary>
    /// Ensure that the variable slider attribute will not clamp the value when loaded
    /// Note that the value will be clamped by the variable itself if it is a VarFloatClamp (and all the sliders are attached to VarFloatClamp variable, so it's OK)
    /// </summary>
    [HarmonyPatch(typeof(ProtoSync))]
    public class Patch_ProtoSync
    {
        /// <summary>
        /// Load the value without clamping it
        /// </summary>
        /// <param name="__result">The loaded value</param>
        /// <param name="fromDisk">The raw data</param>
        /// <param name="index">The index of this value</param>
        /// <param name="sliderData">The slider-specific description</param>
        /// <param name="defaultValue">The default value</param>
        /// <returns>Always 'false' because we completely replace the existing method</returns>
        [HarmonyPatch("LoadSlider")]
        [HarmonyPrefix]
        static public Boolean LoadSlider(ref Single __result, SuperSaverBytesWithId fromDisk, UInt32 index, ISlider sliderData, Single defaultValue)
        {
            __result = ProtoSync.LoadFloat(fromDisk, index, defaultValue);

            return false;
        }
    }

    /// <summary>
    /// Patches the checks performed on the gauge
    /// </summary>
    [HarmonyPatch(typeof(AdvCannonFiringPiece))]
    public class Patch_AdvCannonFiringPiece
    {
        /// <summary>The maximum number of gauge increaser needed in order to reach the maximum diameter (maxed to 1000 because more would be insane)</summary>
        static private Int32 MAX_GAUGE_INCREASER = -1;

        /// <summary>
        /// Compute the maximum number of gauge increaser needed in order to reach the maximum diameter
        /// </summary>
        static private void ComputeMaxGaugeIncreaser()
        {
            if (MAX_GAUGE_INCREASER > 0)
                return;     // Already computed

            for (int i = 0; i <= 1000; i++)
            {
                if (Adjustments.BiAM(0.06f, i, 0.06f, 0.98f) >= LargeAPS.MAX_DIAMETER_MM / 1000.0f)
                {
                    MAX_GAUGE_INCREASER = i;
                    return;
                }
            }

            // The number of gauge increaser necessary is insane, we se the limitation to 1000 to avoid crazy things
            MAX_GAUGE_INCREASER = 1000;
        }

        /// <summary>
        /// Changes the max value of the desired gauge variable
        /// </summary>
        /// <param name="__instance">The instance</param>
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPostfix]
        static public void AdvCannonFiringPiece(AdvCannonFiringPiece __instance)
        {
            VarFloatClamp DesiredGauge = __instance.Data.DesiredGauge as VarFloatClamp;
            DesiredGauge.Max = LargeAPS.MAX_DIAMETER_MM / 1000.0f;

            DesiredGauge.Min = LargeAPS.MIN_DIAMETER_MM / 1000.0f;
        }

        /// <summary>
        /// Ensure that the gauge check uses the newly computed maximum number of gauge increaser
        /// </summary>
        /// <param name="__result">The number of gauge increaser necessary (-1 if problem)</param>
        /// <param name="diameter">The requested diameter</param>
        /// <param name="barrelModifier">The barrel modifier (1 for one barrel, 0.5 for 2 barrels, etc.)</param>
        /// <returns>Always 'false' because we completely replace the existing method</returns>
        [HarmonyPatch("GaugeIncreasesToProvideShellDiameter")]
        [HarmonyPrefix]
        static public Boolean GaugeIncreasesToProvideShellDiameter(ref Int32 __result, float diameter, float barrelModifier)
        {
            // Ensure we have computed the maximum number of gauge increaser necessary to reach the new max diameter
            ComputeMaxGaugeIncreaser();

            // The copy/paste of the existing method starts here, we are only changing the max number of gauge increaser, except for that the code is exactly the same
            for (int i = 0; i <= MAX_GAUGE_INCREASER; i++)
            {
                if ((barrelModifier * Adjustments.BiAM(0.06f, i, 0.06f, 0.98f)) >= diameter)
                {
                    __result = i;
                    return false;
                }
            }

            __result = -1;
            return false;
        }
    }

    /// <summary>
    /// Patches the checks performed on the gauge
    /// </summary>
    [HarmonyPatch(typeof(AdvCannonNode))]
    public class Patch_AdvCannonNode
    {
        /// <summary>
        /// Ensure we are using the new max diameter
        /// </summary>
        /// <param name="__instance">The instance</param>
        /// <returns>Always 'false' because we completely replace the existing method</returns>
        [HarmonyPatch("CheckGauge")]
        [HarmonyPrefix]
        static public Boolean CheckGauge(AdvCannonNode __instance)
        {
            // The copy/paste of the existing method starts here, we are only changing the max diameter, except for that the code is exactly the same
            if (__instance.GoverningBlock.BarrelSystem == null)
            {
                return false;
            }

            var ShellDiameter = Traverse.Create(__instance).Property<Single>("ShellDiameter");

            var forcedGauge = __instance.GoverningBlock.Data.ForceDesiredGauge.Us;
            var possibleDiameter = Mathf.Min(LargeAPS.MAX_DIAMETER_MM / 1000.0f, Adjustments.BiAM(0.06f, __instance.nGaugeIncreases, 0.06f, 0.98f)) * __instance.GoverningBlock.BarrelSystem.GetBarrelAndShellScaleFactor();
            var prevDiameter = __instance.ShellDiameter;

            if (!forcedGauge)
            {
                ShellDiameter.Value = Mathf.Min(possibleDiameter, __instance.GoverningBlock.Data.DesiredGauge.Us);
            }
            else
            {
                ShellDiameter.Value = __instance.GoverningBlock.Data.DesiredGauge.Us;
            }

            __instance.HasEnoughGaugeIncreases = !forcedGauge || (possibleDiameter >= __instance.ShellDiameter - 0.001f);
            possibleDiameter = Mathf.Min(possibleDiameter, __instance.ShellDiameter);

            __instance.GoverningBlock.BarrelSystem.ShellDiameter = possibleDiameter;

            if (prevDiameter != __instance.ShellDiameter)
            {
                __instance.ShellRacks.ClearAllOfWrongGauge(__instance.ShellDiameter);
                __instance.GoverningBlock.SetRecoilAbsorbers(__instance.HydraulicCapacity, __instance.HydraulicRefresh);
            }

            return false;
        }
    }

    /// <summary>
    /// Patches the UI
    /// </summary>
    [HarmonyPatch(typeof(ApsTab))]
    public class Patch_ApsTab
    {
        /// <summary>
        /// Ensure we are using the new max diameter
        /// </summary>
        /// <param name="__instance">The instance</param>
        /// <returns>Always 'false' because we completely replace the existing method</returns>
        [HarmonyPatch("Build")]
        [HarmonyPrefix]
        static public Boolean Build(ApsTab __instance)
        {
            // The copy/paste of the existing method starts here, we are only changing the max diameter, except for that the code is exactly the same
            __instance.CreateHeader(ApsTab._locFile.Get("Header_Ammo", "Ammo"),
                new ToolTip(ApsTab._locFile.Get("Header_Ammo_Tip", "Access the ammo intakes and clear the clips attached to this weapon.")));

            var intakesSegment = __instance.CreateTableSegment(2, 10);
            intakesSegment.SqueezeTable = false;

            intakesSegment.AddInterpretter(SubjectiveButton<AdvCannonFiringPiece>.Quick(__instance._focus,
                ApsTab._locFile.Get("Button_ClearClips", "Clear clips"), new ToolTip(ApsTab._locFile.Get("Button_ClearClips_Tip", "Clear all ammo clips attached to this weapon")),
                (I) => I.ClearAllClips()));

            intakesSegment.AddInterpretter(SubjectiveButton<AdvCannonFiringPiece>.Quick(__instance._focus,
                ApsTab._locFile.Get("Button_AccessIntakes", "Access intakes"), new ToolTip(ApsTab._locFile.Get("Button_AccessIntakes_Tip", "Open the UI of an intake attached to this weapon")),
                (I) => I.AccessIntake()));


            __instance.CreateHeader(ApsTab._locFile.Get("Header_CannonSettings", "Cannon settings"),
                new ToolTip(ApsTab._locFile.Get("Header_CannonSettings_Tip", "Change the settings of this advanced cannon.")));

            var segment = __instance.CreateStandardSegment();

            segment.AddInterpretter(SubjectiveFloatClampedWithBarFromMiddle<AdvCannonFiringPiece>.Quick(
                __instance._focus, -__instance._focus.BarrelSystem.MaxEleNegativeTraverse, __instance._focus.BarrelSystem.MaxElePositiveTraverse, 1f, 0f,
                M.m<AdvCannonFiringPiece>(I => I.Data.IdleElevation),
                ApsTab._locFile.Get("Bar_IdleAngle", "Idle angle {0}° "), (I, f) => I.Data.IdleElevation.Us = (f),
                new ToolTip(ApsTab._locFile.Get("Bar_IdleAngle°_Tip", "Set the elevation to which the barrel will return when idle"))));

            segment.AddInterpretter(SubjectiveFloatClampedWithBarFromMiddle<AdvCannonFiringPiece>.Quick(
                __instance._focus, 1f, 6f, 1f, 1f, M.m<AdvCannonFiringPiece>(I => I.BarrelSystem.BarrelCount),
                ApsTab._locFile.Get("Bar_NumberOfBarrels", "Number of barrels: {0}"), (I, f) => I.Data.BarrelCount.Us = ((int)f),
                new ToolTip(ApsTab._locFile.Get("Bar_NumberOfBarrels_Tip", "The number of barrels"))));

            //segment.AddInterpretter(Quick.SliderFromMiddle(__instance._focus.Data, t => nameof(t.CooldownOverClock)));

            segment.AddInterpretter(SubjectiveFloatClampedWithBar<AdvCannonFiringPiece>.Quick(
                __instance._focus, 1f, LargeAPS.MAX_DIAMETER_MM, .01f, M.m<AdvCannonFiringPiece>(I => I.Data.DesiredGauge * 1000f),
                ApsTab._locFile.Get("Bar_DesiredShellGaugeMm", "Desired shell gauge {0} mm"), (I, f) => I.Data.DesiredGauge.Us = (f * 0.001f),
                new ToolTip(ApsTab._locFile.Get("Bar_DesiredShellGaugeMm_Tip", "This is the shell diameter in millimeters for a single barreled cannon. Your desired gauge will only kick in if it is LOWER than the gauge you achieve through gauge increasers"))));

            segment.AddInterpretter(SubjectiveFloatClampedWithBar<AdvCannonFiringPiece>.Quick(
                __instance._focus, 1f, 2400f, .01f, M.m<AdvCannonFiringPiece>(I => I.Data.MaxFireRatePerMinute),
                ApsTab._locFile.Get("Bar_MaxFireRatePerMinute", "Max fire rate {0} per minute"), (I, f) => I.Data.MaxFireRatePerMinute.Us = (f),
                new ToolTip(ApsTab._locFile.Get("Bar_MaxFireRatePerMinute_Tip", "Set the maximum rate of fire in shells per minute"))));

            DropDownMenuAlt<AimingArcType> _AimingArcTypeMenu = Traverse.Create(__instance).Field("_AimingArcTypeMenu").GetValue<DropDownMenuAlt<AimingArcType>>();
            segment.AddInterpretter(
                new DropDown<AdvCannonFiringPiece, AimingArcType>(
                    __instance._focus,
                    _AimingArcTypeMenu,
                    (I, i) => I.Data.AimingArcType == i,
                    (I, i) => I.Data.AimingArcType.Us = i)
            );

            segment.AddInterpretter(
                new SubjectiveToggle<AdvCannonFiringPiece>(
                    __instance._focus,
                    M.m<AdvCannonFiringPiece>(ApsTab._locFile.Get("Toggle_ForceDesiredGauge", "Force desired gauge")),
                    M.m<AdvCannonFiringPiece>(new ToolTip(ApsTab._locFile.Get("Toggle_ForceDesiredGauge_Tip", "Always use the desired gauge set on the slider. Won't clear clips and won't fire if there are not enough gauge increases available"))),
                    (I, b) => I.SetForcedDesiredGauge(b),
                    (I, b) => b ? ApsTab._locFile.Get("Toggle_ForceDesiredGauge", "Force desired gauge") : ApsTab._locFile.Get("Toggle_ForceDesiredGauge_UseMaxAchievableGauge", "Use maximum achievable gauge"),
                    (I) => I.Data.ForceDesiredGauge.Us,
                    "forced" //LOCIGN
                )
            );


            __instance.CreateHeader(ApsTab._locFile.Get("Header_Aesthetics", "Aesthetics"),
                new ToolTip(ApsTab._locFile.Get("Header_Aesthetics_Tip", "Configure whether the barrels and railgun magnets visually spin, and how far apart multiple barrels will be placed.")));

            var spinSegment = __instance.CreateTableSegment(2, 2);
            spinSegment.SqueezeTable = false;

            spinSegment.AddInterpretter(Quick.Toggle(__instance._focus.Data, t => nameof(t.AllowBarrelSpin)));
            spinSegment.AddInterpretter(Quick.Toggle(__instance._focus.Data, t => nameof(t.InvertSpinDirection)));
            spinSegment.AddInterpretter(Quick.Toggle(__instance._focus.Data, t => nameof(t.AllowRailgunSpin)));
            spinSegment.AddInterpretter(Quick.Toggle(__instance._focus.Data, t => nameof(t.DisableBarrelReciprocation)));

            spinSegment.AddInterpretter(Quick.Slider(__instance._focus.Data, t => nameof(t.BarrelSpacing)));



            __instance.CreateSpace(0);


            var buttonSegment = __instance.CreateTableSegment(2, 2);
            buttonSegment.SqueezeTable = false;
            buttonSegment.AddInterpretter(SubjectiveButton<ConstructableWeapon>.Quick(__instance._focus,
                ApsTab._locFile.Get("Button_Copy", "Copy to clipboard"), new ToolTip(ApsTab._locFile.Get("Button_Copy_Tip", "Copy all settings to clipboard")),
                (I) => RootCopyPaster.Copy(I)));

            var paste = buttonSegment.AddInterpretter(SubjectiveButton<ConstructableWeapon>.Quick(__instance._focus,
                ApsTab._locFile.Get("Button_Paste", "Paste from clipboard"), new ToolTip(ApsTab._locFile.Get("Button_Paste_Tip", "Paste all settings from clipboard")),
                (I) => RootCopyPaster.Paste(I)));

            paste.Color = M.m<ConstructableWeapon>(I => RootCopyPaster.ReadyToPaste(I) ? Color.white : Color.grey);
            paste.TextColor = M.m<ConstructableWeapon>(I => RootCopyPaster.ReadyToPaste(I) ? Color.white : Color.grey);


            __instance.CreateSpace(20); // Put the buttons 20 pix above the bottom so they look better.

            return false;
        }
    }

    //[HarmonyPatch(typeof(SubjectiveFloatClampedWithNub<Var<float>>))]
    //public class Patch_SubjectiveFloatClampedWithNub
    //{
    //    [HarmonyPatch("Quick")]
    //    [HarmonyPostfix]
    //    public static SubjectiveFloatClampedWithNub<Var<float>> Quick(SubjectiveFloatClampedWithNub<Var<float>> __instance)
    //    {
    //        if (inc > 0.99f)
    //        {
    //            displayFormatable = displayFormatable.Replace("{0}", "{0:F0}");
    //        }
    //        else if (inc > 0.099f)
    //        {
    //            displayFormatable = displayFormatable.Replace("{0}", "{0:0.#}");
    //        }
    //        else if (inc > 0.0099f)
    //        {
    //            displayFormatable = displayFormatable.Replace("{0}", "{0:0.##}");
    //        }
    //        else if (inc > 0.00099f)
    //        {
    //            displayFormatable = displayFormatable.Replace("{0}", "{0:0.###}");
    //        }
    //        else if (inc > 9.9E-05f)
    //        {
    //            displayFormatable = displayFormatable.Replace("{0}", "{0:0.####}");
    //        }
    //        return new SubjectiveFloatClampedWithNub<Var<float>>(M.m<I>(min), M.m<I>(max), current, M.m<I>(inc), subject, M.m((I I) => string.Format(displayFormatable, current.GetFromSubject(subject))), action, null, M.m<I>(tip));

    //    }
    //}
}
