using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    public partial class InputGatherSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<MyCapsulesCustomInputs>();
            // RequireForUpdate<InputBufferData<MyCapsulesCustomInputs>>();
        }

        protected override void OnUpdate()
        {
            var inputBuffer =  SystemAPI.GetSingletonBuffer<InputBufferData<MyCapsulesCustomInputs>>();
            Debug.LogWarning(inputBuffer.Length);
            foreach (var inputs in SystemAPI.Query<RefRW<MyCapsulesCustomInputs>>()) // I should get only one entity here
            { // check somehow if it was singleton
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
                
                inputs.ValueRW.horizontalInput = horizontalInput;
                inputs.ValueRW.verticalInput = verticalInput;
                
                inputBuffer.Add(new InputBufferData<MyCapsulesCustomInputs>
                {
                    networkTick = 0,
                    InternalInput = inputs.ValueRW
                });
            }
            
            
            
            // if(SystemAPI.TryGetSingleton<MyCapsulesCustomInputs>(out var inputComponent))
            // {
            //     int horizontalInput = 0;
            //     int verticalInput = 0;
            //
            //     if (World.Name == "ClientWorld2" || World.Name == "ClientWorld") // for local testing purposes
            //     {
            //         horizontalInput = Input.GetKey(KeyCode.A) ? -1 : Input.GetKey(KeyCode.D) ? 1 : 0;
            //         verticalInput = Input.GetKey(KeyCode.S) ? -1 : Input.GetKey(KeyCode.W) ? 1 : 0;
            //     }
            //     else if (World.Name != "ClientWorld3")
            //     {
            //         horizontalInput = Input.GetKey(KeyCode.LeftArrow) ? -1 : Input.GetKey(KeyCode.RightArrow) ? 1 : 0;
            //         verticalInput = Input.GetKey(KeyCode.DownArrow) ? -1 : Input.GetKey(KeyCode.UpArrow) ? 1 : 0;
            //     }
            //     else
            //     {
            //         Debug.LogError("Invalid world name!");
            //         return;
            //     }
            //     
            //     inputComponent.horizontalInput = horizontalInput;
            //     inputComponent.verticalInput = verticalInput;
            //     SystemAPI.SetSingleton(inputComponent);
            // }
            // else
            // {
            //     Debug.LogError("No input singleton present!");
            // }
        }
    }