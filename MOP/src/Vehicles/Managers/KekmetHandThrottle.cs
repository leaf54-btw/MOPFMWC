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
using System;
using UnityEngine;
using HutongGames.PlayMaker;

using MOP.FSM;

namespace MOP.Vehicles.Managers
{
    class KekmetHandThrottle : HandThrottle
    {
        const float MaxMinimumRPM = 500;

        public KekmetHandThrottle()
        {
            throttlePath = "LOD/Dashboard/Throttle";
        }

        protected override void Start()
        {
            base.Start();

            if (!enabled) return;

            try
            {
                Transform starterTransform = transform.Find("Simulation/Starter");
                if (starterTransform == null)
                {
                    ModConsole.Print("[MOP] KekmetHandThrottle: Simulation/Starter not found. Disabling hand throttle optimization.");
                    enabled = false;
                    Destroy(this);
                    return;
                }

                GameObject starter = starterTransform.gameObject;

                starter.FsmInject("Start engine", Invoke);
                starter.FsmInject("State 1", CancelInvoke);
                Invoke();

                PlayMakerFSM starterFSM = starter.GetPlayMaker("Starter");
                if (starterFSM != null)
                {
                    FsmState startEngineState = starterFSM.GetState("Start engine");
                    if (startEngineState != null && startEngineState.Actions.Length > 3)
                    {
                        List<FsmStateAction> startEngineActions = startEngineState.Actions.ToList();
                        startEngineActions.RemoveAt(3);
                        startEngineState.Actions = startEngineActions.ToArray();
                    }

                    FsmState state1State = starterFSM.GetState("State 1");
                    if (state1State != null && state1State.Actions.Length > 1)
                    {
                        List<FsmStateAction> state1Actions = state1State.Actions.ToList();
                        state1Actions.RemoveAt(1);
                        state1State.Actions = state1Actions.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                ModConsole.Print($"[MOP] KekmetHandThrottle error: {ex.Message}. Disabling hand throttle optimization.");
                enabled = false;
                Destroy(this);
                return;
            }
        }

        protected override void ThrottleUpdate()
        {
            base.ThrottleUpdate();
            if (drivetrain != null)
            {
                MinRPM = IdleThrottle * 2500;
            }
        }

        float MinRPM
        {
            set
            {
                if (drivetrain == null) return;
                float val = value;
                if (val > MaxMinimumRPM)
                    val = MaxMinimumRPM;
                drivetrain.minRPM = val;
            }
        }
    }
}
