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

using HutongGames.PlayMaker;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using MOP.FSM;
using MOP.FSM.Actions;
using MOP.Common;
using MOP.Common.Enumerations;
using MOP.Items.Cases;
using MOP.Vehicles.Managers.SatsumaManagers;
using MOP.Rules;
using MOP.Rules.Types;

namespace MOP.Vehicles.Cases
{
    class Satsuma : Vehicle
    {
        const int MinimumBolts = 163;

        static Satsuma instance;
        public static Satsuma Instance { get => instance; }

        // If true, prevents Satsuma physics from disabling.
        public bool IsSatsumaInInspectionArea;
        public bool IsSatsumaInParcFerme;

        // If is false, the first enabling script will be toggled.
        public bool AfterFirstEnable;

        // Objects that can be toggled.
        readonly Transform[] disableableObjects;

        // Whitelist of name of objects that should be disabled.
        readonly string[] whiteList = 
        { 
            "DriveTrigger", "SpawnPlayer", "CarShadowProjector", "Hooks", "CameraPivot", "PivotSeatR", "HookRear", "BeerCaseTarget",
            "HookFront", "GetInPivot", "TrafficTrigger", "Wipers", "WiperLeftPivot", "WiperRightPivot", "wipers_tap", "wipers_rod",
            "generalPivot", "CarRearMirrorPivot", "StagingWheel", "shadow_body", "NormalFront", "NormalSide", "NormalRear", "WindSide",
            "WindFront", "WindRear", "CoG", "Interior", "Body", "MiscParts", "Dashboard" 
        };
        readonly string[] ignoredRendererNames = { "Sphere", "Capsule", "Cube", "Mokia" };

        // Masked objets are the ones which bolts are hidden, until the overlaping object is detached.
        readonly string[] maskedObjectNames = 
        { 
            "MaskedClutchCover", "MaskedBearing2", "MaskedBearing3", "MaskedFlywheel", 
            "MaskedFlywheelRacing", "MaskedPiston2", "MaskedPiston3", "MaskedPiston4" 
        };

        // Renderers.
        // Renderes of the entire car.
        readonly List<Renderer> renderers;
        bool renderersOnCloseEnabled;

        // Pivot transform of hood.
        readonly Transform pivotHood;

        // Key object.
        readonly GameObject key;

        // Elements that are toggled uppon some situation.
        readonly List<SatsumaOnActionObjects> satsumaOnActionObjects;

        // Bolts that resetting will be disabled.
        List<SatsumaBoltsAntiReload> satsumaBoltsAntiReloads;
        bool partsUnglued;
        // Fix for elements for which the bolts are masked.
        readonly Dictionary<GameObject, bool> maskedElements;
        int maskedFixStages;

        // Dashboard gauges.
        readonly List<Material> dashboardMaterials;
        readonly FsmInt lightSelection;

        // Cooldown ticking object.
        readonly GameObject cooldownTick;

        // AI object.
        readonly PlayMakerFSM drivingAI;

        // Used for when the Fleetari has nicked the car.
        // It prevents the car teleporting back to the last position known to the player.
        bool hasBeenMovedByFleetari;

        internal Quaternion lastGoodRotation;
        internal Vector3 lastGoodPosition;
        bool lastGoodRotationSaved;

        // AIR TIME CHECKS
        // Frames left, where the game assumes that the car is still mid-air.
        private int framesLeftToSkipForGroundCheckDueToFlight;
        // Frames to skip when air-time has started.
        private const int FramesToSkip = 30;
        // Velocity magnitude above which the car body is considered as moving.
        private const float VelocityCheckDeadzone = 0.2f;

        /// <summary>
        /// Initialize class
        /// </summary>
        public Satsuma(string gameObject) : base(gameObject)
        {
            instance = this;
            vehicleType = VehiclesTypes.Satsuma;

            disableableObjects = GetDisableableChilds();

            Toggle = ToggleActive;

            // Get engine bay renderers
            var bodyPivotHood = this.gameObject.transform.Find("Body/pivot_hood");
            pivotHood = bodyPivotHood != null ? bodyPivotHood : null;

            // Get all the other renderers
            renderers = GetRenderersToDisable();

            // Ignore Rule
            IgnoreRule vehicleRule = RulesManager.Instance.GetList<IgnoreRule>().Find(v => v.ObjectName == this.gameObject.name);
            if (vehicleRule != null)
            {
                Toggle = IgnoreToggle;

                if (vehicleRule.TotalIgnore)
                    IsActive = false;
            }

            lastGoodRotation = transform.localRotation;
            lastGoodPosition = transform.localPosition;

            // Adding components to normal and bucket seats.
            try
            {
                var seatDriver = GameObject.Find("seat driver(Clone)");
                if (seatDriver != null && seatDriver.GetComponent<SatsumaSeatsManager>() == null) seatDriver.AddComponent<SatsumaSeatsManager>();
                var seatPassenger = GameObject.Find("seat passenger(Clone)");
                if (seatPassenger != null && seatPassenger.GetComponent<SatsumaSeatsManager>() == null) seatPassenger.AddComponent<SatsumaSeatsManager>();
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_SEAT_FIX_ERROR");
            }

            try
            {
                GameObject bucketDriver = GameObject.Find("bucket seat driver(Clone)");
                GameObject bucketPassanger = GameObject.Find("bucket seat passenger(Clone)");
                if (bucketDriver == null)
                {
                    bucketDriver = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "bucket seat driver(Clone)"
                    && g.transform.parent != null && g.transform.parent.gameObject.name == "Parts");
                    bucketPassanger = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "bucket seat passenger(Clone)"
                    && g.transform.parent != null && g.transform.parent.gameObject.name == "Parts");
                }

                if (bucketDriver != null && bucketDriver.GetComponent<SatsumaSeatsManager>() == null) bucketDriver.AddComponent<SatsumaSeatsManager>();
                if (bucketPassanger != null && bucketPassanger.GetComponent<SatsumaSeatsManager>() == null) bucketPassanger.AddComponent<SatsumaSeatsManager>();
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_BUCKET_SEAT_FIX_ERROR");
            }

