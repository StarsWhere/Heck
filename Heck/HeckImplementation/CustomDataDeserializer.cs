using System;
using System.Collections.Generic;
using CustomJSONData;
using CustomJSONData.CustomBeatmap;
using Heck.Animation;
using Heck.Deserialize;
using static Heck.HeckController;

namespace Heck;

internal class CustomDataDeserializer : IEarlyDeserializer, IObjectsDeserializer, ICustomEventsDeserializer
{
    private readonly CustomBeatmapData _beatmapData;
    private readonly float _bpm;
    private readonly Dictionary<string, List<object>> _pointDefinitions;
    private readonly Dictionary<string, Track> _tracks;
    private readonly TrackBuilder _trackBuilder;

    private CustomDataDeserializer(
        CustomBeatmapData beatmapData,
        Dictionary<string, List<object>> pointDefinitions,
        Dictionary<string, Track> tracks,
        float bpm,
        TrackBuilder trackBuilder)
    {
        _beatmapData = beatmapData;
        _pointDefinitions = pointDefinitions;
        _tracks = tracks;
        _bpm = bpm;
        _trackBuilder = trackBuilder;
    }

    public void DeserializeEarly()
    {
        // Animation events can introduce a track before any object is assigned to it.
        foreach (CustomEventData customEvent in _beatmapData.customEventDatas)
            if (customEvent.eventType is ANIMATE_TRACK or ASSIGN_PATH_ANIMATION)
                _trackBuilder.AddManyFromCustomData(customEvent.customData, customEvent.version.IsVersion2());
    }

    public Dictionary<CustomEventData, ICustomEventCustomData> DeserializeCustomEvents()
    {
        Dictionary<CustomEventData, ICustomEventCustomData> dictionary = new();
        foreach (CustomEventData customEventData in _beatmapData.customEventDatas)
        {
            bool v2 = customEventData.version.IsVersion2();
            try
            {
                switch (customEventData.eventType)
                {
                    case ANIMATE_TRACK:
                    case ASSIGN_PATH_ANIMATION:
                        dictionary.Add(
                            customEventData,
                            new HeckCoroutineEventData(customEventData, _pointDefinitions, _tracks, v2));
                        break;

                    case INVOKE_EVENT:
                        if (v2)
                        {
                            break;
                        }

                        dictionary.Add(customEventData, new HeckInvokeEventData(_beatmapData, customEventData));
                        break;

                    default:
                        continue;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.DeserializeFailure(e, customEventData, _bpm);
            }
        }

        return dictionary;
    }

    public Dictionary<BeatmapObjectData, IObjectCustomData> DeserializeObjects()
    {
        Dictionary<BeatmapObjectData, IObjectCustomData> dictionary = new();
        foreach (BeatmapObjectData beatmapObjectData in _beatmapData.beatmapObjectDatas)
        {
            CustomData customData = ((ICustomData)beatmapObjectData).customData;
            bool v2;
            if (beatmapObjectData is IVersionable versionable)
            {
                v2 = versionable.version.IsVersion2();
            }
            else
            {
                v2 = false;
            }

            dictionary.Add(beatmapObjectData, new HeckObjectData(beatmapObjectData, customData, _tracks, _bpm, v2));
        }

        return dictionary;
    }
}
