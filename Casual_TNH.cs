using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using FistVR;

namespace Casual_TNH
{
    [BepInPlugin("Nepper.CasualTNH", "Casual TNH", "1.0.0")]
    public class Casual_TNH : BaseUnityPlugin
    {
        private static Casual_TNH casual_tnh;
        private static ManualLogSource LoggerInstance;
        private static ConfigEntry<int> MagUpgradeValue;
        private static ConfigEntry<string> MagUpgradeType;
        private static ConfigEntry<bool> ModifiedMagCostsEnabled;
        private static ConfigEntry<bool> ShorterAnalyzePhases;
        private static ConfigEntry<float> TimeModifier;
        private static ConfigEntry<bool> IsRerollFree;
        private static ConfigEntry<int> TokenMultiplier;
        private Harmony harmony;

        void Awake()
        {
            casual_tnh = this;
            LoggerInstance = Logger;
            MagUpgradeValue = Config.Bind("Magazine Upgrade Costs", "Magazine_upgrade_cost", 0, "Value to used for calculating magazine upgrade costs.");
            MagUpgradeType = Config.Bind("Magazine Upgrade Costs", "Magazine_upgrade_cost_type", "Flat", "How you want to use \"Magazine_upgrade_cost\" value to calculate magazine upgrade costs. Set it to either \"Flat\" or \"Multiplier\".");
            ModifiedMagCostsEnabled = Config.Bind("Magazine Upgrade Costs", "Change_magazine_upgrade_costs", true, "If mod's magazine upgrade feature should be enabled or not. Set it to false if you don't want to use this.");
            ShorterAnalyzePhases = Config.Bind("Shorter Analyze Phases", "Enabled", true, "If analyze phases should be shortened or not. Set it to false if you don't want to use this.");
            TimeModifier = Config.Bind("Shorter Analyze Phases", "Time_modifier", 0.33f, "Multiplier value used for calculating how long it takes for encryptions to appear. Lesser values means shorter times. \nRecommend values are between 0.25 to 0.75 for short analyze phases.");
            IsRerollFree = Config.Bind("Reroll Costs", "Rerolls_cost_nothing", true, "If rerolls should be free or not. Set it to false if you don't want to use this.");
            TokenMultiplier = Config.Bind("Token Multiplier", "Token_gains_multiplied_by", 5, "Multiplier for token modification. Set it to 1 if you don't want to use this.");
            harmony = new Harmony("Nepper.CasualTNH");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(TNH_Manager), "AddTokens")]
        class TokenModifierPatch
        {
            static bool Prefix(ref TNH_Manager __instance, int i, bool Scorethis)
            {
                LoggerInstance.LogInfo("Patching AddTokens method!");
                int Modifier = TokenMultiplier.Value;

                FieldInfo m_numTokensField = typeof(TNH_Manager).GetField("m_numTokens", BindingFlags.NonPublic | BindingFlags.Instance);
                if (m_numTokensField != null)
                {
                    int? currentNullableValue = (int?)m_numTokensField.GetValue(__instance);
                    if (currentNullableValue.HasValue)
                    {
                        int currentValue = currentNullableValue.Value;
                        LoggerInstance.LogInfo("Token's you would get without this mod: " + i);
                        currentValue += i * Modifier;
                        LoggerInstance.LogInfo("Token's you are going to get with this mod: " + i * Modifier);
                        m_numTokensField.SetValue(__instance, currentValue);
                        if (Scorethis)
                        {
                            __instance.Increment(8, i, false);
                        }
                        __instance.OnTokenCountChange(currentValue);
                    }
                }
                LoggerInstance.LogInfo("Patched AddTokens method!");

                return false;
            }
        }

        [HarmonyPatch(typeof(TNH_HoldPoint), "BeginAnalyzing")]
        class ModifyBeginAnalyzingPatch
        {
            static void Postfix(ref TNH_HoldPoint __instance)
            {
                float timemodifier = TimeModifier.Value;
                bool ShortAnalyze = ShorterAnalyzePhases.Value;
                if (ShortAnalyze)
                {
                    LoggerInstance.LogInfo("Beginning Postfix of BeginAnalyzing method.");
                    FieldInfo m_curPhaseField = typeof(TNH_Manager).GetField("m_curPhase", BindingFlags.NonPublic | BindingFlags.Instance);
                    if(m_curPhaseField == null)
                    {
                        LoggerInstance.LogInfo("m_curPhaseField is null, something is wrong.");
                    }
                    TNH_HoldChallenge.Phase m_curphase = (TNH_HoldChallenge.Phase)m_curPhaseField.GetValue(__instance);
                    if (m_curphase == null)
                    {
                        LoggerInstance.LogInfo("m_curphase is null, something is wrong.");
                    }
                    float scantime = m_curphase.ScanTime;
                    if (scantime == null)
                    {
                        LoggerInstance.LogInfo("m_curphase.ScanTime is null, something is wrong.");
                    }
                    LoggerInstance.LogInfo("Scan time before mod: " + scantime);

                    FieldInfo m_tickDownToIdentificationField = typeof(TNH_HoldPoint).GetField("m_tickDownToIdentification", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_tickDownToIdentificationField == null)
                    {
                        LoggerInstance.LogInfo("m_tickDownToIdentificationField is null, something is wrong.");
                    }
                    float newscantime = scantime * timemodifier;
                    LoggerInstance.LogInfo("Scan time after mod: " + newscantime);
                    m_tickDownToIdentificationField.SetValue(__instance, newscantime);

                    LoggerInstance.LogInfo("Ending Postfix of BeginAnalyzing method.");
                    return;
                }
                LoggerInstance.LogInfo("Skipped Postfix of BeginAnalyzing method.");
            }
        }

