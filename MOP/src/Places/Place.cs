// Modern Optimization Plugin
// Copyright(C) 2019-2022 Athlon

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.If not, see<http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using MOP.FSM;
using MOP.Common;
using MOP.Rules;
using MOP.Rules.Types;

namespace MOP.Places
{
    class Place
    {
        readonly GameObject gameObject;
        internal GameObject ThisGameObject => gameObject;
        protected internal Transform transform => gameObject != null ? gameObject.transform : null;
        readonly float toggleDistance;

        // Objects from that list will not be disabled.
        internal List<string> GameObjectBlackList;

        /// <summary>
        /// List of childs that are allowed to be disabled
        /// </summary>
        internal List<Transform> DisableableChilds;
        internal List<PlayMakerFSM> PlayMakers;
        internal List<Light> LightSources;

        /// <summary>
        /// Saves what value has been last used, to prevent unnescesary launch of loop.
        /// </summary>
        protected bool isActive = true;

        /// <summary>
        /// Initialize the Store class
        /// </summary>
        public Place(string placeName, float distance = 200)
        {
            gameObject = GameObject.Find(placeName);

            toggleDistance = distance;
            GameObjectBlackList = new List<string>();
            PlayMakers = new List<PlayMakerFSM>();
            LightSources = new List<Light>();
            DisableableChilds = new List<Transform>();

            if (gameObject == null)
            {
                MSCLoader.ModConsole.LogWarning($"[MOP] Place {placeName} not found!");
                return;
            }

            IgnoreRuleAtPlace[] ignoreRulesAtThisPlace = RulesManager.Instance.GetList<IgnoreRuleAtPlace>().Where(r => r.Place == placeName).ToArray();
            if (ignoreRulesAtThisPlace.Length > 0)
                foreach (IgnoreRuleAtPlace rule in ignoreRulesAtThisPlace)
                    GameObjectBlackList.Add(rule.ObjectName);
        }

        /// <summary>
        /// Enable or disable the place
        /// </summary>
        /// <param name="enabled"></param>
        public virtual void ToggleActive(bool enabled)
        {
            if (gameObject == null) return;
            // Don't execute the code, if the enabled value is the same as the activity status.
            if (isActive == enabled) 
                return;

            isActive = enabled;

            if (DisableableChilds != null)
            {
                // Load and unload only the objects that aren't on the whitelist.
                for (int i = 0; i < DisableableChilds.Count; i++)
                {
                    // If the object is missing, skip and continue.
                    if (DisableableChilds[i] == null)
                        continue;

                    DisableableChilds[i].gameObject.SetActive(enabled);
                }
            }

            if (PlayMakers != null && PlayMakers.Count > 0)
            {
                for (int i = 0; i < PlayMakers.Count; i++)
                {
                    if (PlayMakers[i] == null)
                    {
                        continue;
                    }

                    PlayMakers[i].enabled = enabled;
                }
            }

            if (LightSources != null && LightSources.Count > 0)
            {
                if (FsmManager.ShadowsHouse)
                {
                    for (int i = 0; i < LightSources.Count; ++i)
                    {
                        if (LightSources[i] != null)
                        {
                            LightSources[i].shadows = enabled ? LightShadows.Hard : LightShadows.None;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns all childs of the object.
        /// </summary>
        /// <returns></returns>
        internal List<Transform> GetDisableableChilds()
        {
            if (gameObject == null) return new List<Transform>();
            return gameObject.GetComponentsInChildren<Transform>(true)
                .Where(trans => trans != null && trans.gameObject != null && !trans.gameObject.name.ContainsAny(GameObjectBlackList)).ToList();
        }

        /// <summary>
        /// Returns the gameo object name.
        /// </summary>
        /// <returns></returns>
        public string GetName()
        {
            return gameObject != null ? gameObject.name : string.Empty;
        }

        /// <summary>
        /// Returns toggling distance of the place.
        /// </summary>
        /// <returns></returns>
        public float GetToggleDistance()
        {
            return toggleDistance;
        }

        internal void Compress()
        {
            if (DisableableChilds == null) return;
            List<Transform> toRemove = new List<Transform>();
            foreach (Transform child in DisableableChilds)
            {
                if (child == null) continue;
                Transform currentChild = child;
                try
                {
                    while (currentChild.parent != null)
                    {
                        if (DisableableChilds.Contains(currentChild.parent))
                        {
                            toRemove.Add(child);
                        }

                        currentChild = currentChild.parent;
                    }
                }
                catch 
                {
                    if (currentChild != null) ModConsole.LogError(currentChild.Path());
                }
            }

            foreach (Transform child in toRemove)
            {
                DisableableChilds.Remove(child);
            }
        }

        internal List<Light> GetLightSources()
        {
            if (gameObject == null) return new List<Light>();
            return Resources.FindObjectsOfTypeAll<GameObject>()
                   .Where(g => g != null && g.transform != null && g.transform.root == gameObject.transform && g.name.StartsWith("Light") && g.GetComponent<Light>())
                   .Select(g => g.GetComponent<Light>()).ToList();
        }

        public bool IsActive => isActive;
    }
}
