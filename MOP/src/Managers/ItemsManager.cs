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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HutongGames.PlayMaker;

using MOP.FSM;
using MOP.Common;
using MOP.Items;
using MOP.Items.Cases;
using MOP.Items.Helpers;
using MOP.Helpers;
using MOP.FSM.Actions;
using MOP.Common.Interfaces;

namespace MOP.Managers
{
    class ItemsManager : IManager<ItemBehaviour>
    {
        static ItemsManager instance;
        public static ItemsManager Instance {
            get
            {
                if (instance == null)
                {
                    instance = new ItemsManager();
                }
                return instance;
            }
        }

        public ItemBehaviour this[int index] => itemHooks[index];

        private readonly string[] nameList =
        {
            "afrgauge", "alternator", "alternator belt", "amplifier", "antenna", "auxshaft",
            "auxsprocket", "ax", "axle", "basketball", "battery", "beer case",
            "block", "body", "bootlid", "booze", "box", "brake fluid",
            "brakehose", "brakepad", "bucket", "bucket lid", "bumperfront", "bumperrear",
            "cam", "camsprocket", "camera", "carb2brl", "carb4brl", "carbchamber",
            "carbreserve", "car jack", "cd player", "charcoal", "chips", "cigarettes",
            "classifieds", "clutchdisc", "coffee cup", "coffee pan", "coilspring", "coolant",
            "crankpulley", "diesel", "digging bar", "dipper", "distributor", "doorleft",
            "doorright", "electrics", "empty", "energydrink", "exhaustf", "exhaustr",
            "exhausts", "exhausttip", "fanbelt", "fenderleft", "fenderright", "fire extinguisher",
            "fire extinguisher holder", "firewood", "fireworks bag", "fish trap", "flashlight",
            "floor jack", "flywheel", "flarefl", "flarefr", "flarerl", "flarerra",
            "fspoiler", "fuelcell", "fuelpump", "fuse", "fuse holder", "fuse package",
            "garbage barrel", "gasoline", "gearbox", "grill", "grille", "ground coffee",
            "halfshaft", "handbrake", "harness", "headgasket", "headlight left", "headlight right",
            "helmet", "hoistarm", "hood", "icescraper", "instrumentpanel", "interiortrim",
            "jacket", "juice", "juiceconcentrate", "kilju", "lantern", "light bulb",
            "lightbar", "log", "macaron box", "mainbearing", "marker light left", "marker light right",
            "marker lights", "milk", "mosquito spray", "motor hoist", "motor oil",
            "mudflap", "muffler", "notepad", "oil filter", "oilpump", "parts magazine",
            "pike", "pistons", "pizza", "plugwires", "portableradiobatteries", "potato chips",
            "powerbrakes", "raceseat", "raceshock", "radiator", "radio", "ratchet set",
            "rocker shaft", "rockers", "rspoiler", "sausages", "screwdriver", "seat driver",
            "seats", "shopping bag", "skidplate", "skirt", "sledgehammer", "sofa",
            "spanner set", "spark plug", "spark plug box", "sparkplug socket", "sparkplugs",
            "sprkplug", "spray can", "spring", "starter", "steerrace", "steeringwheel",
            "subwoofer", "sugar", "table", "tachometer", "thermostat", "thermostathousing",
            "timingbelt", "tv remote control", "two stroke fuel", "warning triangle", "water bucket",
            "water pump", "water pump pulley", "wheel cover", "wheelset hayosiko", "wheelset octo",
            "wheelset racing", "wheelset rally", "wheelset slot", "wheelset spoke", "wheelset steelwide",
            "wheelset turbine", "windshield", "yeast"
        };

        // List of ItemHooks that are a child of objects.
        readonly List<ItemBehaviour> itemHooks = new List<ItemBehaviour>();

        private Transform lostSpawner, landfillSpawn;

        // "Radiator hose3" stuff.
        private PlayMakerFSM radiatorHose3Database;
        private GameObject realRadiatorHose;

        /// <summary>
        /// Initialize Items class.
        /// </summary>
        private ItemsManager() { }

