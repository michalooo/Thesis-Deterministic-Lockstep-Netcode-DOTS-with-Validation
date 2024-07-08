using System;
using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// System that gathers input from the player and stores it in a singleton component representing current inputs of the player.
    /// Those inputs are transformed from raw values like "w" into aproperiate values for the game input struct.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateBefore(typeof(DeterministicSimulationSystemGroup))]
    public partial struct InputGatherSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PongInputs>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            if(SystemAPI.TryGetSingletonRW<PongInputs>(out var inputComponent))
            {
                int verticalInput = 0;

                if (state.World.Name == "ClientWorld") // for local testing purposes
                {
                    verticalInput = Input.GetKey(KeyCode.S) ? -50 : Input.GetKey(KeyCode.W) ? 50 : 0;
                }
                else if (state.World.Name == "ClientWorld1" || state.World.Name == "ClientWorld2")
                {
                    verticalInput = Input.GetKey(KeyCode.DownArrow) ? -50 : Input.GetKey(KeyCode.UpArrow) ? 50 : 0;
                }
                else
                {
                    throw new Exception("Invalid world name!");
                }
                
                inputComponent.ValueRW.verticalInput = verticalInput;
                SystemAPI.SetSingleton(inputComponent.ValueRO);
            }
            else
            {
                throw new Exception("No input singleton present!");
            }
        }
    }
}