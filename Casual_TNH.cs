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
    [BepInPlugin("Nepper.CasualTNH", "Casual TNH", "1.0.1")]
    public class Casual_TNH : BaseUnityPlugin
    {
        private static Casual_TNH casual_tnh;
        private static ManualLogSource LoggerInstance;
        private static ConfigEntry<int> MagCloneValue;
        private static ConfigEntry<string> MagCloneType;
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
            MagCloneValue = Config.Bind("Modify Magazine Costs", "Magazine_clone_cost", 0, "Value to used for calculating magazine clone costs.");
            MagCloneType = Config.Bind("Modify Magazine Costs", "Magazine_clone_cost_type", "Flat", "How you want to use \"Magazine_clone_cost\" value to calculate magazine clone costs. \nSet it to either \"Flat\" or \"Multiplier\". \nSet it to \"Multiplier\" and set \"Magazine_clone_cost\" to 1 if you don't want to use it.");
            MagUpgradeValue = Config.Bind("Modify Magazine Costs", "Magazine_upgrade_cost", 0, "Value to used for calculating magazine upgrade costs.");
            MagUpgradeType = Config.Bind("Modify Magazine Costs", "Magazine_upgrade_cost_type", "Flat", "How you want to use \"Magazine_upgrade_cost\" value to calculate magazine upgrade costs. \nSet it to either \"Flat\" or \"Multiplier\". \nSet it to \"Multiplier\" and set \"Magazine_upgrade_cost\" to 1 if you don't want to use it.");
            ModifiedMagCostsEnabled = Config.Bind("Modify Magazine Costs", "Modify_magazine_costs", true, "If mod is allowed to change magazine upgrade and clone costs. Set it to false if you don't want to use it.");
            ShorterAnalyzePhases = Config.Bind("Shorter Analyze Phases", "Modify_analyze_time", true, "If analyze phases should be shortened or not. Set it to false if you don't want to use it.");
            TimeModifier = Config.Bind("Shorter Analyze Phases", "Time_modifier", 0.5f, "Multiplier value used for calculating how long it takes for encryptions to appear.\n Lesser values means shorter analyze phases. \nRecommend values are between 0.25 to 0.75 for short analyze phases.");
            IsRerollFree = Config.Bind("Reroll Costs", "Rerolls_cost_nothing", true, "If rerolls should be free or not. Set it to false if you don't want to use it.");
            TokenMultiplier = Config.Bind("Token Multiplier", "Token_gains_multiplied_by", 2, "Multiplier for token modification. Set it to 1 if you don't want to use it.");
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
                        LoggerInstance.LogInfo("Tokens you would get without this mod: " + i);
                        currentValue += i * Modifier;
                        LoggerInstance.LogInfo("Tokens you are going to get with this mod: " + i * Modifier);
                        m_numTokensField.SetValue(__instance, currentValue);
                        if (Scorethis)
                        {
                        }
                        __instance.OnTokenCountChange(currentValue);
                    }
                }
                else
                {
                    LoggerInstance.LogInfo("m_numTokensField is null, something is wrong.");
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
                    __instance.M.EnqueueLine(TNH_VoiceLineID.AI_AnalyzingSystem);
                    FieldInfo m_stateField = typeof(TNH_HoldPoint).GetField("m_state", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_stateField == null)
                    {
                        LoggerInstance.LogInfo("m_stateField is null, something is wrong.");
                    }
                    m_stateField.SetValue(__instance, TNH_HoldPoint.HoldState.Analyzing);
                    FieldInfo m_tickDownToIdentificationField = typeof(TNH_HoldPoint).GetField("m_tickDownToIdentification", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_tickDownToIdentificationField == null)
                    {
                        LoggerInstance.LogInfo("m_tickDownToIdentificationField is null, something is wrong.");
                    }
                    FieldInfo m_curPhaseField = typeof(TNH_HoldPoint).GetField("m_curPhase", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_curPhaseField == null)
                    {
                        LoggerInstance.LogInfo("m_curPhaseField is null, something is wrong.");
                    }
                    TNH_HoldChallenge.Phase m_curphase = (TNH_HoldChallenge.Phase)m_curPhaseField.GetValue(__instance);
                    float scantime = m_curphase.ScanTime;
                    if (scantime == null)
                    {
                        LoggerInstance.LogInfo("m_curphase.ScanTime is null, something is wrong.");
                    }
                    LoggerInstance.LogInfo("Scan time before mod: " + scantime);
                    float newscantime = scantime * timemodifier;
                    LoggerInstance.LogInfo("Scan time after mod: " + newscantime);
                    m_tickDownToIdentificationField.SetValue(__instance, newscantime);

                    if (__instance.M.Seed >= 0)
                    {
                        m_tickDownToIdentificationField.SetValue(__instance, newscantime);
                    }
                    if (__instance.M.TargetMode == TNHSetting_TargetMode.NoTargets)
                    {
                        m_tickDownToIdentificationField.SetValue(__instance, newscantime);
                        if (__instance.M.Seed >= 0)
                        {
                            m_tickDownToIdentificationField.SetValue(__instance, newscantime);
                        }
                    }
                    else if (__instance.M.IsBigLevel)
                    {
                        m_tickDownToIdentificationField.SetValue(__instance, newscantime);
                    }

                    __instance.SpawnPoints_Targets.Shuffle<Transform>();

                    FieldInfo m_validSpawnPointsField = typeof(TNH_HoldPoint).GetField("m_validSpawnPoints", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_validSpawnPointsField == null)
                    {
                        LoggerInstance.LogInfo("m_validSpawnPointsField is null, something is wrong.");
                    }
                    List<Transform> m_validspawnpoints = (List<Transform>)m_validSpawnPointsField.GetValue(__instance);
                    m_validspawnpoints.Shuffle<Transform>();
                    m_validSpawnPointsField.SetValue(__instance, m_validspawnpoints);

                    MethodInfo SpawnWarpInMarkersMethod = typeof(TNH_HoldPoint).GetMethod("SpawnWarpInMarkers", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (SpawnWarpInMarkersMethod != null)
                    {
                        SpawnWarpInMarkersMethod.Invoke(__instance, null);
                    }
                    else
                    {
                        LoggerInstance.LogInfo("SpawnWarpInMarkersMethod is null, something is wrong.");
                    }
                    

                    FieldInfo m_systemNodeField = typeof(TNH_HoldPoint).GetField("m_systemNode", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_systemNodeField == null)
                    {
                        LoggerInstance.LogInfo("m_systemNodeField is null, something is wrong.");
                    }
                    TNH_HoldPointSystemNode m_systemnode = (TNH_HoldPointSystemNode)m_systemNodeField.GetValue(__instance);
                    m_systemnode.SetNodeMode(TNH_HoldPointSystemNode.SystemNodeMode.Analyzing);
                    m_systemNodeField.SetValue(__instance, m_systemnode);

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
                    int numTokens = __instance.M.GetNumTokens();
                    if (numTokens >= num)
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
                        else
                        {
                            LoggerInstance.LogInfo("m_poolAddedCostField is null, something is wrong.");
                        }

                        MethodInfo updateTokenDisplayMethod = typeof(TNH_ObjectConstructor).GetMethod("UpdateTokenDisplay", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (updateTokenDisplayMethod != null)
                        {
                            updateTokenDisplayMethod.Invoke(__instance, new object[] { __instance.M.GetNumTokens() });
                        }
                        else
                        {
                            LoggerInstance.LogInfo("updateTokenDisplayMethod is null, something is wrong.");
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

        [HarmonyPatch(typeof(TNH_ObjectConstructor), "ButtonClicked_Unlock")]
        class ModifyUnlockCost
        {
            static bool Prefix(ref TNH_ObjectConstructor __instance, int which)
            {
                bool isRerollFree = IsRerollFree.Value;
                if (isRerollFree)
                {
                    LoggerInstance.LogInfo("Beginning to patch ButtonClicked_Unlock method.");
                    int num = 0;
                    int numTokens = __instance.M.GetNumTokens();
                    if (numTokens >= num)
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Select, __instance.transform.position);

                        MethodInfo UnlockPoolCategoryMethod = typeof(TNH_ObjectConstructor).GetMethod("UnlockPoolCategory", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (UnlockPoolCategoryMethod != null)
                        {
                            UnlockPoolCategoryMethod.Invoke(__instance, new object[] { which });
                        }
                        else
                        {
                            LoggerInstance.LogInfo("UnlockPoolCategoryMethod is null, something is wrong.");
                        }

                        MethodInfo SetStateMethod = typeof(TNH_ObjectConstructor).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (SetStateMethod != null)
                        {
                            SetStateMethod.Invoke(__instance, new object[] { TNH_ObjectConstructor.ConstructorState.EntryList, 0 });
                        }
                        else
                        {
                            LoggerInstance.LogInfo("SetStateMethod is null, something is wrong.");
                        }

                        __instance.M.SubtractTokens(num);

                        MethodInfo updateTokenDisplayMethod = typeof(TNH_ObjectConstructor).GetMethod("UpdateTokenDisplay", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (updateTokenDisplayMethod != null)
                        {
                            updateTokenDisplayMethod.Invoke(__instance, new object[] { __instance.M.GetNumTokens() });
                        }
                        else
                        {
                            LoggerInstance.LogInfo("updateTokenDisplayMethod is null, something is wrong.");
                        }

                        LoggerInstance.LogInfo("ButtonClicked_Unlock method is patched.");
                        return false;
                    }
                    else
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                    }

                    MethodInfo UpdateLockUnlockButtonStateMethod = typeof(TNH_ObjectConstructor).GetMethod("UpdateLockUnlockButtonState", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (UpdateLockUnlockButtonStateMethod != null)
                    {
                        UpdateLockUnlockButtonStateMethod.Invoke(__instance, new object[] { false });
                    }
                    else
                    {
                        LoggerInstance.LogInfo("UpdateLockUnlockButtonStateMethod is null, something is wrong.");
                    }

                    LoggerInstance.LogInfo("ButtonClicked_Unlock method is patched.");
                    return false;
                }
                LoggerInstance.LogInfo("Skipped patching ButtonClicked_Unlock method.");
                return true;
            }
        }

        [HarmonyPatch(typeof(TNH_MagDuplicator), "Button_Upgrade")]
        class ModifyMagazineUpgradeCost
        {
            static bool Prefix(ref TNH_MagDuplicator __instance)
            {
                bool isModifiedMagCostsEnabled = ModifiedMagCostsEnabled.Value;
                if (isModifiedMagCostsEnabled == true)
                {
                    LoggerInstance.LogInfo("Beginning to patch Button_Upgrade method.");
                    int Cost_Value = 0;
                    string Cost_Type = MagUpgradeType.Value;
                    if (Cost_Type == "Flat")
                    {
                        Cost_Value = MagUpgradeValue.Value;
                    }
                    if (Cost_Type == "Multiplier")
                    {
                        Cost_Value = MagUpgradeValue.Value * 3;
                    }

                    if (__instance.M.GetNumTokens() < Cost_Value)
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                        return false;
                    }
                    FieldInfo m_detectedMagField = typeof(TNH_MagDuplicator).GetField("m_detectedMag", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_detectedMagField == null)
                    {
                        LoggerInstance.LogInfo("m_detectedMagField is null, something is wrong.");
                    }
                    FVRFireArmMagazine m_detectedmag = (FVRFireArmMagazine)m_detectedMagField.GetValue(__instance);
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
                        if (!(list[i].ItemID == m_detectedmag.ObjectWrapper.ItemID))
                        {
                            if (list[i].MagazineCapacity > m_detectedmag.m_capacity && list[i].MagazineCapacity < num)
                            {
                                fvrobject = list[i];
                                num = list[i].MagazineCapacity;
                            }
                        }
                    }
                    if (fvrobject != null)
                    {
                        LoggerInstance.LogInfo("Tokens you would pay for upgrading magazine without this mod: 3");
                        LoggerInstance.LogInfo("Tokens you are going to pay for upgrading magazine with this mod: " + Cost_Value);
                        __instance.M.SubtractTokens(Cost_Value);
                        UnityEngine.Object.Destroy(m_detectedmag.GameObject);
                        GameObject g = UnityEngine.Object.Instantiate<GameObject>(fvrobject.GetGameObject(), __instance.Spawnpoint_Mag.position, __instance.Spawnpoint_Mag.rotation);
                        __instance.M.AddObjectToTrackedList(g);
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Spawn, __instance.transform.position);
                    }

                    LoggerInstance.LogInfo("Button_Upgrade method is patched.");
                    return false;
                }
                LoggerInstance.LogInfo("Skipped patching Button_Upgrade method.");
                return true;
            }
        }

        [HarmonyPatch(typeof(TNH_MagDuplicator), "Button_Duplicate")]
        class ModifyMagazineCloneCost
        {
            static bool Prefix(ref TNH_MagDuplicator __instance)
            {
                bool isModifiedMagCostsEnabled = ModifiedMagCostsEnabled.Value;
                if (isModifiedMagCostsEnabled == true)
                {
                    LoggerInstance.LogInfo("Beginning to patch Button_Duplicate method.");

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

                    FieldInfo m_detectedSLField = typeof(TNH_MagDuplicator).GetField("m_detectedSL", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_detectedMagField == null)
                    {
                        LoggerInstance.LogInfo("m_detectedSLField is null, something is wrong.");
                    }
                    Speedloader m_detectedsl = (Speedloader)m_detectedSLField.GetValue(__instance);
                    if (m_detectedsl == null)
                    {
                        LoggerInstance.LogInfo("m_detectedsl is null, something is wrong.");
                    }

                    FieldInfo m_storedDupeCostField = typeof(TNH_MagDuplicator).GetField("m_storedDupeCost", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (m_detectedMagField == null)
                    {
                        LoggerInstance.LogInfo("m_storedDupeCostField is null, something is wrong.");
                    }
                    int m_storeddupecost = (int)m_storedDupeCostField.GetValue(__instance);
                    if (m_storeddupecost == null)
                    {
                        LoggerInstance.LogInfo("m_storeddupecost is null, something is wrong.");
                    }

                    if (m_detectedmag == null && m_detectedsl == null)
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                        return false;
                    }
                    if (m_detectedmag != null && m_detectedmag.IsEnBloc)
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                        return false;
                    }
                    if (m_storeddupecost < 1)
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                        return false;
                    }

                    LoggerInstance.LogInfo("Tokens you would pay for cloning magazine without this mod:" + m_storeddupecost);
                    int Cost_Value = 0;
                    string Cost_Type = MagCloneType.Value;
                    if (Cost_Type == "Flat")
                    {
                        Cost_Value = MagCloneValue.Value;
                    }
                    if (Cost_Type == "Multiplier")
                    {
                        Cost_Value = MagCloneValue.Value * m_storeddupecost;
                    }
                    LoggerInstance.LogInfo("Tokens you are going to pay for cloning magazine with this mod:" + Cost_Value);

                    if (__instance.M.GetNumTokens() >= Cost_Value)
                    {
                        SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Spawn, __instance.transform.position);
                        __instance.M.SubtractTokens(Cost_Value);
                        if (m_detectedmag != null)
                        {
                            FVRObject objectWrapper = m_detectedmag.ObjectWrapper;
                            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(objectWrapper.GetGameObject(), __instance.Spawnpoint_Mag.position, __instance.Spawnpoint_Mag.rotation);
                            __instance.M.AddObjectToTrackedList(gameObject);
                            FVRFireArmMagazine component = gameObject.GetComponent<FVRFireArmMagazine>();
                            for (int i = 0; i < Mathf.Min(m_detectedmag.LoadedRounds.Length, component.LoadedRounds.Length); i++)
                            {
                                if (m_detectedmag.LoadedRounds[i] != null && m_detectedmag.LoadedRounds[i].LR_Mesh != null)
                                {
                                    component.LoadedRounds[i].LR_Class = m_detectedmag.LoadedRounds[i].LR_Class;
                                    component.LoadedRounds[i].LR_Mesh = m_detectedmag.LoadedRounds[i].LR_Mesh;
                                    component.LoadedRounds[i].LR_Material = m_detectedmag.LoadedRounds[i].LR_Material;
                                    component.LoadedRounds[i].LR_ObjectWrapper = m_detectedmag.LoadedRounds[i].LR_ObjectWrapper;
                                }
                            }
                            component.m_numRounds = m_detectedmag.m_numRounds;
                            component.UpdateBulletDisplay();
                        }
                        else if (m_detectedsl != null)
                        {
                            FVRObject objectWrapper = m_detectedsl.ObjectWrapper;
                            GameObject gameObject2 = UnityEngine.Object.Instantiate<GameObject>(objectWrapper.GetGameObject(), __instance.Spawnpoint_Mag.position, __instance.Spawnpoint_Mag.rotation);
                            __instance.M.AddObjectToTrackedList(gameObject2);
                            Speedloader component2 = gameObject2.GetComponent<Speedloader>();
                            for (int j = 0; j < m_detectedsl.Chambers.Count; j++)
                            {
                                if (m_detectedsl.Chambers[j].IsLoaded)
                                {
                                    component2.Chambers[j].Load(m_detectedsl.Chambers[j].LoadedClass, false);
                                }
                                else
                                {
                                    component2.Chambers[j].Unload();
                                }
                            }
                        }
                        LoggerInstance.LogInfo("Button_Duplicate method is patched.");
                        return false;
                    }
                    SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                    return false;
                }
                LoggerInstance.LogInfo("Skipped patching Button_Duplicate method.");
                return true;
            }
        }
    }
}
