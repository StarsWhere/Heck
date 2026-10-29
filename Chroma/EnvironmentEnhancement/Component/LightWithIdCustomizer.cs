using System.Collections.Generic;
using System.Linq;
using Chroma.Colorizer;
using Chroma.HarmonyPatches.Colorizer.Initialize;
using CustomJSONData.CustomBeatmap;
using JetBrains.Annotations;
using SiraUtil.Logging;
using static Chroma.EnvironmentEnhancement.Component.ComponentConstants;

namespace Chroma.EnvironmentEnhancement.Component;

internal class LightWithIdCustomizer
{
    private readonly LightColorizerManager _lightColorizerManager;
    private readonly LightWithIdManager _lightWithIdManager;
    private readonly LightWithIdRegisterer _lightWithIdRegisterer;
    private readonly SiraLog _log;
    private readonly List<UnityEngine.Transform> _missingLights = new();

    [UsedImplicitly]
    private LightWithIdCustomizer(
        SiraLog log,
        LightColorizerManager lightColorizerManager,
        LightWithIdRegisterer lightWithIdRegisterer,
        LightWithIdManager lightWithIdManager)
    {
        _log = log;
        _lightColorizerManager = lightColorizerManager;
        _lightWithIdRegisterer = lightWithIdRegisterer;
        _lightWithIdManager = lightWithIdManager;
    }

    internal void LightWithIdInit(List<UnityEngine.Component> allComponents, CustomData customData, UnityEngine.Transform root)
    {
        int? lightID = customData.Get<int?>(LIGHT_ID);
        int? type = customData.Get<int?>(LIGHT_TYPE);
        if (!type.HasValue && !lightID.HasValue) return;

        ILightWithId[] lightWithIds = allComponents
            .OfType<LightWithIds>()
            .SelectMany(n => n._lightWithIds)
            .Cast<ILightWithId>()
            .Concat(allComponents.OfType<ILightWithId>())
            .Distinct()
            .ToArray();
        if (lightWithIds.Length == 0)
        {
            _missingLights.Add(root);
            return;
        }

        foreach (ILightWithId lightWithId in lightWithIds)
        {
            if (lightWithId.isRegistered)
            {
                _lightWithIdRegisterer.ForceUnregister(lightWithId);
                _lightWithIdRegisterer.MarkForTableRegister(lightWithId);
                SetType();
                SetLightID();
                _lightWithIdManager.RegisterLight(lightWithId);
            }
            else
            {
                SetType();
                SetLightID();
            }

            continue;

            void SetLightID()
            {
                if (lightID.HasValue)
                {
                    _lightWithIdRegisterer.SetRequestedId(lightWithId, lightID.Value);
                }
            }

            void SetType()
            {
                if (!type.HasValue)
                {
                    return;
                }

                int lightId = _lightColorizerManager.GetColorizer((BasicBeatmapEventType)type.Value)
                    .ChromaLightSwitchEventEffect.LightsID;

                switch (lightWithId)
                {
                    case LightWithIds.LightWithId lightWithIdsLightWithId:
                        lightWithIdsLightWithId._lightId = lightId;
                        break;

                    case LightWithIdMonoBehaviour lightWithIdMonoBehaviour:
                        lightWithIdMonoBehaviour._ID = lightId;
                        break;
                }
            }
        }
    }

    internal void ValidateComponents()
    {
        foreach (UnityEngine.Transform root in _missingLights.Distinct())
        {
            if (root == null) continue;
            if (root.gameObject.activeInHierarchy)
                _log.Error($"No [{LIGHT_WITH_ID}] component found on active environment object [{root.name}]");
            else
                _log.Debug($"Ignored [{LIGHT_WITH_ID}] customization on removed environment object [{root.name}]");
        }
        _missingLights.Clear();
    }
}