        internal void Initialize()
        {
            var lostSpawnerObj = GameObject.Find("LostSpawner");
            lostSpawner = lostSpawnerObj != null ? lostSpawnerObj.transform : null;

            var landfillObj = GameObject.Find("LANDFILL");
            landfillSpawn = landfillObj != null ? landfillObj.transform.Find("LandfillSpawn") : null;

            GameObject spawnerObj = GameObject.Find("Spawner");
            if (spawnerObj != null)
            {
                Transform spawner = spawnerObj.transform;
                var createItems = spawner.Find("CreateItems");
                if (createItems != null) InjectSpawnScripts(createItems.gameObject);

                var createSpraycans = spawner.Find("CreateSpraycans");
                if (createSpraycans != null) InjectSpawnScripts(createSpraycans.gameObject);

                var createShoppingbag = spawner.Find("CreateShoppingbag");
                if (createShoppingbag != null) InjectSpawnScripts(createShoppingbag.gameObject);

                var createMooseMeat = spawner.Find("CreateMooseMeat");
                if (createMooseMeat != null) InjectSpawnScripts(createMooseMeat.gameObject);
            }

            var fishTrapObj = GameObject.Find("fish trap(itemx)");
            if (fishTrapObj != null)
            {
                var spawnTrans = fishTrapObj.transform.Find("Spawn");
                if (spawnTrans != null) InjectSpawnScripts(spawnTrans.gameObject);
            }

            GetCanTrigger();

            // Car parts order bill hook.
            LoadStoreHook();

            InitializeList();

            // Hooks all bottles and adds adds BeerBottle script to them, which deletes the object and adds the ItemHook to them.
            HookThrowableBottles();

            // Hook the prefab of firewood.
            HookFirewood();

            // Hook the prefab of log.
            HookPrefabLog();

            // Fix for radiator hose 3.
            CreateRadiatorHoseFix();
        }

        private void CreateRadiatorHoseFix()
        {
            var dbMechRadHose3 = GameObject.Find("Database/DatabaseMechanics/RadiatorHose3");
            radiatorHose3Database = dbMechRadHose3 != null ? dbMechRadHose3.GetPlayMaker("Data") : null;
            
            GameObject attachedHose = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "radiator hose3(xxxxx)");
            realRadiatorHose = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "radiator hose3(Clone)");
            
            if (realRadiatorHose != null)
            {
                GameObject dummy = GameObject.Instantiate(realRadiatorHose); // used for spawning the radiator hose3 after it gets detached.
                var dummyItemBeh = dummy.GetComponent<ItemBehaviour>();
                if (dummyItemBeh != null)
                {
                    UnityEngine.Object.Destroy(dummyItemBeh);
                }
                dummy.SetActive(false);
                dummy.name = dummy.name.Replace("(Clone)(Clone)", "(Clone)");
            }

            Transform t = null;
            try
            {
                t = SaveManager.GetRadiatorHose3Transform();
            }
            catch (Exception ex)
            {
                ModConsole.Log("[MOP] SaveManager.GetRadiatorHose3Transform() failed: " + ex.Message);
            }

            if (attachedHose != null && realRadiatorHose != null && t != null)
            {
                if (!attachedHose.activeSelf)
                {
                    realRadiatorHose.transform.position = t.position;
                    realRadiatorHose.transform.rotation = t.rotation;
                    realRadiatorHose.SetActive(true);
                }
            }
            
