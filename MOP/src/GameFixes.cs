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

using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using HutongGames.PlayMaker;

using MOP.Managers;
using MOP.Common;
using MOP.Common.Enumerations;
using MOP.FSM;
using MOP.FSM.Actions;
using MOP.Vehicles.Cases;
using MOP.Vehicles.Managers;
using MOP.Vehicles.Managers.SatsumaManagers;
using MOP.Helpers;

namespace MOP
{
    class GameFixes : MonoBehaviour
    {
        // This class fixes shit done by ToplessGun.
        // Also some caused by MOP itself...

        static GameFixes instance;
        public static GameFixes Instance { get => instance; }

        public bool HoodFixDone { get; private set; }

        public GameFixes()
        {
            instance = this;
        }

        public void MainFixes()
        {
            // GT Grille resetting fix.
            try
            {
                GameObject grille = GameObject.Find("grille gt(Clone)");
                if (grille != null)
                {
                    PlayMakerFSM[] gtGrille = grille.GetComponents<PlayMakerFSM>();
                    foreach (var fsm in gtGrille)
                        fsm.Fsm.RestartOnEnable = false;
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "GT_GRILLE_ERROR");
            }

            Transform buildings = null;
            Transform perajarvi = null;
            // Random fixes.
            // Find house of Teimo and detach it from Perajarvi, so it can be loaded and unloaded separately
            try
            {
                GameObject b = GameObject.Find("Buildings");
                if (b != null) buildings = b.transform;
                
                GameObject p = GameObject.Find("PERAJARVI");
                if (p != null) perajarvi = p.transform;
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, true, "PERAJARVI_FIXES_BUILDINGS_FIND_ERROR");
            }

            if (buildings != null && perajarvi != null)
            {
                SetParent(perajarvi, buildings, "HouseRintama3");
                SetParent(perajarvi, buildings, "HouseSmall3");
                SetParent(null, "CHURCHWALL");
                SetParent(null, "DINGONBIISI");

                // Perajarvi fixes for multiple objects with the same name.
                // Instead of being the part of Perajarvi, we're changing it to be the part of Buildings.
                Transform[] perajarviChilds = perajarvi.GetComponentsInChildren<Transform>();
                for (int i = 0; i < perajarviChilds.Length; i++)
                {
                    if (perajarviChilds[i] == null) continue;

                    string objName = perajarviChilds[i].gameObject.name;

                    if (objName.Contains("silo") || objName == "MailBox" || objName == "Greenhouse")
                    {
                        SetParent(buildings, perajarviChilds[i]);
                        continue;
                    }
                }

                // Fix for floppies at Jokke's new house
                while (perajarvi.transform.Find("TerraceHouse/diskette(itemx)") != null)
                {
                    Transform diskette = perajarvi.transform.Find("TerraceHouse/diskette(itemx)");
                    if (diskette != null && diskette.parent != null)
                    {
                        SetParent(null, diskette);
                    }
                    else
                    {
                        break;
                    }
                }

                // Fix for Jokke's house furnitures clipping through floor
                SetParent(perajarvi, null, "TerraceHouse/Colliders");
            }

            // Fix for cottage items disappearing when moved
            try
            {
                GameObject g1 = GameObject.Find("coffee pan(itemx)");
                if (g1 != null) g1.transform.parent = null;
                GameObject g2 = GameObject.Find("lantern(itemx)");
                if (g2 != null) g2.transform.parent = null;
                GameObject g3 = GameObject.Find("coffee cup(itemx)");
                if (g3 != null) g3.transform.parent = null;
                GameObject g4 = GameObject.Find("camera(itemx)");
                if (g4 != null) g4.transform.parent = null;
                GameObject g5 = GameObject.Find("fireworks bag(itemx)");
                if (g5 != null) g5.transform.parent = null;

                // Fix for fishing areas
                GameObject f1 = GameObject.Find("FishAreaAVERAGE");
                if (f1 != null) f1.transform.parent = null;
                GameObject f2 = GameObject.Find("FishAreaBAD");
                if (f2 != null) f2.transform.parent = null;
                GameObject f3 = GameObject.Find("FishAreaGOOD");
                if (f3 != null) f3.transform.parent = null;
                GameObject f4 = GameObject.Find("FishAreaGOOD2");
                if (f4 != null) f4.transform.parent = null;

                // Fix for items left on cottage chimney clipping through it on first load of cottage
                GameObject cottageObj = GameObject.Find("COTTAGE");
                if (cottageObj != null)
                {
                    Transform chimney = cottageObj.transform.Find("MESH/Cottage_chimney");
                    if (chimney != null) chimney.parent = null;
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "ITEMS_FIXES_ERROR");
            }

