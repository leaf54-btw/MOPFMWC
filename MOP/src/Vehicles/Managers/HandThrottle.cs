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
using UnityEngine;
using System;
using MOP.FSM;

namespace MOP.Vehicles.Managers
{
    class HandThrottle : MonoBehaviour
    {
        // This class overwrites the hand throttle systems found in Kekmet and Gifu.

        protected Drivetrain drivetrain;
        protected AxisCarController axisCarController;
        protected FsmFloat handThrottleValue;

        const float ThrottleMax = 1;

        protected string throttlePath;

        protected virtual void Start()
        {
            try
            {
                drivetrain = gameObject.GetComponent<Drivetrain>();
                axisCarController = gameObject.GetComponent<AxisCarController>();
            }
            catch (Exception ex)
            {
                ModConsole.Print($"[MOP] HandThrottle: Components not found on {gameObject.name}. Disabling optimization. Details: {ex.Message}");
                enabled = false;
                Destroy(this);
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(throttlePath))
                {
                    ModConsole.Print($"[MOP] HandThrottle: throttlePath is empty on {gameObject.name}. Disabling optimization.");
                    enabled = false;
                    Destroy(this);
                    return;
                }

                Transform handThrottle = transform.Find(throttlePath);
                if (handThrottle == null)
                {
                    if (throttlePath.StartsWith("LOD/"))
                    {
                        string fallbackPath = throttlePath.Substring(4);
                        handThrottle = transform.Find(fallbackPath);
                    }
                }

                if (handThrottle == null)
                {
                    ModConsole.Print($"[MOP] HandThrottle: throttlePath '{throttlePath}' not found on {gameObject.name}. Disabling optimization.");
                    enabled = false;
                    Destroy(this);
                    return;
                }

                PlayMakerFSM useFSM = handThrottle.GetPlayMaker("Use");
                if (useFSM == null)
                {
                    ModConsole.Print($"[MOP] HandThrottle: 'Use' FSM not found on '{throttlePath}'. Disabling optimization.");
                    enabled = false;
                    Destroy(this);
                    return;
                }

                handThrottleValue = useFSM.FsmVariables.GetFsmFloat("Throttle");
                PlayMakerFSM throttleFSM = handThrottle.GetPlayMaker("Throttle");
                if (throttleFSM != null)
                {
                    throttleFSM.enabled = false;
                }
            }
            catch (Exception ex)
            {
                ModConsole.Print($"[MOP] HandThrottle: FSM tweaks issue on {gameObject.name}. Disabling optimization. Details: {ex.Message}");
                enabled = false;
                Destroy(this);
                return;
            }
        }

        protected void Invoke()
        {
            if (!enabled) return;
            InvokeRepeating("ThrottleUpdate", 0, 0.000015f);
        }

        protected virtual void ThrottleUpdate()
        {
            if (handThrottleValue == null || drivetrain == null || axisCarController == null)
            {
                CancelInvoke("ThrottleUpdate");
                return;
            }
            IdleThrottle = handThrottleValue.Value;
            Throttle = axisCarController.throttle + IdleThrottle;
        }

        protected float Throttle
        {
            get => drivetrain != null ? drivetrain.throttle : 0f;
            set
            {
                if (drivetrain == null) return;
                if (value > ThrottleMax)
                    value = ThrottleMax;

                drivetrain.throttle = value;
            }
        }

        protected float IdleThrottle
        {
            get => drivetrain != null ? drivetrain.idlethrottle : 0f;
            set
            {
                if (drivetrain != null)
                    drivetrain.idlethrottle = value;
            }
        }
    }
}