            if (radiatorHose3Database != null && radiatorHose3Database.FsmVariables != null && realRadiatorHose != null)
            {
                var spawnThisVar = radiatorHose3Database.FsmVariables.GameObjectVariables.FirstOrDefault(g => g.Name == "SpawnThis");
                if (spawnThisVar != null)
                {
                    spawnThisVar.Value = realRadiatorHose;
                }
            }
        }

        private static void HookPrefabLog()
        {
            GameObject logPrefab = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(f => f.name.EqualsAny("log") && f.GetComponent<PlayMakerFSM>() == null);
            if (logPrefab != null)
            {
                if (logPrefab.GetComponent<ItemBehaviour>() == null)
                    logPrefab.AddComponent<ItemBehaviour>();
                
                var childLog = logPrefab.transform.Find("log(Clone)");
                if (childLog != null)
                {
                    if (childLog.gameObject.GetComponent<ItemBehaviour>() == null)
                        childLog.gameObject.AddComponent<ItemBehaviour>();
                }
            }

            GameObject[] logs = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name == "log(Clone)").ToArray();
            for (int i = 0; i < logs.Length; i++)
            {
                if (logs[i].GetComponent<ItemBehaviour>() == null)
                    logs[i].AddComponent<ItemBehaviour>();
            }
        }

        private static void HookFirewood()
        {
            GameObject firewoodPrefab = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(f => f.name.EqualsAny("firewood") && f.GetComponent<PlayMakerFSM>() == null);
            if (firewoodPrefab != null && firewoodPrefab.GetComponent<ItemBehaviour>() == null)
            {
                firewoodPrefab.AddComponent<ItemBehaviour>();
            }
        }

        private static void HookThrowableBottles()
        {
            Dictionary<string, string> names = new Dictionary<string, string>()
            {
                {
                    "BottleBeerFly",
                    "empty bottle(Clone)"
                },
                {
                    "BottleBoozeFly",
                    "empty bottle(Clone)"
                },
                {
                    "BottleSpiritFly",
                    "empty bottle(Clone)"
                },
                {
                    "CoffeeFly",
                    "empty cup(Clone)"
                },
                {
                    "MilkFly",
                    "empty pack(Clone)"
                },
                {
                    "VodkaShotFly",
                    "empty glass(Clone)"
                }
            };
            GameObject[] bottles = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(obj => names.ContainsKey(obj.name) && obj.GetComponent<PlayMakerFSM>() != null).ToArray();
            for (int i = 0; i < bottles.Length; i++)
            {
                if (bottles[i].GetComponent<ThrowableJunkBehaviour>() == null)
                    bottles[i].AddComponent<ThrowableJunkBehaviour>();
            }
        }

        private static void LoadStoreHook()
        {
            GameObject store = GameObject.Find("STORE");
            if (store == null) return;

            Transform storeLODTrans = store.transform.Find("LOD");
            if (storeLODTrans == null) return;
            GameObject storeLOD = storeLODTrans.gameObject;

            Transform activateStoreTrans = storeLOD.transform.Find("ActivateStore");
            if (activateStoreTrans == null) return;
            GameObject activateStore = activateStoreTrans.gameObject;

            // Check what is the current state of the LOD and Store logic, and store it.
            bool lodLastState = storeLOD.activeSelf;
            bool activeStore = activateStore.activeSelf;

            // Activate the store and LOD, if it's disabled.
            storeLOD.SetActive(true);
            activateStore.SetActive(true);

            // Finally we FsmInject the CashRegisterBehaviour.
            Transform postOrderTrans = activateStore.transform.Find("PostOffice/PostOrderBuy");
            GameObject postOrder = postOrderTrans != null ? postOrderTrans.gameObject : null;
            
            Transform registerTrans = store.transform.Find("StoreCashRegister/Register");
            GameObject register = registerTrans != null ? registerTrans.gameObject : null;

            if (postOrder != null && register != null)
            {
                bool isPostOrderActive = postOrder.activeSelf;
                postOrder.SetActive(true);
                var cashReg = register.GetComponent<CashRegisterBehaviour>() ?? register.AddComponent<CashRegisterBehaviour>();
                postOrder.FsmInject("State 3", cashReg.Packages);
                
                // Restore previous states of the stuff.
                postOrder.SetActive(isPostOrderActive);
            }

            storeLOD.SetActive(lodLastState);
            activateStore.SetActive(activeStore);
        }

        /// <summary>
        /// Add object hook to the list
        /// </summary>
        /// <param name="newHook"></param>
        public ItemBehaviour Add(ItemBehaviour newHook)
        {
            itemHooks.Add(newHook);
            return newHook;
        }

        /// <summary>
        /// Remove object hook from the list
        /// </summary>
        /// <param name="objectHook"></param>
        public void Remove(ItemBehaviour objectHook)
        {
            if (itemHooks.Contains(objectHook))
            {
                itemHooks.Remove(objectHook);
            }
        }

        public void RemoveAt(int index)
        {
            if (itemHooks.Contains(itemHooks[index])) itemHooks.Remove(itemHooks[index]);
        }

        public int Count => itemHooks.Count;

        /// <summary>
        /// Lists all the game objects in the game's world and that are on the whitelist,
        /// then it adds ObjectHook to them
        /// </summary>
        private void InitializeList()
        {
            // Find shopping bags in the list
            UnityEngine.Object.FindObjectsOfType<GameObject>()
                    .Where(gm => gm.name.ContainsAny(nameList) && gm.name.ContainsAny("(itemx)", "(Clone)") && gm.GetComponent<ItemBehaviour>() == null)
                    .All(gm => gm.AddComponent<ItemBehaviour>());


            // CD Player Enhanced compatibility
            if (MSCLoader.ModLoader.IsModPresent("CDPlayer"))
            {
                GameObject[] cdEnchancedObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(gm => gm.name.ContainsAny("cd case(itemy)", "CD Rack(itemy)", "cd(itemy)") && gm.activeSelf).ToArray();

                for (int i = 0; i < cdEnchancedObjects.Length; i++)
                {
                    if (cdEnchancedObjects[i].GetComponent<ItemBehaviour>() == null)
                        cdEnchancedObjects[i].AddComponent<ItemBehaviour>();
                }

                // Create shop table check, for when the CDs are bought
                GameObject itemCheck = new GameObject("MOP_ItemAreaCheck");
                itemCheck.transform.localPosition = new Vector3(-1551.303f, 4.883998f, 1182.904f);
                itemCheck.transform.localEulerAngles = new Vector3(0f, 58.00043f, 0f);
                itemCheck.transform.localScale = new Vector3(1f, 1f, 1f);
                itemCheck.AddComponent<ShopModItemSpawnCheck>();
            }
            else
            {
                GameObject[] cds = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name.ContainsAny("cd case(item", "cd(item")).ToArray();
                for (int i = 0; i < cds.Length; i++)
                {
                    if (cds[i].GetComponent<ItemBehaviour>() == null)
                        cds[i].AddComponent<ItemBehaviour>();
                }
            }

            // Get items from ITEMS object.
            GameObject itemsObject = GameObject.Find("ITEMS");
            if (itemsObject != null)
            {
                for (int i = 0; i < itemsObject.transform.childCount; i++)
                {
                    GameObject item = itemsObject.transform.GetChild(i).gameObject;
                    if (item.name == "CDs") continue;

                    try
                    {
                        if (item.GetComponent<ItemBehaviour>() == null)
                        {
                            item.AddComponent<ItemBehaviour>();
                        }
                    }
                    catch (Exception ex)
                    {
                        ExceptionManager.New(ex, false, "ITEM_LIST_AT_ITEMS_LOAD_ERROR");
                    }
                }

                // Also disable the restart for that sunnuva bitch.
                var itemsFsm = itemsObject.GetComponent<PlayMakerFSM>();
                if (itemsFsm != null && itemsFsm.Fsm != null)
                {
                    itemsFsm.Fsm.RestartOnEnable = false;
                }
            }

            // Fucking wheels.
            GameObject[] wheels = UnityEngine.Object.FindObjectsOfType<GameObject>()
                .Where(gm => gm.name.EqualsAny("wheel_regula", "wheel_offset") && gm.activeSelf).ToArray();
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i].GetComponent<ItemBehaviour>() == null)
                    wheels[i].AddComponent<ItemBehaviour>();
            }

            // Tires trigger at Fleetari's.
            GameObject wheelTrigger = new GameObject("MOP_WheelTrigger");
            wheelTrigger.transform.localPosition = new Vector3(1555.49f, 4.8f, 737);
            wheelTrigger.transform.localEulerAngles = new Vector3(1.16f, 335, 1.16f);
            wheelTrigger.AddComponent<WheelRepairJobTrigger>();

            // Hook up the envelope.
            GameObject[] envelopes = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.name.EqualsAny("envelope(xxxxx)", "lottery ticket(xxxxx)")).ToArray();
            for (int i = 0; i < envelopes.Length; i++)
            {
                if (envelopes[i].GetComponent<ItemBehaviour>() == null)
                    envelopes[i].AddComponent<ItemBehaviour>();
            }

            // Unparent all childs of CDs object.
            if (itemsObject != null)
            {
                Transform cds = itemsObject.transform.Find("CDs");
                if (cds != null)
                {
                    while (cds.childCount > 0)
                    {
                        cds.GetChild(0).parent = null;
                    }
                }
            }

            // Find the initial beer case and hook it.
            GameObject beerCaseInitial = Resources.FindObjectsOfTypeAll<GameObject>
                ().FirstOrDefault(g => g.name == "beer case(itemx)" && g.GetComponent<ItemBehaviour>() == null);
            beerCaseInitial?.AddComponent<ItemBehaviour>();
        }

        GameObject canTrigger;
        /// <summary>
        /// Returns the position of Jokke's kilju can trigger.
        /// </summary>
        public GameObject GetCanTrigger()
        {
            if (!canTrigger)
            {
                canTrigger = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "CanTrigger");
            }

            return canTrigger;
        }

        public Transform LostSpawner => lostSpawner;
        public Transform LandfillSpawn => landfillSpawn;

        internal void SetCurrentRadiatorHose(GameObject g)
        {
            realRadiatorHose = g;
        }

        internal GameObject GetRadiatorHose3()
        {
            return realRadiatorHose;
        }

        internal void OnSave()
        {
            try
            {
                if (radiatorHose3Database != null && radiatorHose3Database.FsmVariables != null)
                {
                    var spawnThisVar = radiatorHose3Database.FsmVariables.GameObjectVariables.FirstOrDefault(g => g.Name == "SpawnThis");
                    if (spawnThisVar != null)
                    {
                        spawnThisVar.Value = realRadiatorHose;
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionManager.New(ex, true, "RADIATOR_HOSE_3_DB_ERROR");
            }
        }

        public List<ItemBehaviour> GetAll => itemHooks;

        private void InjectSpawnScripts(GameObject gm)
        {
            PlayMakerFSM[] createFSMs = gm.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < createFSMs.Length; i++)
            {
                try
                {
                    PlayMakerFSM fsm = createFSMs[i];

                    FsmState state = fsm.GetState("Create");
                    if (state == null)
                    {
                        state = fsm.GetState("Create product");
                    }

                    if (state == null)
                    {
                        ModConsole.LogError($"[MOP] FSM {i} has no Create or Create product");
                        continue;
                    }

                    FsmGameObject go = fsm.FsmVariables.GetFsmGameObject("New");

                    state.AddAction(new CustomAddItemBehaviour(go));
                }
                catch (Exception ex)
                {
                    ExceptionManager.New(ex, false, "INJECT_SPAWN_SCRIPTS_ERROR");
                }
            }
        }

        private ItemBehaviour bucket;
        public ItemBehaviour GetBucket()
        {
            if (bucket == null)
            {
                bucket = itemHooks.FirstOrDefault(g => g != null && g.name == "bucket(itemx)");
            }
            return bucket;
        }

        public int EnabledCount
        {
            get
            {
                int enabled = 0;
                for (int i = 0; i < itemHooks.Count; i++)
                {
                    if (itemHooks[i] != null && itemHooks[i].ActiveSelf)
                    {
                        enabled++;
                    }
                }
                return enabled;
            }
        }

        public bool IsVanillaItem(ItemBehaviour item)
        {
            if (item.gameObject.name.Contains("haybale"))
            {
                return true;
            }

            return nameList.Contains(item.gameObject.name.Replace("(itemx)", "").Replace("(Clone)", ""));
        }
    }
}
