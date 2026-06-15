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
using System.Reflection;
using UnityEngine;

using MOP.Common.Enumerations;
using MOP.Managers;

namespace MOP.FSM
{
    static class FsmManager
    {
        public static void ResetAll()
        {
            FieldInfo[] fields = typeof(FsmManager).GetFields();
            // Loop through fields
            foreach (var field in fields)
            {
                field.SetValue(field, null);
            }
        }

        static FsmInt uncleStage;
        /// <summary>
        /// Checks if the player has the keys to the Hayosiko.
        /// </summary>
        /// <returns></returns>
        public static bool PlayerHasHayosikoKey()
        {
            // Store Uncle's PlayMakerFSM for later
            if (uncleStage == null)
            {
                var uncle = GameObject.Find("UNCLE");
                if (uncle != null)
                {
                    var pm = uncle.GetComponent<PlayMakerFSM>();
                    if (pm != null)
                    {
                        uncleStage = pm.FsmVariables.GetFsmInt("UncleStage");
                    }
                }
            }
             
            return uncleStage != null && uncleStage.Value == 5;
        }

        static FsmBool gtGrilleInstalled;
        /// <summary>
        /// Checks if GT Grille is attached to the car.
        /// </summary>
        /// <returns></returns>
        public static bool IsGTGrilleInstalled()
        {
            if (gtGrilleInstalled == null)
            {
                var gtGrille = GameObject.Find("Database/DatabaseOrders/GrilleGT");
                if (gtGrille != null)
                {
                    var pm = gtGrille.GetComponent<PlayMakerFSM>();
                    if (pm != null)
                    {
                        gtGrilleInstalled = pm.FsmVariables.GetFsmBool("Installed");
                    }
                }
            }

            return gtGrilleInstalled != null && gtGrilleInstalled.Value;
        }

        static FsmBool order;
        /// <summary>
        /// Checks if repair shop job has been ordered.
        /// </summary>
        /// <returns></returns>
        public static bool IsRepairshopJobOrdered()
        {
            if (order == null)
            {
                var repairShopOrder = GameObject.Find("REPAIRSHOP/Order");
                if (repairShopOrder != null)
                {
                    var pm = repairShopOrder.GetComponent<PlayMakerFSM>();
                    if (pm != null)
                    {
                        order = pm.FsmVariables.GetFsmBool("_Order");
                    }
                }
            }

            return order != null && order.Value;
        }

        static FsmString playerCurrentVehicle;
        /// <summary>
        /// Checks PlayerCurrentVehicle global value. If it says "Satsuma", that means player sits in Satsuma.
        /// </summary>
        /// <returns></returns>
        public static bool IsPlayerInSatsuma()
        {
            if (playerCurrentVehicle == null)
                playerCurrentVehicle = PlayMakerGlobals.Instance.Variables.FindFsmString("PlayerCurrentVehicle");

            return playerCurrentVehicle != null && (playerCurrentVehicle.Value == "CORRIS" || playerCurrentVehicle.Value == "Corris" || playerCurrentVehicle.Value == "Machtwagen" || playerCurrentVehicle.Value == "Satsuma");
        }

        public static bool IsPlayerInCar()
        {
            if (playerCurrentVehicle == null)
                playerCurrentVehicle = PlayMakerGlobals.Instance.Variables.FindFsmString("PlayerCurrentVehicle");

            return playerCurrentVehicle != null && playerCurrentVehicle.Value.Length > 0;
        }

        static FsmInt farmJobStage;
        /// <summary>
        /// Checks if the JobStage of Farm Job has rechead 3rd stage.
        /// On 3rd stage, the combine is available to the player.
        /// </summary>
        /// <returns></returns>
        public static bool IsCombineAvailable()
        {
            if (farmJobStage == null)
            {
                var farmJob = GameObject.Find("JOBS/Farm/Job");
                if (farmJob != null)
                {
                    var pm = farmJob.GetComponent<PlayMakerFSM>();
                    if (pm != null)
                    {
                        farmJobStage = pm.FsmVariables.GetFsmInt("JobStage");
                    }
                }
            }

            return farmJobStage != null && farmJobStage.Value >= 3;
        }

        static FsmBool hoodBolted;
        public static bool IsStockHoodBolted()
        {
            if (hoodBolted == null)
            {
                var dbHood = GameObject.Find("Database/DatabaseBody/Hood");
                if (dbHood != null)
                {
                    var pm = dbHood.GetComponent<PlayMakerFSM>();
                    if (pm != null)
                    {
                        hoodBolted = pm.FsmVariables.GetFsmBool("Bolted");
                    }
                }
            }

            return hoodBolted != null && hoodBolted.Value;
        }

        static FsmBool fiberHoodBolted;
        public static bool IsFiberHoodBolted()
        {
            if (fiberHoodBolted == null)
            {
                var dbFiberHood = GameObject.Find("Database/DatabaseOrders/Fiberglass Hood");
                if (dbFiberHood != null)
                {
                    var pm = dbFiberHood.GetComponent<PlayMakerFSM>();
                    if (pm != null)
                    {
                        fiberHoodBolted = pm.FsmVariables.GetFsmBool("Bolted");
                    }
                }
            }

            return fiberHoodBolted != null && fiberHoodBolted.Value;
        }