        [HarmonyPatch(typeof(TNH_ObjectConstructor), "ButtonClicked_Reroll")]
        class ModifyRerollCost
        {
            static bool Prefix(ref TNH_ObjectConstructor __instance, int which)
            {
                bool isRerollFree = IsRerollFree.Value;
                if (isRerollFree)
                {
                    LoggerInstance.LogInfo("Beginning to patch ButtonClicked_Reroll method.");
                    int num = 0;
                    if (__instance.M.GetNumTokens() >= num)
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Select, __instance.transform.position);
                        __instance.M.RegenerateConstructor(__instance, which);
                        __instance.M.SubtractTokens(num);

                        FieldInfo m_poolAddedCostField = typeof(TNH_ObjectConstructor).GetField("m_poolAddedCost", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (m_poolAddedCostField != null)
                        {
                            List<int> m_pooladdedcost = (List<int>)m_poolAddedCostField.GetValue(__instance);
                            m_pooladdedcost[which] = 0;
                            m_poolAddedCostField.SetValue(__instance, m_pooladdedcost);
                        }

                        MethodInfo updateTokenDisplayMethod = typeof(TNH_ObjectConstructor).GetMethod("UpdateTokenDisplay", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (updateTokenDisplayMethod != null)
                        {
                            updateTokenDisplayMethod.Invoke(__instance, new object[] { __instance.M.GetNumTokens() });
                        }

                        LoggerInstance.LogInfo("ButtonClicked_Reroll method is patched.");
                        return false;
                    }
                    SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                    LoggerInstance.LogInfo("ButtonClicked_Reroll method is patched.");
                    return false;
                }
                LoggerInstance.LogInfo("Skipped patching ButtonClicked_Reroll method.");
                return true;
            }
        }

        [HarmonyPatch(typeof(TNH_MagDuplicator), "Button_Upgrade")]
        class ModifyMagazineUpgradeCost
        {
            static bool Prefix(ref TNH_MagDuplicator __instance)
            {
                bool isModifiedMagCostsEnabled = ModifiedMagCostsEnabled.Value;
                if (isModifiedMagCostsEnabled)
                {
                    LoggerInstance.LogInfo("Beginning to patch Button_Upgrade method.");
                    string Cost_Type = MagUpgradeType.Value;
                    int Cost_Value = MagUpgradeValue.Value;

                    FieldInfo m_detectedMagField = typeof(TNH_MagDuplicator).GetField("m_detectedMag", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_detectedMagField == null)
                    {
                        LoggerInstance.LogInfo("m_detectedMagField is null, something is wrong.");
                    }
                    FVRFireArmMagazine m_detectedmag = (FVRFireArmMagazine)m_detectedMagField.GetValue(__instance);
                    if (m_detectedmag == null)
                    {
                        LoggerInstance.LogInfo("m_detectedmag is null, something is wrong.");
                    }

                    if (Cost_Type == "Flat")
                    {
                        if (__instance.M.GetNumTokens() < Cost_Value)
                        {
                            SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                            return false;
                        }
                    }
                    if (Cost_Type == "Multiplier")
                    {
                        if (__instance.M.GetNumTokens() < 3 * Cost_Value)
                        {
                            SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                            return false;
                        }
                    }
                    if (__instance.M.GetNumTokens() < 0)
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                        return false;
                    }
                    if (m_detectedmag == null)
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                        return false;
                    }
                    if (!IM.CompatMags.ContainsKey(m_detectedmag.MagazineType))
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                        return false;
                    }
                    List<FVRObject> list = IM.CompatMags[m_detectedmag.MagazineType];
                    FVRObject fvrobject = null;
                    int num = 10000;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!(list[i].ItemID == m_detectedmag.ObjectWrapper.ItemID) && list[i].MagazineCapacity > m_detectedmag.m_capacity && list[i].MagazineCapacity < num)
                        {
                            fvrobject = list[i];
                            num = list[i].MagazineCapacity;
                        }
                    }
                    if (fvrobject != null)
                    {
                        if (Cost_Type == "Flat")
                        {
                            __instance.M.SubtractTokens(Cost_Value);
                        }
                        if (Cost_Type == "Multiplier")
                        {
                            __instance.M.SubtractTokens(3 * Cost_Value);
                        }
                        __instance.M.Increment(10, false);
                        UnityEngine.Object.Destroy(m_detectedmag.GameObject);
                        UnityEngine.Object.Instantiate<GameObject>(fvrobject.GetGameObject(), __instance.Spawnpoint_Mag.position, __instance.Spawnpoint_Mag.rotation);
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Spawn, __instance.transform.position);
                    }

                    LoggerInstance.LogInfo("Button_Upgrade method is patched.");
                    return false;
                }
                LoggerInstance.LogInfo("Skipped patching Button_Upgrade method.");
                return true;
            }
        }
    }
}
