using Unity.Entities;
using Unity.Mathematics;

// In which group should it update?
// This script should be responsible for getting other player inputs and syncing them with the local game state
// This script will be responsible for storing the state of the simulation for later debugging purposes
// Right now to see if it works we can maybe implement "Networked cube" example and see the position changing? Or something simpler?
// host can say "you are 50 frames behind and do it in one frame"

public struct PositionComponent : IComponentData {
    public float3 position;
}

public partial class StateSync : SystemBase {
    protected override void OnUpdate() {
        Entities.ForEach((ref PositionComponent position) => {
            // Example: Send position data to other clients over the network
            // Note: You need to implement network communication
            // SendData(position.position);
        }).Schedule();
    }
}