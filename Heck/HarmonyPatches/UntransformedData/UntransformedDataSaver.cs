#if !PRE_V1_37_1
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using IPA.Utilities;

namespace Heck.HarmonyPatches.UntransformedData;

[HeckPatch]
public class HeckGameplayCoreSceneSetupData : GameplayCoreSceneSetupData
{
    private static readonly MethodInfo _heckType = AccessTools.Method(
        typeof(HeckGameplayCoreSceneSetupData),
        nameof(HeckGetType));

    private static readonly FieldAccessor<GameplayCoreSceneSetupData, BeatmapLevelsModel>.Accessor
        _beatmapLevelsModelAccessor =
            FieldAccessor<GameplayCoreSceneSetupData, BeatmapLevelsModel>.GetAccessor(nameof(_beatmapLevelsModel));

    private IReadonlyBeatmapData? _untransformedBeatmapData;
    internal void SetUntransformedData(IReadonlyBeatmapData data) => _untransformedBeatmapData = data;

    public HeckGameplayCoreSceneSetupData(
        GameplayCoreSceneSetupData original)
        : base(
            original.beatmapKey,
            original.beatmapLevel,
            original.gameplayModifiers,
            original.playerSpecificSettings,
            original.practiceSettings,
#if PRE_V1_44_1
            original.useTestNoteCutSoundEffects,
#endif
#if !PRE_V1_40_8
            original.targetEnvironmentInfo,
            original.originalEnvironmentInfo,
#else
            original.environmentInfo,
#endif
            original.colorScheme,
#if !PRE_V1_40_8
            original._settingsManager,
#elif V1_37_1
            original._performancePreset,
#endif
            original._audioClipAsyncLoader,
            original._beatmapDataLoader,
            original._beatmapLevelsEntitlementModel,
            original._enableBeatmapDataCaching,
#if !PRE_V1_44_1
            original.environmentsListModel,
            original._allowNullBeatmapLevelData,
            original._beatmapLevelsModel,
    #if LATEST
            original.beatmapLevelData)
    #else
            original.beatmapLevelData,
            original.recordingToolData)
    #endif
#else
            original._allowNullBeatmapLevelData,
    #if !PRE_V1_40_8
            original.environmentsListModel,
    #endif
            original.recordingToolData)
#endif
    {
        GameplayCoreSceneSetupData @this = this;
        _beatmapLevelsModelAccessor(ref @this) = original._beatmapLevelsModel;
        beatmapLevelData = original.beatmapLevelData;
    }

    public IReadonlyBeatmapData UntransformedBeatmapData =>
        _untransformedBeatmapData ??
        throw new InvalidOperationException($"[{nameof(_untransformedBeatmapData)}] was null.");

    private static Type HeckGetType(Type original)
    {
        return original == typeof(HeckGameplayCoreSceneSetupData) ? typeof(GameplayCoreSceneSetupData) : original;
    }

    // i hate gettype i hate gettype i hate gettype
    [HarmonyTranspiler]
#if LATEST
    [HarmonyPatch(typeof(ScenesTransitionSetupData), nameof(ScenesTransitionSetupData.InstallBindings))]
#else
    [HarmonyPatch(typeof(ScenesTransitionSetupDataSO), nameof(ScenesTransitionSetupDataSO.InstallBindings))]
#endif
    private static IEnumerable<CodeInstruction> HeckOff(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            /*
             * -- Type type = sceneSetupData.GetType();
             * ++ Type type = HeckGetType(sceneSetupData.GetType());
             */
            .MatchForward(false, new CodeMatch(OpCodes.Stloc_3))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, _heckType))
            .InstructionEnumeration();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameplayCoreSceneSetupData), nameof(LoadTransformedBeatmapDataAsync))]
    private static void CaptureLoadResult(
        GameplayCoreSceneSetupData __instance,
        ref Task __result)
    {
        if (__instance is HeckGameplayCoreSceneSetupData setup)
            __result = CaptureRawAfterLoad(setup, __result);
    }

    private static async Task CaptureRawAfterLoad(HeckGameplayCoreSceneSetupData setup, Task load)
    {
        await load;
        if (setup.beatmapLevelData == null) throw new InvalidOperationException("Beatmap level data was not loaded: " + setup.beatmapKey);
        var key = setup.beatmapKey;
        IReadonlyBeatmapData? raw = await setup._beatmapDataLoader.LoadBeatmapDataAsync(
            setup.beatmapLevelData, key, setup.beatmapLevel.beatsPerMinute,
            setup.beatmapLevel.GetEnvironmentName(key.characteristic, key.difficulty) == setup.targetEnvironmentInfo.serializedName,
            setup.targetEnvironmentInfo, setup.originalEnvironmentInfo, BeatmapLevelDataVersion.Original,
            setup.gameplayModifiers, setup.playerSpecificSettings);
        setup._untransformedBeatmapData = raw ?? throw new InvalidOperationException("Raw beatmap data was not loaded: " + key);
    }
}
#endif
