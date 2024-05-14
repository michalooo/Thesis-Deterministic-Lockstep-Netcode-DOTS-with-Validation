using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    public partial class InputGatherSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<CapsulesInputs>();
        }

        protected override void OnUpdate()
        {
            if(SystemAPI.TryGetSingleton<CapsulesInputs>(out var inputComponent))
            {
                int horizontalInput = 0;
                int verticalInput = 0;

                if (World.Name == "ClientWorld2" || World.Name == "ClientWorld") // for local testing purposes
                {
                    horizontalInput = Input.GetKey(KeyCode.A) ? -1 : Input.GetKey(KeyCode.D) ? 1 : 0;
                    verticalInput = Input.GetKey(KeyCode.S) ? -1 : Input.GetKey(KeyCode.W) ? 1 : 0;
                }
                else if (World.Name != "ClientWorld3")
                {
                    horizontalInput = Input.GetKey(KeyCode.LeftArrow) ? -1 : Input.GetKey(KeyCode.RightArrow) ? 1 : 0;
                    verticalInput = Input.GetKey(KeyCode.DownArrow) ? -1 : Input.GetKey(KeyCode.UpArrow) ? 1 : 0;
                }
                else
                {
                    Debug.LogError("Invalid world name!");
                    return;
                }
                
                inputComponent.horizontalInput = horizontalInput;
                inputComponent.verticalInput = verticalInput;
                SystemAPI.SetSingleton(inputComponent);
            }
            else
            {
                Debug.LogError("No input singleton present!");
            }
        }
    }