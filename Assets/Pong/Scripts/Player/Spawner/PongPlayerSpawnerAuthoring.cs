using Unity.Entities;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// Component used to store the player prefab entity for spawner
    /// </summary>
    public struct PongPlayerSpawner : IComponentData
    {
        public Entity player;
    }

    /// <summary>
    /// Authoring function for the Spawner
    /// </summary>
    public class PongPlayerSpawnerAuthoring : MonoBehaviour
    {
        public GameObject player;

        class Baker : Baker<PongPlayerSpawnerAuthoring>
        {
            public override void Bake(PongPlayerSpawnerAuthoring authoring)
            {
                var pongPlayerSpawnerComponent = default(PongPlayerSpawner);
                pongPlayerSpawnerComponent.player = GetEntity(authoring.player, TransformUsageFlags.Dynamic);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, pongPlayerSpawnerComponent);
            }
        }
        
    }
}