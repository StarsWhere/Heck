using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chroma.Lighting;

internal static class LightRegistry
{
    internal static LightWithIdManager.LightMapData Get(LightWithIdManager manager, int id)
    {
        if ((uint)id >= (uint)manager._oldMapping.Length) throw new ArgumentOutOfRangeException(nameof(id));
        return manager._oldMapping[id] ??= new LightWithIdManager.LightMapData
        {
            lightInstances = Array.Empty<ILightWithId>(), color = Color.clear, hasColor = false
        };
    }

    internal sealed class View : IReadOnlyList<ILightWithId>
    {
        private readonly LightWithIdManager _manager;
        private readonly int _id;
        internal View(LightWithIdManager manager, int id) { _manager = manager; _id = id; Get(manager, id); }
        public int Count => Get(_manager, _id).lightInstances.Length;
        public ILightWithId this[int index] => Get(_manager, _id).lightInstances[index];
        public IEnumerator<ILightWithId> GetEnumerator() => ((IEnumerable<ILightWithId>)Get(_manager, _id).lightInstances).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
