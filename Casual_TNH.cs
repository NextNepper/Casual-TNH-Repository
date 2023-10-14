using System.Reflection;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using FistVR;


[BepInPlugin("Nepper.CasualTNH", "Casual TNH", "1.0.0")]
public class Casual_TNH : BaseUnityPlugin
{
    private static Casual_TNH casual_tnh;
    private static ManualLogSource LoggerInstance;
    private static ConfigEntry<int> TokenMultiplier;
    private static ConfigEntry<bool> ShortenAnalyzePhases;
    private static ConfigEntry<bool> IsRerollFree;
    private static ConfigEntry<bool> ModifiedMagCostsEnabled;
    private static ConfigEntry<string> MagUpgradeType;
    private static ConfigEntry<int> MagUpgradeValue;
    private Harmony harmony;

    void Awake()
    {
        casual_tnh = this;
        LoggerInstance = Logger;
        TokenMultiplier = Config.Bind("Token Multiplier", "Token gains multiplied by: ", 5, "The multiplier for token modification. Set it to 1 if you don't want to use this. Integer values only.");
        ShortenAnalyzePhases = Config.Bind("Shorter Analyze Phases", "Enabled: ", true, "If analyze phases should be shortened or not. Set it to false if you don't want to use this. Boolean values only.");
        IsRerollFree = Config.Bind("Reroll Costs", "Rerolls cost nothing: ", true, "If rerolls should be free or not. Set it to false if you don't want to use this. Boolean values only.");
        ModifiedMagCostsEnabled = Config.Bind("Magazine Upgrade Costs", "Change magazine upgrade costs: ", true, "If mod's magazine upgrade feature should be enabled or not. Set it to false if you don't want to use this feature of the mod. Boolean values only.");
        MagUpgradeType = Config.Bind("Magazine Costs", "Magazine upgrade cost type: ", "Flat", "How you want to use \"Magazine upgrade cost\" value to calculate magazine upgrade costs. Set it to either \"Flat\" or \"Multiplier\". String values only.");
        MagUpgradeValue = Config.Bind("Magazine Costs", "Magazine upgrade cost: ", 0, "The value to used for calculating magazine upgrade costs. Integer values only.");
        harmony = new Harmony("Nepper.CasualTNH");
        harmony.PatchAll();
    }

    [HarmonyPatch(typeof(TNH_Manager), "AddTokens")]
    class TokenModifierPatch
    {
        static bool Prefix(ref TNH_Manager __instance, int i, bool Scorethis)
        {
            // Successfully patched AddTokens method
            LoggerInstance.LogInfo("Casual TNH: Patching AddTokens method!");

            // Read the multiplier value from the configuration
            int Modifier = TokenMultiplier.Value;

            // Use reflection to access and modify the private variable m_numTokens
            FieldInfo m_numTokensField = typeof(TNH_Manager).GetField("m_numTokens", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m_numTokensField != null)
            {
                int? currentNullableValue = (int?)m_numTokensField.GetValue(__instance);
                if (currentNullableValue.HasValue)
                {
                    int currentValue = currentNullableValue.Value;
                    LoggerInstance.LogInfo("Casual TNH: Token's you'd get without this mod: " + i);
                    currentValue += i * Modifier;
                    LoggerInstance.LogInfo("Casual TNH: Token's you are going to get with this mod: " + i * Modifier);
                    m_numTokensField.SetValue(__instance, currentValue);
                    if (Scorethis)
                    {
                        __instance.Increment(8, i, false);
                    }
                    __instance.OnTokenCountChange(currentValue);
                }
            }
            LoggerInstance.LogInfo("Casual TNH: Patched AddTokens method!");
            
            return false;
        }
    }

