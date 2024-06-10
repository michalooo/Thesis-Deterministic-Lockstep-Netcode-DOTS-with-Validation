using Unity.Burst;

namespace PongGame
{
    using DeterministicLockstep;
    using Unity.Entities;
    using UnityEngine;

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
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
                    verticalInput = Input.GetKey(KeyCode.S) ? -1 : Input.GetKey(KeyCode.W) ? 1 : 0;
                }
                else if (state.World.Name == "ClientWorld1" || state.World.Name == "ClientWorld2")
                {
                    verticalInput = Input.GetKey(KeyCode.DownArrow) ? -1 : Input.GetKey(KeyCode.UpArrow) ? 1 : 0;
                }
                else
                {
                    Debug.LogError("Invalid world name!");
                    return;
                }
                
                inputComponent.ValueRW.verticalInput = verticalInput;
                SystemAPI.SetSingleton(inputComponent.ValueRO);
            }
            else
            {
                Debug.LogError("No input singleton present!");
            }
        }
    }
}