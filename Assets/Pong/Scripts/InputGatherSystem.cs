namespace PongGame
{
    using DeterministicLockstep;
    using Unity.Entities;
    using UnityEngine;

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    public partial class InputGatherSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PongInputs>();
        }

        protected override void OnUpdate()
        {
            if(SystemAPI.TryGetSingletonRW<PongInputs>(out var inputComponent))
            {
                int verticalInput = 0;

                if (World.Name == "ClientWorld") // for local testing purposes
                {
                    verticalInput = Input.GetKey(KeyCode.S) ? -1 : Input.GetKey(KeyCode.W) ? 1 : 0;
                }
                else if (World.Name == "ClientWorld1")
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