        static GameObject triggerHood;
        public static void ForceHoodAssemble()
        {
            if (triggerHood == null)
            {
                var vehicle = VehicleManager.Instance.GetVehicle(VehiclesTypes.Satsuma);
                if (vehicle != null)
                {
                    var trigger = vehicle.transform.Find("Body/trigger_hood");
                    if (trigger != null)
                    {
                        triggerHood = trigger.gameObject;
                    }
                }
            }

            if (triggerHood != null)
            {
                triggerHood.SetActive(true);
                triggerHood.GetComponent<PlayMakerFSM>().SendEvent("ASSEMBLE");
            }
        }

        static GameObject triggerBumperRear;
        public static void ForceRearBumperAssemble()
        {
            if (triggerBumperRear == null)
            {
                var vehicle = VehicleManager.Instance.GetVehicle(VehiclesTypes.Satsuma);
                if (vehicle != null)
                {
                    var trigger = vehicle.transform.Find("Body/trigger_bumper_rear");
                    if (trigger != null)
                    {
                        triggerBumperRear = trigger.gameObject;
                    }
                }
            }

            if (triggerBumperRear != null)
            {
                triggerBumperRear.SetActive(true);
                triggerBumperRear.GetComponent<PlayMakerFSM>().SendEvent("ASSEMBLE");
            }
        }

        static GameObject kekmetTrailerRemove;
        public static bool IsTrailerAttached()
        {
            if (kekmetTrailerRemove == null)
            {
                var kekmet = VehicleManager.Instance.GetVehicle(VehiclesTypes.Kekmet);
                if (kekmet != null)
                {
                    var trailerRemove = kekmet.transform.Find("Trailer/Remove");
                    if (trailerRemove != null)
                    {
                        kekmetTrailerRemove = trailerRemove.gameObject;
                    }
                }
            }

            return kekmetTrailerRemove != null && kekmetTrailerRemove.activeSelf;
        }

        static FsmFloat battery1;
        static FsmFloat battery2;
        public static bool IsBatteryInstalled()
        {
            if (battery1 == null || battery2 == null)
            {
                var dbBattery = GameObject.Find("Database/PartsStatus/Battery");
                if (dbBattery != null)
                {
                    var pm = dbBattery.GetComponent<PlayMakerFSM>();
                    if (pm != null)
                    {
                        battery1 = pm.FsmVariables.GetFsmFloat("Bolt1");
                        battery2 = pm.FsmVariables.GetFsmFloat("Bolt2");
                    }
                }
            }

            return (battery1 != null && battery1.Value > 0) || (battery2 != null && battery2.Value > 0);
        }

        static FsmBool playerHelmet;
        public static bool PlayerHelmetOn()
        {
            if (playerHelmet == null)
                playerHelmet = PlayMakerGlobals.Instance.Variables.GetFsmBool("PlayerHelmet");

            return playerHelmet != null && playerHelmet.Value;
        }

        static FsmFloat drawDistance;
        public static float GetDrawDistance()
        {
            if (drawDistance == null)
            {
                var options = GameObject.Find("Systems/Options");
                if (options != null)
                {
                    var pm = options.GetPlayMaker("GFX");
                    if (pm != null)
                    {
                        drawDistance = pm.FsmVariables.GetFsmFloat("DrawDistance");
                    }
                }
            }

            return drawDistance != null ? drawDistance.Value : 2000f;

        }

        static FsmBool suskiLarge;
        public static bool IsSuskiLargeCall()
        {
            if (suskiLarge == null)
            {
                var phone = GameObject.Find("Telephone/Logic/UseHandle");
                if (phone != null)
                {
                    var pm = phone.GetComponent<PlayMakerFSM>();
                    if (pm != null)
                    {
                        suskiLarge = pm.FsmVariables.GetFsmBool("SuskiLarge");
                    }
                }
            }

            return suskiLarge != null && suskiLarge.Value;
        }

        static FsmBool playerInMenu;
        public static bool PlayerInMenu
        {            
            get
            {
                if (playerInMenu == null)
                    playerInMenu = PlayMakerGlobals.Instance.Variables.GetFsmBool("PlayerInMenu");

                return playerInMenu.Value;
            }
            set
            {
                if (playerInMenu == null)
                    playerInMenu = PlayMakerGlobals.Instance.Variables.GetFsmBool("PlayerInMenu");

                playerInMenu.Value = value; 
            }
        }

        static FsmBool playerComputer;
        public static bool PlayerComputer
        {
            get
            {
                if (playerComputer == null)
                    playerComputer = PlayMakerGlobals.Instance.Variables.GetFsmBool("PlayerComputer");

                return playerComputer.Value;
            }
        }

        static FsmBool shadowsHouse;
        public static bool ShadowsHouse
        {
            get
            {
                if (shadowsHouse == null)
                {
                    GameObject systems = GameObject.Find("Systems");
                    if (systems != null)
                    {
                        Transform options = systems.transform.Find("Options");
                        if (options != null)
                        {
                            PlayMakerFSM pm = options.GetPlayMaker("GFX");
                            if (pm != null && pm.FsmVariables != null)
                            {
                                shadowsHouse = pm.FsmVariables.GetFsmBool("ShadowsHouse");
                            }
                        }
                    }
                }

                return shadowsHouse != null ? shadowsHouse.Value : true;
            }
        }

        static FsmFloat hour;
        public static int Hour
        {
            get
            {
                if (hour == null)
                {
                    GameObject sun = GameObject.Find("MAP/SUN/Pivot/SUN");
                    if (sun != null)
                    {
                        PlayMakerFSM pm = sun.GetPlayMaker("Clock");
                        if (pm != null && pm.FsmVariables != null)
                        {
                            hour = pm.FsmVariables.GetFsmFloat("Hours");
                        }
                    }
                }

                return hour != null ? (int)hour.Value : 12;
            }
        }
    }
}
