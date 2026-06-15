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
using UnityEngine;
using MSCLoader;

using MOP.Vehicles.Cases;
using MOP.Rules;
using MOP.FSM;

namespace MOP.Places
{
    class RepairShop : Place
    {
        readonly string[] blackList = 
        {
            "REPAIRSHOP", "JunkCar", "sats_burn_masse", "TireOld(Clone)", "Order", "JunkYardJob",
            "BoozeJob", "Spawn", "SatsumaSpawns", "SeatPivot", "DistanceTarget", "SpawnToRepair",
            "PartsDistanceTarget", "JunkCarSpawns", "Parts", "wheel_regul", "rpm gauge(Clone)",
            "Hook", "Jobs", "GearRatios", "Fix", "fix", "Job", "Polish", "Wheel", "Fill", "Rollcage",
            "Adjust", "GearLinkage", "Paintjob", "Windshield", "ToeAdjust", "Brakes", "Lifter", "Audio", "roll",
            "TireCatcher", "Ropes", "Note", "note", "inspection_desk 1", "Office", "Furniture",
            "Building", "office_floor", "coll", "wall_base", "JunkYardJob", "PayMoney", "100mk", "GaugeMeshTach",
            "gauge_glass_fbx", "Pivot", "needle", "Bolt", "bolt", "grille", "wheel_steel5", "gear_stick",
            "Platform", "Coll", "Buy", "Product", "Key(Clone)", "LOD", "repair_shop_walls", "repair_shop_roof_metal"
        };

        Transform cashRegister;

        /// <summary>
        /// Initialize the RepairShop class
        /// </summary>
        public RepairShop() : base("REPAIRSHOP", 250)
        {
            if (ThisGameObject == null) return;

            // Set junk car objects parent to null.
            for (int i = 1; ; i++)
            {
                Transform junk = transform.Find($"JunkCar{i}");
                if (junk == null) break;
                junk.parent = null;
            }

            GameObjectBlackList.AddRange(blackList);
            DisableableChilds = GetDisableableChilds();

            // Fix for Satsuma parts on shelves
            if (DisableableChilds != null)
            {
                List<Transform> productsMesh = DisableableChilds.FindAll(t => t != null && t.name == "mesh" && t.parent != null && t.parent.name.Contains("Product"));
                foreach (Transform product in productsMesh)
                {
                    DisableableChilds.Remove(product);
                }
            }

            // Fix Sphere Collider of the cash register.
            Transform registerTrans = transform.Find("LOD/Store/ShopCashRegister/Register");
            if (registerTrans != null)
            {
                SphereCollider registerCollider = registerTrans.gameObject.GetComponent<SphereCollider>();
                if (registerCollider != null)
                {
                    registerCollider.radius = 70;
                    Vector3 newBounds = registerCollider.center;
                    newBounds.x = 5;
                    registerCollider.center = newBounds;
                }
            }

            // Add collider to Fury.
            if (!RulesManager.Instance.SpecialRules.SkipFuryColliderFix)
            {
                Transform furyTrans = transform.Find("LOD/Vehicle/FURY");
                if (furyTrans != null)
                {
                    BoxCollider collBox0 = furyTrans.gameObject.AddComponent<BoxCollider>();
                    BoxCollider collBox1 = furyTrans.gameObject.AddComponent<BoxCollider>();
                    collBox0.center = new Vector3(0, 0, -.2f);
                    collBox0.size = new Vector3(2.2f, 1.3f, 5);
                    collBox1.center = new Vector3(0, 0, -.25f);
                    collBox1.size = new Vector3(1.4f, 2.3f, 1.1f);
                }
            }

            // Hooks a trigger for when Satsuma has been moved by the Fleetari, so it won't get teleported back.
            try
            {
                Transform order = transform.Find("Order");
                if (order != null && Satsuma.Instance != null)
                {
                    order.gameObject.FsmInject("Move Satsuma", Satsuma.Instance.FleetariIsMovingCar);
                }
            }
            catch (System.Exception ex)
            {
                ModConsole.Log("[MOP] Couldn't FsmHook RepairShop/Order object: " + ex.Message);
            }

            // Fix door resetting.
            Transform handle = transform.Find("LOD/Door/Handle");
            if (handle != null)
            {
                PlayMakerFSM doorFsm = handle.gameObject.GetComponent<PlayMakerFSM>();
                if (doorFsm != null && doorFsm.Fsm != null) doorFsm.Fsm.RestartOnEnable = false;
            }

            // Prevent alignment lasers from restarting and turning on automatically when repair shop is loaded/enabled.
            Transform toeAdjust = FindRecursive(transform, "ToeAdjust");
            if (toeAdjust != null)
            {
                foreach (PlayMakerFSM fsm in toeAdjust.gameObject.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (fsm != null && fsm.Fsm != null) fsm.Fsm.RestartOnEnable = false;
                }
            }

            Transform shopCash = transform.Find("LOD/Store/ShopCashRegister");
            cashRegister = shopCash != null ? shopCash : null;

            Compress();
        }

        private static Transform FindRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        public Transform GetCashRegister()
        {
            return cashRegister;
        }
    }
}
