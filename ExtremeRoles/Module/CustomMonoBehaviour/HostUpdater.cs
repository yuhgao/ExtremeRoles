﻿using System.Collections.Generic;

using UnityEngine;

using ExtremeRoles.Module.Interface;

namespace ExtremeRoles.Module.CustomMonoBehaviour
{
    [Il2CppRegister]
    public sealed class HostObjectUpdater : MonoBehaviour
    {
        private List<IUpdatableObject> updateObject = new List<IUpdatableObject>();

        public void Awake()
        {
            updateObject.Clear();
        }

        public void AddObject(IUpdatableObject obj)
        {
            updateObject.Add(obj);
        }

        public void RemoveObject(int index)
        {
            updateObject[index].Clear();
            updateObject.RemoveAt(index);
        }

        public void RemoveObject(IUpdatableObject obj)
        {
            obj.Clear();
            updateObject.Remove(obj);
        }

        public IUpdatableObject GetObject(int index) => updateObject[index];

        public void Update()
        {
            if (!AmongUsClient.Instance.AmHost) { return; }

            for (int i = 0; i < updateObject.Count; i++)
            {
                updateObject[i].Update(i);
            }
        }
    }
}
