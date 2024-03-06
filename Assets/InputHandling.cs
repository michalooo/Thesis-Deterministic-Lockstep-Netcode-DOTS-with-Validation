using Unity.Entities;
using UnityEngine;

// Decide what kind of input you want to handle (Lets try with some basic one like movement)
// We should store the input in struct and send it all every frame (there is a possibility of reducing the load if input is none but let's leave it for now)
// Should I create InputSystemGroup? start of the frame
// Test Input sending and reciving
// This script should be responsible for gathering player input every frame and sending it via transport

public struct PlayerInputComponent : IComponentData {
    public float horizontalInput;
    public float verticalInput;
}

public partial class InputHandling : SystemBase {
    
    protected override void OnUpdate() {
        // Entities.ForEach((ref PlayerInputComponent input) => {
        //     // Example: Get player input from Unity Input system
        //     input.horizontalInput = Input.GetAxis("Horizontal");
        //     input.verticalInput = Input.GetAxis("Vertical");
        //     // Update other input variables as needed
        // }).ScheduleParallel();
    }
}