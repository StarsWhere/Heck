using System;
using System.Collections.Generic;
using System.Linq;
using CustomJSONData.CustomBeatmap;
using HarmonyLib;
using Heck.Animation;
using Heck.Deserialize;
using static Heck.HeckController;

namespace Heck;

internal class HeckObjectData : IObjectCustomData, ICopyable<IObjectCustomData>
{
    internal HeckObjectData(
        BeatmapObjectData beatmapObjectData,
        CustomData customData,
        Dictionary<string, Track> beatmapTracks,
        float bpm,
        bool v2)
    {
        try
        {
            Track = customData.GetNullableTrackArray(beatmapTracks, v2)?.ToList();
        }
        catch (Exception e)
        {
            Plugin.Log.DeserializeFailure(e, beatmapObjectData, bpm);
        }
    }

    private HeckObjectData(HeckObjectData original)
    {
        Track = original.Track;
    }

    internal IReadOnlyList<Track>? Track { get; }

    public IObjectCustomData Copy()
    {
        return new HeckObjectData(this);
    }
}

internal class HeckCoroutineEventData : ICustomEventCustomData
{
    private readonly string[] _excludedStrings = [V2_TRACK, V2_DURATION, V2_EASING, TRACK, DURATION, EASING, REPEAT];

    internal HeckCoroutineEventData(
        CustomEventData customEventData,
        Dictionary<string, List<object>> pointDefinitions,
        Dictionary<string, Track> beatmapTracks,
        bool v2)
    {
        CustomData data = customEventData.customData;

        Track[] tracks = data.GetTrackArray(beatmapTracks, v2).ToArray();

        IEnumerable<string> propertyKeys = data.Keys.Where(n => _excludedStrings.All(m => m != n)).ToArray();
        List<CoroutineInfo> coroutineInfos = [];
        bool path = customEventData.eventType switch
        {
            ANIMATE_TRACK => false,
            ASSIGN_PATH_ANIMATION => true,
            _ => throw new InvalidOperationException("Custom event was not of correct type.")
        };

        foreach (string propertyKey in propertyKeys)
        {
            IEnumerable<string>? names = v2
                ? path ? Track.GetPathAliases(propertyKey) : Track.GetAliases(propertyKey)
                : [propertyKey];
            bool supported = false;
            if (names != null)
            {
                foreach (string name in names)
                {
                    IPropertyBuilder? builder = path ? Track.GetPathBuilder(name) : Track.GetBuilder(name);
                    if (builder == null)
                    {
                        continue;
                    }

                    supported = true;
                    foreach (Track track in tracks)
                    {
                        BaseProperty property = path
                            ? track.GetOrCreatePathProperty(name, builder)
                            : track.GetOrCreateProperty(name, builder);
                        coroutineInfos.Add(new HeckCoroutineEventData.CoroutineInfo(
                            builder.GetPointData(data, v2 ? propertyKey : name, pointDefinitions),
                            property,
                            track));
                    }
                }
            }

            if (!supported)
            {
                Plugin.Log.Warn(
                    $"Unsupported property [{propertyKey}] in [{customEventData.eventType}] at [{customEventData.time}] seconds; preserving supported animation properties.");
            }
        }

        Duration = data.Get<float?>(v2 ? V2_DURATION : DURATION) ?? 0f;
        Easing = data.GetStringToEnum<Functions?>(v2 ? V2_EASING : EASING) ?? Functions.easeLinear;
        CoroutineInfos = coroutineInfos;

        if (!v2)
        {
            Repeat = data.Get<int?>(REPEAT) ?? 0;
        }
    }

    internal IReadOnlyList<CoroutineInfo> CoroutineInfos { get; }

    internal float Duration { get; }

    internal Functions Easing { get; }

    internal int Repeat { get; }

    internal readonly struct CoroutineInfo
    {
        internal CoroutineInfo(IPointDefinition? pointDefinition, BaseProperty property, Track track)
        {
            PointDefinition = pointDefinition;
            Property = property;
            Track = track;
        }

        internal IPointDefinition? PointDefinition { get; }

        internal BaseProperty Property { get; }

        internal Track Track { get; }
    }
}

internal class HeckInvokeEventData : ICustomEventCustomData
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    internal HeckInvokeEventData(CustomBeatmapData beatmapData, CustomEventData customEventData)
    {
        IDictionary<string, CustomEventData> eventDefinitions =
            beatmapData.customData.GetRequired<IDictionary<string, CustomEventData>>(EVENT_DEFINITIONS);
        string eventName = customEventData.customData.GetRequired<string>(EVENT);
        CustomEventData = eventDefinitions[eventName];
    }

    internal CustomEventData CustomEventData { get; }
}
