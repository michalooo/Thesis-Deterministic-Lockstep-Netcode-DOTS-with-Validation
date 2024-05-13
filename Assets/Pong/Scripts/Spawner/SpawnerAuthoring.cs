using Unity.Entities;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// Component used to store the player prefab entity
    /// </summary>
    public struct Spawner : IComponentData
    {
        public Entity Player;
    }

    /// <summary>
    /// Authoring function for the Spawner
    /// </summary>
    public class SpawnerAuthoring : MonoBehaviour
    {
        public GameObject Player;

        class Baker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                var component = default(Spawner);
                component.Player = GetEntity(authoring.Player, TransformUsageFlags.Dynamic);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
    }
}