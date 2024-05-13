using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class InputGatherSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (inputComponent, inputEntity) in SystemAPI.Query<RefRW<CapsulesInputs>>().WithEntityAccess())
            {
                int horizontalInput;
                int verticalInput;

                if (World.Name == "ClientWorld2") // for local testing purposes
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

                inputComponent.ValueRW.horizontalInput = horizontalInput;
                inputComponent.ValueRW.verticalInput = verticalInput;
            }
        }
    }