            // Applying a script to vehicles that can pick up and drive the player as a passanger to his house.
            // This script makes it so when the player enters the car, the parent of the vehicle is set to null.
            try
            {
                GameObject traffic = GameObject.Find("TRAFFIC");
                GameObject npc_cars = GameObject.Find("NPC_CARS");
                
                Transform fittanTrans = traffic != null ? traffic.transform.Find("VehiclesDirtRoad/Rally/FITTAN") : null;
                Transform kuskiTrans = npc_cars != null ? npc_cars.transform.Find("KUSKI") : null;

                if (fittanTrans != null)
                {
                    Transform trigger = fittanTrans.Find("PlayerTrigger/DriveTrigger");
                    if (trigger != null)
                    {
                        trigger.gameObject.FsmInject("Player in car", () => fittanTrans.parent = null);
                    }
                }

                if (kuskiTrans != null)
                {
                    Transform trigger = kuskiTrans.Find("PlayerTrigger/DriveTrigger");
                    if (trigger != null)
                    {
                        trigger.gameObject.FsmInject("Player in car", () => kuskiTrans.parent = null);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, true, "TAXI_MANAGERS_ERROR");
            }

            // Fixed Ventii bet resetting to default on cabin load.
            GameObject cabinObj = GameObject.Find("CABIN");
            if (cabinObj != null)
            {
                Transform cabin = cabinObj.transform;
                try
                {
                    Transform manager = cabin.Find("Cabin/Ventti/Table/GameManager");
                    if (manager != null)
                    {
                        PlayMakerFSM fsm = manager.GetPlayMaker("Use");
                        if (fsm != null) fsm.Fsm.RestartOnEnable = false;
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.New(ex, false, "VENTII_FIX_ERROR");
                }

                // Cabin door resetting fix.
                try
                {
                    Transform handle = cabin.Find("Cabin/Door/Pivot/Handle");
                    if (handle != null)
                    {
                        PlayMakerFSM fsm = handle.gameObject.GetComponent<PlayMakerFSM>();
                        if (fsm != null) fsm.Fsm.RestartOnEnable = false;
                    }
                }
                catch (Exception ex)
                {
                    ExceptionManager.New(ex, false, "CABIN_DOOR_RESET_FIX_ERROR");
                }
            }

            // Junk cars - setting Load game to null.
            int junkCarCounter = 1;
            try
            {
                for (; GameObject.Find($"JunkCar{junkCarCounter}") != null; junkCarCounter++)
                {
                    GameObject junk = GameObject.Find($"JunkCar{junkCarCounter}");
                    PlayMakerFSM fsm = junk.GetComponent<PlayMakerFSM>();
                    if (fsm != null)
                    {
                        fsm.Fsm.RestartOnEnable = false;
                    }

                    WorldObjectManager.Instance.Add(junk, DisableOn.Distance);
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, $"JUNK_CARS_{junkCarCounter}_ERROR");;
            }

            // Toggle Humans (apart from Farmer and Fighter2).
            try
            {
                GameObject humans = GameObject.Find("HUMANS");
                if (humans != null)
                {
                    foreach (Transform t in humans.GetComponentsInChildren<Transform>().Where(t => t.parent == humans.transform))
                    {
                        if (t.gameObject.name.EqualsAny("HUMANS", "Fighter2", "Farmer", "FighterPub"))
                            continue;

                        var human = WorldObjectManager.Instance.Add(t.gameObject, DisableOn.Distance);
                        if (human != null)
                        {
                            human.MinimumToggleDistance = 150;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "HUMANS_ERROR");
            }

            // Fixes wasp hives resetting to on load values.
            try
            {
                GameObject[] wasphives = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == "WaspHive").ToArray();
                foreach (GameObject wasphive in wasphives)
                {
                    PlayMakerFSM fsm = wasphive.GetComponent<PlayMakerFSM>();
                    if (fsm != null)
                    {
                        fsm.Fsm.RestartOnEnable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "WASPHIVES_ERROR");
            }

            // Disabling the script that sets the kinematic state of Satsuma to False.
            try
            {
                GameObject hand = GameObject.Find("PLAYER/Pivot/AnimPivot/Camera/FPSCamera/1Hand_Assemble/Hand");
                if (hand != null)
                {
                    PlayMakerFSM pickUp = hand.GetPlayMaker("PickUp");
                    if (pickUp != null)
                    {
                        var dropState = pickUp.GetState("Drop part");
                        if (dropState != null && dropState.Actions.Length > 0) dropState.RemoveAction(0);
                        
                        var dropState2 = pickUp.GetState("Drop part 2");
                        if (dropState2 != null && dropState2.Actions.Length > 0) dropState2.RemoveAction(0);
                        
                        var toolPicked = pickUp.GetState("Tool picked");
                        if (toolPicked != null && toolPicked.Actions.Length > 2) toolPicked.RemoveAction(2);
                        
                        var dropTool = pickUp.GetState("Drop tool");
                        if (dropTool != null && dropTool.Actions.Length > 0) dropTool.RemoveAction(0);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_HAND_BS_FIX");
            }

            // Preventing mattres from being disabled.
            try
            {
                GameObject dingo = GameObject.Find("DINGONBIISI");
                if (dingo != null)
                {
                    Transform mattres = dingo.transform.Find("mattres");
                    if (mattres != null)
                        mattres.parent = null;
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "MANSION_MATTRES_ERROR");
            }

            // Item anti clip for cottage.
            try
            {
                GameObject area = new GameObject("MOP_ItemAntiClip");
                area.transform.position = new Vector3(-848.3f, -5.4f, 505.5f);
                area.transform.eulerAngles = new Vector3(0, 343.0013f, 0);
                area.AddComponent<ItemAntiClip>();
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, true, "ITEM_ANTICLIP_ERROR");
            }

            // Z-fighting fix for wristwatch.
            try
            {
                GameObject player = GameObject.Find("PLAYER");
                if (player != null)
                {
                    Transform hour = player.transform.Find("Pivot/AnimPivot/Camera/FPSCamera/FPSCamera/Watch/Animate/BreathAnim/WristwatchHand/Clock/Pivot/Hour/hour");
                    if (hour)
                    {
                        hour.GetComponent<Renderer>().material.renderQueue = 3001;
                    }

                    Transform minute = player.transform.Find("Pivot/AnimPivot/Camera/FPSCamera/FPSCamera/Watch/Animate/BreathAnim/WristwatchHand/Clock/Pivot/Minute/minute");
                    if (minute)
                    {
                        minute.GetComponent<Renderer>().material.renderQueue = 3002;
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "HANDWATCH_RENDERER_QUEUE_ERROR");
            }

            // Adds roll fix to the bus.
            try
            {
                GameObject bus = GameObject.Find("BUS");
                if (bus)
                {
                    bus.AddComponent<BusRollFix>();
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "BUS_ROLL_FIX_ERROR");
            }

            // Fixes bedroom window wrap resetting to default value.
            try
            {
                GameObject yard = GameObject.Find("YARD");
                if (yard != null)
                {
                    Transform triggerWindowWrap = yard.transform.Find("Building/BEDROOM1/trigger_window_wrap");
                    if (triggerWindowWrap != null)
                        triggerWindowWrap.GetComponent<PlayMakerFSM>().Fsm.RestartOnEnable = false;
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "TRIGGER_WINDOW_WRAP_ERROR");
            }

            // Fixes diskette ejecting not wokring.
            try
            {
                GameObject triggerDiskette = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "TriggerDiskette");

                if (triggerDiskette != null)
                {
                    PlayMakerFSM fsm = triggerDiskette.GetPlayMaker("Assembly");
                    if (fsm != null) fsm.Fsm.RestartOnEnable = false;
                }
                else
                {
                    ModConsole.Log("[MOP] Trigger diskette was null");
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "TRIGGER_DISKETTE_ERROR");
            }

            // Fixed computer memory resetting.
            try
            {
                GameObject triggerPlayMode = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "TriggerPlayMode");
                if (triggerPlayMode != null)
                {
                    PlayMakerFSM fsm = triggerPlayMode.GetPlayMaker("PlayerTrigger");
                    if (fsm != null) fsm.Fsm.RestartOnEnable = false;
                }
                else
                {
                    ModConsole.Log("[MOP] Trigger play mode was null");
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "TRIGGER_PLAY_MODE_ERROR");
            }

            // Fixes berry picking skill resetting to default.
            try
            {
                GameObject jobs = GameObject.Find("JOBS");
                if (jobs != null)
                {
                    Transform sf = jobs.transform.Find("StrawberryField");
                    if (sf != null)
                    {
                        PlayMakerFSM fsm = sf.gameObject.GetComponent<PlayMakerFSM>();
                        if (fsm != null) fsm.Fsm.RestartOnEnable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "STRAWBERRY_FIELD_FSM");
            }

            // GrandmaHiker fixes.
            try
            {
                GameObject grandmaHiker = GameObject.Find("GrannyHiker");
                if (grandmaHiker)
                {
                    Transform skeletonTrans = grandmaHiker.transform.Find("Char/skeleton");
                    if (skeletonTrans != null)
                    {
                        GameObject skeleton = skeletonTrans.gameObject;
                        PlayMakerFSM logicFSM = grandmaHiker.GetPlayMaker("Logic");
                        if (logicFSM != null)
                        {
                            FsmState openDoorFsmState = logicFSM.GetState("Open door");
                            if (openDoorFsmState != null)
                            {
                                List<FsmStateAction> openDoorActions = openDoorFsmState.Actions.ToList();
                                openDoorActions.Add(new GrandmaHiker(skeleton, false));
                                openDoorFsmState.Actions = openDoorActions.ToArray();
                            }

                            FsmState setMass2State = logicFSM.GetState("Set mass 2");
                            if (setMass2State != null)
                            {
                                List<FsmStateAction> setMass2Actions = setMass2State.Actions.ToList();
                                setMass2Actions.Add(new GrandmaHiker(skeleton, true));
                                setMass2State.Actions = setMass2Actions.ToArray();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "GRANDMA_HIKER_FIXES");
            }

            // Construction site
            try
            {
                GameObject perajarviObj = GameObject.Find("PERAJARVI");
                if (perajarviObj != null)
                {
                    Transform construction = perajarviObj.transform.Find("ConstructionSite");
                    if (construction)
                    {
                        construction.parent = null;
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "PERAJARVI_CONSTRUCTION_ERROR");
            }

            // Satsuma parts trigger fix.
            try
            {
                gameObject.AddComponent<SatsumaTriggerFixer>();
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "SATSUMA_TRIGGER_FIXER_ERROR");
            }

            // MailBox fix
            try
            {
                foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == "MailBox"))
                {
                    Transform hatch = g.transform.Find("BoxHatch");
                    if (hatch)
                    {
                        PlayMakerFSM fsm = hatch.GetComponent<PlayMakerFSM>();
                        if (fsm != null) fsm.Fsm.RestartOnEnable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, "MAILBOX_ERROR");
            }

            ModConsole.Log("[MOP] Finished applying fixes");
        }

        /// <summary>
        /// Fixes hood popping out of the car on game load.
        /// </summary>
        public void HoodFix(Transform hoodPivot, Transform batteryPivot, Transform batteryTrigger)
        {
            if (hoodPivot == null || batteryPivot == null || batteryTrigger == null) return;
            StartCoroutine(HoodFixCoroutine(hoodPivot, batteryPivot, batteryTrigger));
        }

        IEnumerator HoodFixCoroutine(Transform hoodPivot, Transform batteryPivot, Transform batteryTrigger)
        {
            yield return new WaitForSeconds(2);

            // Hood
            GameObject hoodObj = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "hood(Clone)");
            if (hoodObj == null) yield break;
            Transform hood = hoodObj.transform;
            CustomPlayMakerFixedUpdate hoodFixedUpdate = hood.gameObject.AddComponent<CustomPlayMakerFixedUpdate>();

            // Fiber Hood
            GameObject fiberHood = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(obj => obj.name == "fiberglass hood(Clone)"
                && obj.GetComponent<PlayMakerFSM>() != null
                && obj.GetComponent<MeshCollider>() != null);

            int retries = 0;
            if (FsmManager.IsStockHoodBolted() && hood.parent != hoodPivot)
            {
                hood.gameObject.SetActive(true);

                while (hood.parent != hoodPivot)
                {
                    // Satsuma got disabled while trying to fix the hood.
                    // Attempt to fix it later.
                    if (!hoodPivot.gameObject.activeSelf)
                    {
                        yield break;
                    }

                    FsmManager.ForceHoodAssemble();
                    yield return null;

                    // If 10 retries failed, quit the loop.
                    retries++;
                    if (retries == 10)
                    {
                        break;
                    }
                }
            }

            hoodFixedUpdate.StartFixedUpdate();

            if (fiberHood != null && FsmManager.IsFiberHoodBolted() && fiberHood.transform.parent != hoodPivot)
            {
                retries = 0;
                while (fiberHood.transform.parent != hoodPivot)
                {
                    // Satsuma got disabled while trying to fix the hood.
                    // Attempt to fix it later.
                    if (!hoodPivot.gameObject.activeSelf)
                    {
                        Satsuma.Instance.AfterFirstEnable = false;
                        yield break;
                    }

                    FsmManager.ForceHoodAssemble();
                    yield return null;

                    // If 10 retries failed, quit the loop.
                    retries++;
                    if (retries == 60)
                    {
                        break;
                    }
                }
            }

            if (hood.gameObject.GetComponent<SatsumaBoltsAntiReload>() == null)
                hood.gameObject.AddComponent<SatsumaBoltsAntiReload>();
            if (fiberHood != null && fiberHood.GetComponent<SatsumaBoltsAntiReload>() == null)
                fiberHood.AddComponent<SatsumaBoltsAntiReload>();

            // Adds delayed initialization for hood hinge.
            if (hood.gameObject.GetComponent<DelayedHingeManager>() == null)
                hood.gameObject.AddComponent<DelayedHingeManager>();

            // Fix for hood not being able to be closed.
            if (hood.gameObject.GetComponent<SatsumaCustomHoodUse>() == null)
                hood.gameObject.AddComponent<SatsumaCustomHoodUse>();

            // Fix for battery popping out.
            if (FsmManager.IsBatteryInstalled() && batteryPivot.parent == null)
            {
                batteryTrigger.gameObject.SetActive(true);
                PlayMakerFSM fsm = batteryTrigger.gameObject.GetComponent<PlayMakerFSM>();
                if (fsm != null) fsm.SendEvent("ASSEMBLE");
            }

            HoodFixDone = true;
        }

        internal void ForceDetachTrailer()
        {
            StartCoroutine(ForceDetachTrailerRoutine());
        }

        IEnumerator ForceDetachTrailerRoutine()
        {
            while (!MopSettings.IsModActive) yield return null;
            for (int i = 0; i < 10; i++)
            {
                PlayMakerFSM.BroadcastEvent("TRAILERDETACH");
                yield return null;
            }
        }

        void SetParent(Transform root, Transform newParent, string objectName)
        {
            if (root == null) return;
            try
            {
                Transform t = root.Find(objectName);
                if (t != null)
                {
                    t.parent = newParent;
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, $"GAMEFIXES_PARENTCHANGING\n{root.gameObject.name} => {objectName} => {(newParent == null ? "null" : newParent.gameObject.name)}");
            }
        }

        void SetParent(Transform newParent, string objectName)
        {
            try
            {
                GameObject obj = GameObject.Find(objectName);
                if (obj != null)
                {
                    obj.transform.parent = newParent;
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, $"GAMEFIXES_PARENTCHANGING_{objectName}");
            }
        }

        void SetParent(Transform newParent, Transform obj)
        {
            if (obj == null) return;
            try
            {
                obj.parent = newParent;
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, false, $"GAMEFIXES_PARENTCHANGING_UNKNOWN_TRANSFORM");
            }
        }
    }
}
