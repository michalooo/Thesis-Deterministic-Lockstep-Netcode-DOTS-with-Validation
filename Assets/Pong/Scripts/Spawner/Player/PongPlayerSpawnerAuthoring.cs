using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// Component used to store the player prefab entity
    /// </summary>
    public struct PongPlayerSpawner : IComponentData
    {
        public Entity Player;
    }

    /// <summary>
    /// Authoring function for the Spawner
    /// </summary>
    public class PongPlayerSpawnerAuthoring : MonoBehaviour
    {
        public GameObject Player;

        class Baker : Baker<PongPlayerSpawnerAuthoring>
        {
            public override void Bake(PongPlayerSpawnerAuthoring authoring)
            {
                var component = default(PongPlayerSpawner);
                component.Player = GetEntity(authoring.Player, TransformUsageFlags.Dynamic);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
        
    }
}