    [HarmonyPatch(typeof(TNH_HoldPoint), "BeginAnalyzing")]
    class ModifyBeginAnalyzingPatch
    {
        static bool Prefix(ref TNH_HoldPoint __instance)
        {
            bool ShorterAnalyze = ShortenAnalyzePhases.Value;
            if (ShorterAnalyze)
            {
                LoggerInstance.LogInfo("Casual TNH: Beginning to patch BeginAnalyzing method.");

                __instance.M.EnqueueLine(TNH_VoiceLineID.AI_AnalyzingSystem);

                FieldInfo m_stateField = typeof(TNH_HoldPoint).GetField("m_state", BindingFlags.NonPublic | BindingFlags.Instance);
                m_stateField.SetValue(__instance, TNH_HoldPoint.HoldState.Analyzing);

                FieldInfo m_curPhaseField = typeof(TNH_Manager).GetField("m_curPhase", BindingFlags.NonPublic | BindingFlags.Instance);
                TNH_HoldChallenge.Phase phaseInstance = (TNH_HoldChallenge.Phase)m_curPhaseField.GetValue(__instance);

                Type phaseType = typeof(TNH_HoldChallenge.Phase);
                float scantime = (float)phaseType.GetProperty("ScanTime").GetValue(phaseInstance);

                FieldInfo m_tickDownToIdentificationField = typeof(TNH_HoldPoint).GetField("m_tickDownToIdentification", BindingFlags.NonPublic | BindingFlags.Instance);
                m_tickDownToIdentificationField.SetValue(__instance, UnityEngine.Random.Range(scantime * 0.24f, scantime * 0.26f));
                if (__instance.M.TargetMode == TNHSetting_TargetMode.NoTargets)
                {
                    m_tickDownToIdentificationField.SetValue(__instance, UnityEngine.Random.Range(scantime * 0.24f, scantime * 0.26f) + 1f);
                }
                else if (__instance.M.IsBigLevel)
                {
                    float m_tickdowntoidentification = (float)m_tickDownToIdentificationField.GetValue(__instance);
                    m_tickdowntoidentification += 1f;
                    m_tickDownToIdentificationField.SetValue(__instance, m_tickdowntoidentification);
                }

                __instance.SpawnPoints_Targets.Shuffle<Transform>();

                FieldInfo m_validSpawnPointsField = typeof(TNH_HoldPoint).GetField("m_validSpawnPoints", BindingFlags.NonPublic | BindingFlags.Instance);
                List<Transform> m_validspawnpoints = (List<Transform>)m_validSpawnPointsField.GetValue(__instance);
                m_validspawnpoints.Shuffle<Transform>();
                m_validSpawnPointsField.SetValue(__instance, m_validspawnpoints);

                MethodInfo SpawnWarpInMarkersMethod = typeof(TNH_HoldPoint).GetMethod("SpawnWarpInMarkers", BindingFlags.NonPublic | BindingFlags.Instance);
                SpawnWarpInMarkersMethod.Invoke(__instance, null);

                FieldInfo m_systemNodeField = typeof(TNH_HoldPoint).GetField("m_systemNode", BindingFlags.NonPublic | BindingFlags.Instance);
                TNH_HoldPointSystemNode m_systemnode = (TNH_HoldPointSystemNode)m_systemNodeField.GetValue(__instance);
                m_systemnode.SetNodeMode(TNH_HoldPointSystemNode.SystemNodeMode.Analyzing);
                m_systemNodeField.SetValue(__instance, m_systemnode);

                LoggerInstance.LogInfo("Casual TNH: BeginAnalyzing method is patched.");

                return false;
            }
            LoggerInstance.LogInfo("Casual TNH: Skipped patching BeginAnalyzing method.");
            return true;
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
                LoggerInstance.LogInfo("Casual TNH: Beginning to patch ButtonClicked_Reroll method.");
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

                    return false;
                }
                SM.PlayCoreSound(FVRPooledAudioType.UIChirp, __instance.AudEvent_Fail, __instance.transform.position);
                LoggerInstance.LogInfo("Casual TNH: ButtonClicked_Reroll method is patched.");
                return false;
            }
            LoggerInstance.LogInfo("Casual TNH: Skipped patching ButtonClicked_Reroll method.");
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
                LoggerInstance.LogInfo("Casual TNH: Beginning to patch Button_Upgrade method.");
                string Cost_Type = MagUpgradeType.Value;
                int Cost_Value = MagUpgradeValue.Value;

                FieldInfo m_detectedMagField = typeof(TNH_MagDuplicator).GetField("m_detectedMag", BindingFlags.NonPublic | BindingFlags.Instance);
                FVRFireArmMagazine m_detectedmag = (FVRFireArmMagazine)m_detectedMagField.GetValue(__instance);
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

                LoggerInstance.LogInfo("Casual TNH: Button_Upgrade method is patched.");
                return false;
            }
            LoggerInstance.LogInfo("Casual TNH: Skipped patching Button_Upgrade method.");
            return true;
        }
    }
}