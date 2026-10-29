using System.Collections.Generic;
using Chroma.Colorizer;
using Chroma.Lighting;
using SiraUtil.Affinity;
using UnityEngine;
using System;

namespace Chroma.HarmonyPatches.Colorizer.Initialize;

internal class LightWithIdRegisterer : IAffinity
{
    private readonly LightColorizerManager _colorizerManager;
    private readonly LightWithIdManager _lightWithIdManager;
    private readonly HashSet<ILightWithId> _needToRegister = [];
    private readonly Dictionary<ILightWithId, int> _requestedIds = new();
    private readonly LightIDTableManager _tableManager;

    private LightWithIdRegisterer(
        LightColorizerManager colorizerManager,
        LightWithIdManager lightWithIdManager,
        LightIDTableManager tableManager)
    {
        _colorizerManager = colorizerManager;
        _lightWithIdManager = lightWithIdManager;
        _tableManager = tableManager;
    }

    internal void ForceUnregister(ILightWithId lightWithId)
    {
        int lightId = lightWithId.lightId;
        if (lightId < 0 || lightId >= _lightWithIdManager._oldMapping.Length) return;
        ILightWithId[] lights = LightRegistry.Get(_lightWithIdManager, lightId).lightInstances;
        int index = Array.IndexOf(lights, lightWithId);
        if (index >= 0)
        {
            lights[index] = null!;
            _tableManager.UnregisterIndex(lightId, index);
        }
        if (lightWithId.groupId >= 0 && lightWithId.elementId >= 0) _lightWithIdManager.UnregisterLight(lightWithId);
        _colorizerManager.CreateLightColorizerContractByLightID(
            lightId,
            n => n.ChromaLightSwitchEventEffect.UnregisterLight(lightWithId));
        lightWithId.__SetIsUnRegistered();
    }

    internal void MarkForTableRegister(ILightWithId lightWithId)
    {
        _needToRegister.Add(lightWithId);
    }

    internal void SetRequestedId(ILightWithId lightWithId, int id)
    {
        _requestedIds[lightWithId] = id;
    }

    // too lazy to make a transpiler
    [AffinityPrefix]
    [AffinityPatch(typeof(LightWithIdManager), nameof(LightWithIdManager.SetColorForId),
        AffinityMethodType.Normal, null, typeof(LightWithIdManager.LightMapData), typeof(Color))]
    private bool AllowNull(
        LightWithIdManager.LightMapData lightMapData,
        Color color,
        ref bool ____didChangeSomeColorsThisFrame)
    {
        lightMapData.color = color;
        lightMapData.hasColor = true;
        ____didChangeSomeColorsThisFrame = true;
        foreach (ILightWithId light in lightMapData.lightInstances)
            if (light is { isRegistered: true }) light.ColorWasSet(color);
        return false;
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(LightWithIdManager), nameof(LightWithIdManager.UnregisterLight))]
    private bool DontClearList(ILightWithId lightWithId)
    {
        if (lightWithId.groupId >= 0 && lightWithId.elementId >= 0) return true;
        lightWithId.__SetIsUnRegistered();
        return false;
    }

    [AffinityPrefix]
    [AffinityPatch(typeof(LightWithIdManager), nameof(LightWithIdManager.RegisterLight))]
    private void Prefix(
        ref bool __runOriginal,
        LightWithIdManager __instance,
        ILightWithId lightWithId)
    {
        // TODO: figure this shit out
        // for some reason, despite being an affinity patch bound to player, this still runs in the menu scene
        // so quick and dirty fix
        if (__instance.gameObject.scene.name.Contains("Menu"))
        {
            return;
        }

        // Group/element registration remains owned by the native v4 lighting path.
        if (lightWithId.groupId >= 0 && lightWithId.elementId >= 0) return;

        __runOriginal = false;

        if (lightWithId.isRegistered)
        {
            return;
        }

        int lightId = lightWithId.lightId;
        if (lightId < 0 || lightId >= __instance._oldMapping.Length)
        {
            return;
        }

        LightWithIdManager.LightMapData data = LightRegistry.Get(__instance, lightId);
        ILightWithId[] lights = data.lightInstances;

        lightWithId.__SetIsRegistered();

        if (Array.IndexOf(lights, lightWithId) >= 0)
        {
            return;
        }

        // TODO: find a better way to register "new" lights to table
        int index = lights.Length;
        if (_needToRegister.Remove(lightWithId))
        {
            int? tableId = _requestedIds.TryGetValue(lightWithId, out int value) ? value : null;
            _tableManager.RegisterIndex(lightId, index, tableId);
        }

        // this also colors the light
        _colorizerManager.CreateLightColorizerContractByLightID(
            lightId,
            n => n.ChromaLightSwitchEventEffect.RegisterLight(lightWithId, index));

        Array.Resize(ref lights, index + 1);
        lights[index] = lightWithId;
        data.lightInstances = lights;
        lightWithId.ColorWasSet(data.hasColor ? data.color : Color.clear);
    }
}
