using Unity.Entities;
using Unity.Mathematics;

// In which group should it update?
// This script should be responsible for getting other player inputs and syncing them with the local game state

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