            // Fix for mechanical wear of the car.
            try
            {
                var wearTrans = transform.Find("CarSimulation/MechanicalWear");
                if (wearTrans != null)
                {
                    var wearFsm = wearTrans.gameObject.GetComponent<PlayMakerFSM>();
                    if (wearFsm != null && wearFsm.Fsm != null) wearFsm.Fsm.RestartOnEnable = false;
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_MECHANICAL_WEAR_FIX_ERROR");
            }

            // Fix for not working handbrake after respawn.
            try
            {
                var handbrakeObj = GameObject.Find("HandBrake");
                if (handbrakeObj != null)
                {
                    var handbrakeFsm = handbrakeObj.GetComponent<PlayMakerFSM>();
                    if (handbrakeFsm != null) handbrakeFsm.enabled = true;
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_HANDBRAKE_ERROR");
            }

            // Fixes handbrake lever position.
            try
            {
                var leverTrans = transform.Find("MiscParts/HandBrake/handbrake(xxxxx)/handbrake lever");
                if (leverTrans != null)
                {
                    var useFsm = leverTrans.GetPlayMaker("Use");
                    if (useFsm != null && useFsm.Fsm != null) useFsm.Fsm.RestartOnEnable = false;
                }
            }
            catch
            {
                ExceptionManager.New(new System.Exception("handbrake lever fix"), false, "Unable to fix handbrake lever.");
            }

            // Battery terminal.
            // For some reason in the vanilla game, the negative battery terminal sometimes gets disabled, and the game doesn't allow to reenable it.
            try
            {
                var wiringTrans = transform.Find("Wiring");
                if (wiringTrans != null)
                {
                    var statusFsm = wiringTrans.GetPlayMaker("Status");
                    if (statusFsm != null)
                    {
                        var disableState = statusFsm.GetState("Disable battery wires");
                        if (disableState != null) disableState.AddAction(new CustomBatteryDisable());
                    }
                }
            }
            catch
            {
                ExceptionManager.New(new System.Exception("battery terminal fix"), false, "Unable to fix battery terminal wire.");
            }

            BoltsFix();

            // Fixes car body color resetting to default.
            try
            {
                var paintTrans = transform.Find("Body/car body(xxxxx)");
                if (paintTrans != null)
                {
                    var paintFsm = paintTrans.GetPlayMaker("Paint");
                    if (paintFsm != null && paintFsm.Fsm != null) paintFsm.Fsm.RestartOnEnable = false;
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_BODY_PAINT_FIX_ERROR");
            }

            // Wiping Load for alternator belts, oil filters, spark plugs and batteries.
            GameObject[] parts = Resources.FindObjectsOfTypeAll<GameObject>()
                                .Where(obj => obj.name.ContainsAny("alternator belt(Clone)", "oil filter(Clone)", "spark plug(Clone)", "battery(Clone)"))
                                .ToArray();
            foreach (GameObject part in parts)
            {
                try
                {
                    if (part.transform.root != null && part.transform.root.gameObject.name == "GAZ24(1420kg)") continue;

                    PlayMakerFSM useFsm = part.GetPlayMaker("Use");
                    if (useFsm != null)
                    {
                        FsmState state1 = useFsm.GetState("State 1");
                        if (state1 != null && state1.Actions != null)
                        {
                            List<FsmStateAction> emptyState1 = state1.Actions.ToList();
                            emptyState1.Insert(0, new CustomStop());
                            state1.Actions = emptyState1.ToArray();
                            state1.SaveActions();
                        }

                        FsmState loadState = useFsm.GetState("Load");
                        if (loadState != null && loadState.Fsm != null) loadState.Fsm.RestartOnEnable = false;
                    }
                }
                catch (System.Exception ex)
                {
                    ExceptionManager.New(ex, true, "SATSUMA_STATE1_WIPE_ERROR");
                }
            }

            // Fix for cd player disabling other vehicles radio.
            try
            {
                var cdPlayer = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(g => g.name == "cd player(Clone)" && g.GetComponent<PlayMakerFSM>() != null && g.GetComponent<MeshRenderer>() != null);
                if (cdPlayer != null)
                {
                    var knobTrans = cdPlayer.transform.Find("ButtonsCD/RadioVolume");
                    if (knobTrans != null)
                    {
                        var knobFsm = knobTrans.GetPlayMaker("Knob");
                        if (knobFsm != null && knobFsm.Fsm != null) knobFsm.Fsm.RestartOnEnable = false;
                    }
                }
            }
            catch
            {
                ExceptionManager.New(new System.Exception("FSM RadioCD Fix"), false, "Unable to fix cd player(Clone).");
            }

            // Fix radio.
            try
            {
                var radio = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(g => g.name == "radio(Clone)" && g.transform.root != null && g.transform.root.gameObject.name != "JAIL");
                if (radio != null)
                {
                    var knobTrans = radio.transform.Find("ButtonsRadio/RadioVolume");
                    if (knobTrans != null)
                    {
                        var knobFsm = knobTrans.GetPlayMaker("Knob");
                        if (knobFsm != null && knobFsm.Fsm != null) knobFsm.Fsm.RestartOnEnable = false;
                    }
                }
            }
            catch
            {
                ExceptionManager.New(new System.Exception("FSM Radio Fix"), false, "Unable to fix radio(Clone).");
            }

            // Fix for window grille paint resetting to the default.
            try
            {
                var grille = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "window grille(Clone)" && g.GetPlayMaker("Paint") != null);
                if (grille != null)
                {
                    var paintFsm = grille.GetPlayMaker("Paint");
                    if (paintFsm != null && paintFsm.Fsm != null) paintFsm.Fsm.RestartOnEnable = false;
                }
            }
            catch
            {
                ExceptionManager.New(new System.Exception("window grille fix"), false, "Unable to fix window grille(Clone).");
            }

            // Apply hood fix.
            try
            {
                var bodyHoodTrans = transform.Find("Body/pivot_hood");
                var trigBatteryTrans = transform.Find("MiscParts/trigger_battery");
                var pivBatteryTrans = transform.Find("MiscParts/pivot_battery");
                if (bodyHoodTrans != null && trigBatteryTrans != null && pivBatteryTrans != null)
                {
                    GameFixes.Instance.HoodFix(bodyHoodTrans, trigBatteryTrans, pivBatteryTrans);
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_HOOD_FIX_ERROR");
            }
            
            // Rear bumper detaching fix.
            try
            {
                GameObject rearBumper = GameObject.Find("bumper rear(Clone)");
                GameObject databaseBumper = GameObject.Find("Database/DatabaseBody/Bumper_Rear");
                if (rearBumper != null && databaseBumper != null)
                {
                    databaseBumper.SetActive(false);
                    databaseBumper.SetActive(true);
                    RearBumperBehaviour behaviour = rearBumper.GetComponent<RearBumperBehaviour>() ?? rearBumper.AddComponent<RearBumperBehaviour>();
                    
                    var removalFsm = rearBumper.GetPlayMaker("Removal");
                    if (removalFsm != null)
                    {
                        var removePartState = removalFsm.GetState("Remove part");
                        if (removePartState != null) removePartState.AddAction(new CustomSatsumaBumperDetach(behaviour));
                    }

                    var trigBumperRear = transform.Find("Body/trigger_bumper_rear");
                    if (trigBumperRear != null)
                    {
                        var assemblyFsm = trigBumperRear.GetPlayMaker("Assembly");
                        if (assemblyFsm != null)
                        {
                            var assemble2State = assemblyFsm.GetState("Assemble 2");
                            if (assemble2State != null) assemble2State.AddAction(new CustomSatsumaBumperAttach(behaviour));
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_REAR_BUMPER_DETACH_FIX_ERROR");
            }

            // Fix suspension adding a weight to the car on each car respawn.
            IEnumerable<GameObject> suspensionParts = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(g => g.name.ContainsAny("strut", "coil spring", "shock absorber")
                         && g.transform.root != null
                         && g.transform.root == this.gameObject.transform);
            foreach (GameObject part in suspensionParts)
            {
                try
                {
                    if (part.GetComponent<SatsumaPartMassManager>() == null)
                        part.AddComponent<SatsumaPartMassManager>();
                }
                catch
                {
                    ExceptionManager.New(new System.Exception("SatsumaPartMassManager: Suspension"), false, "SatsumaPartsMassManager: Adding to suspension parts");
                }
            }
            
            // Appparently not only suspension fucks over the Satsuma...
            GameObject[] others = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(g => g.name.EqualsAny("grille gt(Clone)", "grille(Clone)", "subwoofer panel(Clone)",
                "seat rear(Clone)", "amplifier(Clone)", "rearlight(leftx)", "rearlight(right)", "racing radiator(xxxxx)", "radiator(xxxxx)",
                "radiator hose1(xxxxx)", "radiator hose3(xxxxx)", "marker light left(xxxxx)", "marker light right(xxxxx)", "antenna(leftx)",
                "antenna(right)", "dashboard(Clone)")).ToArray();
            foreach (GameObject other in others)
            {
                try
                {
                    if (other.GetComponent<SatsumaPartMassManager>() == null)
                        other.AddComponent<SatsumaPartMassManager>();
                }
                catch
                {
                    ExceptionManager.New(new System.Exception("SatsumaPartMassManager: Others"), false, "SatsumaPartsMassManager: Others adding.");
                }
            }

            try
            {
                var column = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "steering_column2" && g.transform.root == this.transform);
                if (column != null)
                {
                    var ignitionKey = column.transform.Find("Ignition/Keys/Key");
                    key = ignitionKey != null ? ignitionKey.gameObject : null;
                }
            }
            catch
            {
                ExceptionManager.New(new System.Exception("SatsumKey: Cannot Locate Key"), false, "SatsumaPartsMassManager: Key Error");
            }

            try
            {
                var blockClone = GameObject.Find("block(Clone)");
                if (blockClone != null)
                {
                    var coolTickTrans = blockClone.transform.Find("CooldownTick");
                    cooldownTick = coolTickTrans != null ? coolTickTrans.gameObject : null;
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_COOLDOWN_TICK_ERROR");
            }

            // Fix for reg plates Z fighting.
            try
            {
                var bootlidObj = GameObject.Find("bootlid(Clone)");
                if (bootlidObj != null)
                {
                    var regPlate = bootlidObj.transform.Find("RegPlateRear");
                    if (regPlate != null)
                    {
                        var rend = regPlate.gameObject.GetComponent<Renderer>();
                        if (rend != null && rend.material != null) rend.material.renderQueue = 100;
                    }
                }

                var bumperFrontObj = GameObject.Find("bumper front(Clone)");
                if (bumperFrontObj != null)
                {
                    var regPlate = bumperFrontObj.transform.Find("RegPlateFront");
                    if (regPlate != null)
                    {
                        var rend = regPlate.gameObject.GetComponent<Renderer>();
                        if (rend != null && rend.material != null) rend.material.renderQueue = 100;
                    }
                }

                // Z-fighting of the Satsuma dashboard meters.
                if (!RulesManager.Instance.SpecialRules.SatsumaIgnoreRenderers)
                {
                    dashboardMaterials = new List<Material>();
                    var dashMeters = GameObject.Find("dashboard meters(Clone)");
                    if (dashMeters != null)
                    {
                        var nFuel = dashMeters.transform.Find("Gauges/Fuel/needle_small");
                        if (nFuel != null) dashboardMaterials.Add(nFuel.gameObject.GetComponent<Renderer>().material);
                        var nTemp = dashMeters.transform.Find("Gauges/Temp/needle_small");
                        if (nTemp != null) dashboardMaterials.Add(nTemp.gameObject.GetComponent<Renderer>().material);
                        var nSpeed = dashMeters.transform.Find("Gauges/Speedometer/needle_large");
                        if (nSpeed != null) dashboardMaterials.Add(nSpeed.gameObject.GetComponent<Renderer>().material);
                    }

                    var rpmGaugeObj = GameObject.Find("rpm gauge(Clone)");
                    if (rpmGaugeObj != null)
                    {
                        var nRpm = rpmGaugeObj.transform.Find("Pivot/needle");
                        if (nRpm != null) dashboardMaterials.Add(nRpm.gameObject.GetComponent<Renderer>().material);
                    }

                    var clockGaugeObj = GameObject.Find("clock gauge(Clone)");
                    if (clockGaugeObj != null)
                    {
                        var nHour = clockGaugeObj.transform.Find("ClockCar/hour/needle_hour");
                        if (nHour != null) dashboardMaterials.Add(nHour.gameObject.GetComponent<Renderer>().material);
                        var nMin = clockGaugeObj.transform.Find("ClockCar/minute/needle_minute");
                        if (nMin != null) dashboardMaterials.Add(nMin.gameObject.GetComponent<Renderer>().material);
                    }

                    foreach (Material mat in dashboardMaterials)
                    {
                        if (mat != null) mat.renderQueue = 100;
                    }

                    if (dashMeters != null)
                    {
                        var modesTrans = dashMeters.transform.Find("Knobs/ButtonsDash/LightModes");
                        if (modesTrans != null)
                        {
                            var modesFsm = modesTrans.gameObject.GetComponent<PlayMakerFSM>();
                            if (modesFsm != null && modesFsm.FsmVariables != null)
                            {
                                lightSelection = modesFsm.FsmVariables.GetFsmInt("Selection");
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                // Fixing the door hinges getting stuck.
                var doorLeft = GameObject.Find("door left(Clone)");
                if (doorLeft != null)
                {
                    if (doorLeft.GetComponent<SatsumaHingeManager>() == null) doorLeft.AddComponent<SatsumaHingeManager>();
                    var paintFsm = doorLeft.GetPlayMaker("Paint");
                    if (paintFsm != null && paintFsm.Fsm != null) paintFsm.Fsm.RestartOnEnable = false;
                }
                
                var doorRight = GameObject.Find("door right(Clone)");
                if (doorRight != null)
                {
                    if (doorRight.GetComponent<SatsumaHingeManager>() == null) doorRight.AddComponent<SatsumaHingeManager>();
                    var paintFsm = doorRight.GetPlayMaker("Paint");
                    if (paintFsm != null && paintFsm.Fsm != null) paintFsm.Fsm.RestartOnEnable = false;
                }

                var bootlidObj = GameObject.Find("bootlid(Clone)");
                if (bootlidObj != null)
                {
                    if (bootlidObj.GetComponent<SatsumaHingeManager>() == null) bootlidObj.AddComponent<SatsumaHingeManager>();
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_DOOR_FIX_ERROR");
            }
            
            // Setup stuff that gets disabled using ToggleElements
            try
            {
                satsumaOnActionObjects = new List<SatsumaOnActionObjects>();
                
                var wearObj = transform.Find("CarSimulation/MechanicalWear");
                if (wearObj != null) satsumaOnActionObjects.Add(new SatsumaOnActionObjects(wearObj.gameObject, SatsumaEnableOn.OnEngine));
                
                var fixesObj = transform.Find("CarSimulation/Fixes");
                if (fixesObj != null) satsumaOnActionObjects.Add(new SatsumaOnActionObjects(fixesObj.gameObject, SatsumaEnableOn.OnEngine));
                
                var dynoObj = transform.Find("CarSimulation/DynoDistance");
                if (dynoObj != null) satsumaOnActionObjects.Add(new SatsumaOnActionObjects(dynoObj.gameObject, SatsumaEnableOn.OnEngine));
                
                var randBoltObj = transform.Find("CarSimulation/RandomBolt");
                if (randBoltObj != null) satsumaOnActionObjects.Add(new SatsumaOnActionObjects(randBoltObj.gameObject, SatsumaEnableOn.OnEngine));
                
                var rainObj = transform.Find("RainScript");
                if (rainObj != null) satsumaOnActionObjects.Add(new SatsumaOnActionObjects(rainObj.gameObject, SatsumaEnableOn.OnPlayerClose));
                
                var headObj = transform.Find("DriverHeadPivot");
                if (headObj != null) satsumaOnActionObjects.Add(new SatsumaOnActionObjects(headObj.gameObject, SatsumaEnableOn.OnPlayerClose));
                
                var airObj = transform.Find("AirIntake");
                if (airObj != null) satsumaOnActionObjects.Add(new SatsumaOnActionObjects(airObj.gameObject, SatsumaEnableOn.OnPlayerClose));
                
                var shifterFsm = this.gameObject.GetPlayMaker("ButtonShifter");
                if (shifterFsm != null) satsumaOnActionObjects.Add(new SatsumaOnActionObjects(shifterFsm, SatsumaEnableOn.OnPlayerClose));
                
                var chassisObj = transform.Find("Chassis");
                if (chassisObj != null) satsumaOnActionObjects.Add(new SatsumaOnActionObjects(chassisObj.gameObject, SatsumaEnableOn.OnPlayerFar));
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_ON_ACTION_OBJECTS_ERROR");
            }
            
            // Replace on assemble sound playing with custom script.
            try
            {
                GameObject blockObj = GameObject.Find("block(Clone)");
                if (blockObj != null)
                {
                    PlayMakerFSM blockBoltCheck = blockObj.GetPlayMaker("BoltCheck");
                    if (blockBoltCheck != null)
                    {
                        FsmState boltsONState = blockBoltCheck.GetState("Bolts ON");
                        if (boltsONState != null && boltsONState.Actions != null && boltsONState.Actions.Length > 1)
                        {
                            FsmStateAction[] boltsONActions = boltsONState.Actions;
                            boltsONActions[1] = new MasterAudioAssembleCustom();
                            boltsONState.Actions = boltsONActions;
                            boltsONState.SaveActions();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_BOLTS_AUDIO_ERROR");
            }
            
            // Disable the "Fix Collider" state in FSM Setup, so it won't make items fall through the car.
            try
            {
                var setupFsm = this.gameObject.GetPlayMaker("Setup");
                if (setupFsm != null && setupFsm.Fsm != null) setupFsm.Fsm.RestartOnEnable = false;

                var floorCollider = transform.Find("Colliders/collider_floor3");
                if (floorCollider != null)
                {
                    MeshCollider bootFloor = floorCollider.gameObject.GetComponent<MeshCollider>();
                    if (bootFloor != null)
                    {
                        bootFloor.isTrigger = false;
                        bootFloor.enabled = true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_COLLIDER_SETUP_ERROR");
            }
            
            // Fixes driver dying way too easily from small impacts (hopefully).
            try
            {
                var headPivot = transform.Find("DriverHeadPivot");
                if (headPivot != null)
                {
                    var headRb = headPivot.GetComponent<Rigidbody>();
                    if (headRb != null) headRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_DRIVER_HEAD_PIVOT_ERROR");
            }

            // Masked Elements.
            // Those are objects that are disabled by default, so their bolts are not easily accessible.
            try
            {
                maskedElements = new Dictionary<GameObject, bool>();
                foreach (string obj in maskedObjectNames)
                {
                    GameObject gm = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == obj);
                    if (gm != null)
                    {
                        maskedElements.Add(gm, key != null ? key.activeSelf : true);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_MASKED_ELEMENTS_ERROR");
            }

            // It is not used right now by the game, but it is safe to assume that one day it might be used.
            try
            {
                drivingAI = transform.Find("AI")?.GetPlayMaker("Driving");
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, false, "SATSUMA_AI_ERROR");
            }

            // radiator hose 3
            try
            {
                GameObject radiatorHosePart = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "radiator hose3(xxxxx)");
                if (radiatorHosePart != null)
                {
                    if (radiatorHosePart.GetComponent<SatsumaRadiatorHoseFix>() == null)
                        radiatorHosePart.AddComponent<SatsumaRadiatorHoseFix>();
                }
            }
            catch
            {
                ModConsole.Log("[MOP] Radiator hose 3 error");
            }

            // Windscreen fixes :)
            try
            {
                Transform windscreen = transform.Find("Body/Windshield");
                if (windscreen != null)
                {
                    SatsumaWindscreenFixer swf = windscreen.gameObject.GetComponent<SatsumaWindscreenFixer>() ?? windscreen.gameObject.AddComponent<SatsumaWindscreenFixer>();
                    
                    // And now we are hooking up the windscreen job in repairshop.
                    var repairshop = GameObject.Find("REPAIRSHOP");
                    Transform windshieldJob = repairshop != null ? repairshop.transform.Find("Jobs/Windshield") : null;
                    if (windshieldJob != null)
                    {
                        if (windshieldJob.parent != null && !windshieldJob.parent.gameObject.activeSelf)
                        {
                            windshieldJob.parent.gameObject.SetActive(true);
                            windshieldJob.parent.gameObject.SetActive(false);
                        }

                        PlayMakerFSM windshieldJobFSM = windshieldJob.gameObject.GetComponent<PlayMakerFSM>();
                        if (windshieldJobFSM != null)
                        {
                            FsmState wait1State = windshieldJobFSM.GetState("Wait1");
                            if (wait1State != null && wait1State.Actions != null)
                            {
                                List<FsmStateAction> wait1Actions = wait1State.Actions.ToList();
                                wait1Actions.Insert(0, new WindscreenRepairJob(swf));
                                wait1State.Actions = wait1Actions.ToArray();
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "WINDSCREEN REPAIR FIX ERROR");
            }

            // Fire extinguisher holder.
            try
            {
                var holderTrans = transform.Find("Interior/fire extinguisher holder(xxxxx)");
                if (holderTrans != null)
                {
                    GameObject extinguisherHolder = holderTrans.gameObject;
                    foreach (PlayMakerFSM fsm in extinguisherHolder.GetComponents<PlayMakerFSM>())
                    {
                        if (fsm.Fsm != null) fsm.Fsm.RestartOnEnable = false;
                    }
                }
            }
            catch
            {
                ModConsole.Log("[MOP] Fire extinguisher holder error");
            }

            // Odometer resetting fix
            try
            {
                GameObject odometer = GameObject.Find("dashboard meters(Clone)/Gauges/Odometer");
                if (odometer != null)
                {
                    var fsm = odometer.GetComponent<PlayMakerFSM>();
                    if (fsm == null)
                    {
                        ModConsole.Log("[MOP] Can't reset Odometer FSM. Perhaps a mod has removed it?");
                    }
                    else if (fsm.Fsm != null)
                    {
                        fsm.Fsm.RestartOnEnable = false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, false, "SATS_ODOMETER_RESET_FIX_ERROR");
            }

            // New bolt enabling/disabling.
            try
            {
                GameObject subframeBolts = transform.Find("Chassis/sub frame(xxxxx)/Bolts")?.gameObject;
                GameObject triggerSubframe = transform.Find("Chassis/trigger_subframe")?.gameObject;
                if (triggerSubframe != null && subframeBolts != null)
                {
                    bool isSubframeTriggerActive = triggerSubframe.activeSelf;
                    triggerSubframe.SetActive(true);

                    PlayMakerFSM triggerSubframeFSM = triggerSubframe.GetPlayMaker("Assembly");
                    if (triggerSubframeFSM != null)
                    {
                        var assembleState = triggerSubframeFSM.GetState("Assemble");
                        if (assembleState != null) assembleState.AddAction(new CustomToggleAllBoltsAction(subframeBolts, true));
                    }
                    triggerSubframe.SetActive(isSubframeTriggerActive);
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, false, "SUBFRAME_BOLTS_ERROR");
            }

            // Fix exhaust muffler trigger.
            try
            {
                var mufTrans = transform.Find("MiscParts/pivot_exhaust_muffler");
                if (mufTrans != null)
                {
                    if (mufTrans.gameObject.GetComponent<SatsumaExhaustMufflerFix>() == null)
                        mufTrans.gameObject.AddComponent<SatsumaExhaustMufflerFix>();
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, false, "EXHAUST_MUFFLER_FIX_ERROR");
            }

            try
            {
                GameObject block = GameObject.Find("block(Clone)");
                if (block != null)
                {
                    if (block.GetComponent<SatsumaHingeManager>() == null) block.AddComponent<SatsumaHingeManager>();
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, false, "SATSUMA_BLOCK_FIND_FAILURE");
            }

            try
            {
                var headPivot = transform.Find("DriverHeadPivot");
                if (headPivot != null)
                {
                    if (headPivot.gameObject.GetComponent<SatsumaHingeManager>() == null)
                        headPivot.gameObject.AddComponent<SatsumaHingeManager>();
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, false, "SATSUMA_DRIVER_HEAD_PIVOT_FAILURE");
            }

            if (MopSettings.Mode != PerformanceMode.Safe && this.IsActive)
            {
                this.ForceToggleUnityCar(false);
            }

            if (MopSettings.GenerateToggledItemsListDebug)
            {
                ToggledItemsListGenerator.CreateSatsumaList(disableableObjects);
            }

            if (rb != null) rb.isKinematic = true;
        }

        /// <summary>
        /// Enable or disable car
        /// </summary>
        internal override void ToggleActive(bool enabled)
        {
            if (this.IsPlayerInThisCar())
            {
                enabled = true;
            }

            // Applying hood fix after first enabling.
            if (!AfterFirstEnable && enabled)
            {
                AfterFirstEnable = true;
            }

            // Force enable if AI is driving the car.
            if (drivingAI != null && drivingAI.enabled)
            {
                enabled = true;
            }

            // If the car is left in ParcFerme, the renderes may not re-enable, so just in case we force them to re-enable.
            if (IsSatsumaInParcFerme && renderers != null && renderers.Count > 0 && renderers[0] != null && !renderers[0].enabled)
                RenderersCulling(enabled);

            if (!RulesManager.Instance.SpecialRules.SatsumaIgnoreRenderers)
            {
                bool dashIllumination = false;
                if (IsKeyInserted())
                {
                    dashIllumination = lightSelection != null && lightSelection.Value > 0;
                }

                ToggleDashIllumination(dashIllumination);
            }

            // Don't run the code, if the value is the same
            if (gameObject == null || disableableObjects == null || disableableObjects.Length == 0 || disableableObjects[0] == null || disableableObjects[0].gameObject.activeSelf == enabled)
            {
                return;
            }

            if (!enabled)
            {
                lastGoodRotation = transform.rotation;
                lastGoodPosition = transform.position;
            }

            if (!GameFixes.Instance.HoodFixDone)
                enabled = true;

            for (int i = 0; i < disableableObjects.Length; i++)
            {
                if (disableableObjects[i] == null)
                    continue;

                bool isElementEnabled = IsSatsumaInParcFerme || enabled;
                disableableObjects[i].gameObject.SetActive(isElementEnabled);
            }

            RenderersCulling(enabled);
        }

        public override void ToggleUnityCar(bool enabled)
        {
            // Satsuma in inspection zone and mod wants to disable it?
            // Set enabled to true, so it won't be disabled.
            if (!enabled && IsSatsumaInInspectionArea || IsRopeHooked() || !IsOnGround())
                enabled = true;

            base.ToggleUnityCar(enabled);

            // We're completly freezing Satsuma, so it won't flip (hopefully...).
            SaveCarPosition(enabled);
        }

        public override void ForceToggleUnityCar(bool enabled)
        {
            base.ForceToggleUnityCar(enabled);

            // We're completly freezing Satsuma, so it won't flip (hopefully...).
            SaveCarPosition(enabled);
        }

        private void SaveCarPosition(bool enabled)
        {
            if (!enabled && !lastGoodRotationSaved)
            {
                lastGoodRotationSaved = true;
                lastGoodRotation = transform.localRotation;
                lastGoodPosition = transform.localPosition;
            }

            if (enabled)
            {
                lastGoodRotationSaved = false;
            }

            rb.constraints = enabled ? RigidbodyConstraints.None : RigidbodyConstraints.FreezePosition;
        }
        
        public override bool IsOnGround()
        {
            // Wheel not attached? Check for drivetrain torque and rigidbody velocity.
            if (!wheel.enabled)
            {
                // Car body is moving? Reset the time where we assume that the car body is moving.
                if (rb.velocity.magnitude > VelocityCheckDeadzone)
                {
                    framesLeftToSkipForGroundCheckDueToFlight = FramesToSkip;
                }

                // If we're still asuming the car body is moving, return FALSE, and decrease the counter by 1.
                if (framesLeftToSkipForGroundCheckDueToFlight > 0)
                {
                    framesLeftToSkipForGroundCheckDueToFlight--;
                    return false;
                }

                return drivetrain.torque == 0 && rb.velocity.sqrMagnitude <= VelocityCheckDeadzone;
            }

            return wheel.onGroundDown;
        }

        /// <summary>
        /// Get list of disableable childs.
        /// It looks for objects that contian the name from the whiteList
        /// </summary>
        /// <returns></returns>
        internal Transform[] GetDisableableChilds()
        {
            Transform[] childs = gameObject.GetComponentsInChildren<Transform>(true).Where(t => t.gameObject.name.ContainsAny(whiteList)).ToArray();

            if (RulesManager.Instance.GetList<IgnoreRule>().Count > 0)
            {
                childs = childs.Where(t => !RulesManager.Instance.IsObjectInIgnoreList(t.gameObject)).ToArray();
            }

            return childs;
        }

        /// <summary>
        /// Returns true, if any hood is attached to the car.
        /// </summary>
        /// <returns></returns>
        private bool IsHoodAttached()
        {
            return pivotHood != null && pivotHood.childCount > 0;
        }

        /// <summary>
        /// Toggles all renderers of the Satsuma,
        /// </summary>
        /// <param name="enabled">If set to true, the renderers are enabled.</param>
        private void RenderersCulling(bool enabled)
        {
            if (RulesManager.Instance.SpecialRules.SatsumaIgnoreRenderers) enabled = true;

            for (int i = 0; i < renderers.Count; i++)
            {
                try
                {
                    if (renderers[i] == null)
                        continue;

                    // Skip renderer if its root is not Satsuma.
                    if (renderers[i].transform.root.gameObject != this.gameObject)
                        continue;

                    renderers[i].enabled = enabled;
                }
                catch (System.Exception ex)
                {
                    ExceptionManager.New(ex, true, "SATSUMA_RENDERER_TOGGLE_ISSUE");
                }
            }
        }

        /// <summary>
        /// We're forcing to keep his rotation.
        /// For some reason it keeps shaking otherwise.
        /// </summary>
        public void ForceRotation()
        {
            if (MopSettings.Mode == PerformanceMode.Safe || carDynamics == null || IsPlayerInThisCar() || IsRopeHooked()) return;

            if (!hasBeenMovedByFleetari && !carDynamics.enabled)
            {
                transform.localRotation = lastGoodRotation;
                transform.localPosition = lastGoodPosition;
            }
        }

        /// <summary>
        /// Returns true, if the key object is active.
        /// </summary>
        /// <returns></returns>
        public bool IsKeyInserted()
        {
            if (key == null)
            {
                return true;
            }

            return key.activeSelf;
        }

        /// <summary>
        /// Toggles some of the elements of that car, depneding on the of player to Satsuma.
        /// </summary>
        /// <param name="distance"></param>
        public void ToggleElements(float distance)
        {
            try
            {
                bool onEngine = distance < 2;
                bool onClose = distance <= 10 * MopSettings.ActiveDistanceMultiplicationValue;
                bool onFar = distance <= 20 * MopSettings.ActiveDistanceMultiplicationValue;

                if (Toggle == IgnoreToggle || IsKeyInserted() || IsSatsumaInParcFerme || IsSatsumaInInspectionArea || IsMoving() || (drivingAI != null && drivingAI.enabled))
                {
                    onEngine = true;
                    onClose = true;
                    onFar = true;
                }

                if (satsumaOnActionObjects != null)
                {
                    for (int i = 0; i < satsumaOnActionObjects.Count; i++)
                    {
                        SatsumaOnActionObjects sao = satsumaOnActionObjects[i];
                        if (sao == null)
                        {
                            continue;
                        }

                        PlayMakerFSM fsm = sao.FSM;
                        if (fsm != null)
                        {
                            switch (sao.EnableOn)
                            {
                                case SatsumaEnableOn.OnEngine:
                                    fsm.enabled = onEngine;
                                    break;
                                case SatsumaEnableOn.OnPlayerClose:
                                    fsm.enabled = onClose;
                                    break;
                                case SatsumaEnableOn.OnPlayerFar:
                                    fsm.enabled = onFar;
                                    break;
                            }
                        }

                        GameObject gm = sao.GameObject;
                        if (gm != null)
                        {
                            switch (sao.EnableOn)
                            {
                                case SatsumaEnableOn.OnEngine:
                                    gm.SetActive(onEngine);
                                    break;
                                case SatsumaEnableOn.OnPlayerClose:
                                    gm.SetActive(onClose);
                                    break;
                                case SatsumaEnableOn.OnPlayerFar:
                                    gm.SetActive(onFar);
                                    break;
                            }
                        }
                    }
                }

                if (onEngine)
                {
                    // This script fixes the issue with bolts staying unbolted, with parts internally being fully bolted.
                    if (maskedFixStages < 2 && maskedElements != null)
                    {
                        for (int i = 0; i < maskedElements.Count; i++)
                        {
                            GameObject gm = maskedElements.ElementAt(i).Key;
                            if (gm == null)
                            {
                                continue;
                            }

                            switch (maskedFixStages)
                            {
                                case 0:
                                    gm.SetActive(true);
                                    break;
                                case 1:
                                    gm.SetActive(maskedElements.ElementAt(i).Value);
                                    break;
                            }
                        }
                        maskedFixStages++;
                    }
                }
                else
                {
                    if (cooldownTick != null)
                    {
                        cooldownTick.SetActive(false);
                    }
                }

                if (onClose)
                {
                    if (!renderersOnCloseEnabled)
                    {
                        renderersOnCloseEnabled = true;
                        RenderersCulling(true);
                    }
                }
                else
                {
                    renderersOnCloseEnabled = false;
                }

                if (onFar)
                {
                    hasBeenMovedByFleetari = false;
                    UnglueAll();
                }
            }
            catch (System.Exception ex)
            {
                ExceptionManager.New(ex, true, "SATSUMA_TOGGLE_ELEMENTS_ERROR");
            }
        }

        /// <summary>
        /// Toggles Satsuma's dashboard illumination.
        /// </summary>
        /// <param name="enabled"></param>
        private void ToggleDashIllumination(bool enabled)
        {
            if (dashboardMaterials == null || dashboardMaterials.Count == 0 || dashboardMaterials[0] == null)
            {
                return;
            }

            if ((dashboardMaterials[0].GetFloat("_Intensity") == 0 && !enabled) || (dashboardMaterials[0].GetFloat("_Intensity") == 0.2f && enabled))
            {
                return;
            }

            for (int i = 0; i < dashboardMaterials.Count; i++)
            {
                if (dashboardMaterials[i] != null)
                {
                    dashboardMaterials[i].SetFloat("_Intensity", enabled ? 0.2f : 0);
                }
            }
        }

        public GameObject GetCarBody()
        {
            var body = transform.Find("Body/car body(xxxxx)");
            return body != null ? body.gameObject : null;
        }

        public void FleetariIsMovingCar()
        {
            hasBeenMovedByFleetari = true;
        }

        internal void AddPart(SatsumaBoltsAntiReload i)
        {
            satsumaBoltsAntiReloads.Add(i);
        }

        private void UnglueAll()
        {
            if (partsUnglued) return;
            partsUnglued = true;

            List<SatsumaBoltsAntiReload> brokens = new List<SatsumaBoltsAntiReload>();

            for (int i = 0; i < satsumaBoltsAntiReloads.Count; ++i)
            {
                if (satsumaBoltsAntiReloads[i] == null)
                {
                    brokens.Add(satsumaBoltsAntiReloads[i]);
                    continue;
                }
                satsumaBoltsAntiReloads[i]?.Unglue();
            }

            ModConsole.Log($"[MOP] Found {brokens.Count} broken CorrisBoltsAntiReload.");
            foreach (var broken in brokens)
            {
                satsumaBoltsAntiReloads.Remove(broken);
            }
        }

        /// <summary>
        /// Executed when game is being saved, makes all items permamently attached to the car.
        /// </summary>
        public void OnSaveGlueAll()
        {
            for (int i = 0; i < satsumaBoltsAntiReloads.Count; ++i)
            {
                if (satsumaBoltsAntiReloads[i] == null)
                {
                    continue;
                }

                satsumaBoltsAntiReloads[i].enabled = true;
                satsumaBoltsAntiReloads[i].Glue();

                PlayMakerFSM removalFSM = satsumaBoltsAntiReloads[i].gameObject.GetPlayMaker("Removal");
                if (removalFSM)
                {
                    Object.Destroy(removalFSM);
                }
            }

            ModConsole.Log("[MOP] Glued all Corris parts.");
        }

        private List<Renderer> GetRenderersToDisable()
        {
            List<Renderer> renderers = gameObject.transform.GetComponentsInChildren<Renderer>(true).Where(r => !r.gameObject.name.ContainsAny(ignoredRendererNames)).ToList();

            if (RulesManager.Instance.GetList<IgnoreRule>().Count > 0)
            {
                renderers = renderers.Where(r => !RulesManager.Instance.IsObjectInIgnoreList(r.gameObject)).ToList();
            }

            List<Renderer> toReturn = new List<Renderer>();
            foreach (Renderer r in renderers)
            {
                try
                {
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    // Check if object is a primitive cube.
                    // If not, add it toReturn list.
                    if (!(f && f.sharedMesh.name == "Cube" && f.mesh.subMeshCount == 1 && f.mesh.vertexCount == 24))
                    {
                        toReturn.Add(r);
                    }
                }
                catch { }
            }

            return toReturn;
        }

        protected override void ApplyHingeManager()
        {
            return;
        }

        // This class tries to repair the bolts resetting.
        private void BoltsFix()
        {
            // Get all bolts.
            satsumaBoltsAntiReloads = new List<SatsumaBoltsAntiReload>();
            GameObject[] bolts = Resources.FindObjectsOfTypeAll<GameObject>().Where(obj => obj.name == "BoltPM").ToArray();
            foreach (GameObject bolt in bolts)
            {
                Transform parent = bolt.transform.parent;
                if (parent == null) continue;

                // Elements with that name have their BoltCheck parents as a grandparent (water_pump and Hooks are exception)
                if (parent.name.EqualsAny("Bolts", "_Motor", "water_pump_pulley_mesh", "Hooks", "hooks"))
                {
                    // Great-great grandparents for these ones.
                    if (parent.parent != null && parent.parent.gameObject.name.EqualsAny("water_pump_pulley_mesh", "Hooks", "hooks"))
                    {
                        if (parent.parent.parent != null) parent = parent.parent.parent;
                    }
                    else
                    {
                        if (parent.parent != null) parent = parent.parent;
                    }
                }
                // Great-great grandparents for these too...
                else if (parent.name.ContainsAny("Masked", "ValveAdjust"))
                {
                    if (parent.parent != null && parent.parent.parent != null) parent = parent.parent.parent;
                }
                // Sibling of bolt if the parent is IK_wishbone.
                else if (parent.name.StartsWith("IK_wishbone_"))
                {
                    string suffix = parent.gameObject.name.Replace("IK_wishbone_", "");
                    var sibling = parent.Find($"wishbone {suffix}(xxxxx)");
                    if (sibling != null) parent = sibling;
                }

                if (parent == null) continue;

                // Skip those if the assigned parent is one of these.
                if (parent.name.ContainsAny("bolts_shock", "shock_bottom", "halfshaft_", "pivot_steering_arm_", "OFFSET", "pivot_shock_"))
                {
                    continue;
                }

                // Skip if the parent is Pivot and the grandparent name starts with "alternator".
                if (parent.name == "Pivot" && parent.parent != null && parent.parent.gameObject.name.StartsWith("alternator"))
                {
                    continue;
                }

                if (parent.gameObject.name == "hood(Clone)")
                    continue;

                if (parent.gameObject.GetComponent<SatsumaBoltsAntiReload>() == null)
                {
                    parent.gameObject.AddComponent<SatsumaBoltsAntiReload>();
                }
            }

            // Halfshafts hook.
            GameObject[] halfshafts = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == "halfshaft(xxxxx)").ToArray();
            foreach (GameObject halfshaft in halfshafts)
            {
                if (halfshaft.GetComponent<SatsumaBoltsAntiReload>() == null)
                    halfshaft.AddComponent<SatsumaBoltsAntiReload>();
            }

            // Engine Block.
            var block = GameObject.Find("block(Clone)");
            if (block != null && block.GetComponent<SatsumaBoltsAntiReload>() == null)
            {
                block.AddComponent<SatsumaBoltsAntiReload>();
            }

            // Steering rods.
            var steeringFr = transform.Find("Chassis/steering rod fr(xxxxx)");
            if (steeringFr != null && steeringFr.gameObject.GetComponent<SatsumaBoltsAntiReload>() == null)
            {
                steeringFr.gameObject.AddComponent<SatsumaBoltsAntiReload>();
            }
            var steeringFl = transform.Find("Chassis/steering rod fl(xxxxx)");
            if (steeringFl != null && steeringFl.gameObject.GetComponent<SatsumaBoltsAntiReload>() == null)
            {
                steeringFl.gameObject.AddComponent<SatsumaBoltsAntiReload>();
            }

            // Fuel line.
            var fuelLine = transform.Find("MiscParts/fuel line(xxxxx)");
            if (fuelLine != null && fuelLine.gameObject.GetComponent<SatsumaBoltsAntiReload>() == null)
            {
                fuelLine.gameObject.AddComponent<SatsumaBoltsAntiReload>();
            }

            ModConsole.Log($"[MOP] Found {satsumaBoltsAntiReloads.Count} bolts.");
            // If there's less bolts found than the value, warn user.
            if (satsumaBoltsAntiReloads.Count < MinimumBolts)
            {
                ModConsole.Log($"<color=yellow>[MOP] Only {satsumaBoltsAntiReloads.Count} out of expected {MinimumBolts} have been reset!</color>");
            }
        }

        protected override void LoadColliders()
        {
            return;
        }
    }
}
