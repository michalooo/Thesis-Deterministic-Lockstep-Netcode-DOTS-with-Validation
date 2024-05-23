using Unity.Entities;
using UnityEngine;

namespace CapsulesGame
{
    /// <summary>
    /// Component used to store the player prefab entity
    /// </summary>
    public struct CapsulesSpawner : IComponentData
    {
        public Entity Player;
    }

    /// <summary>
    /// Authoring function for the Spawner
    /// </summary>
    public class CapsulesSpawnerAuthoring : MonoBehaviour
    {
        public GameObject Player;

        class Baker : Baker<CapsulesSpawnerAuthoring>
        {
            public override void Bake(CapsulesSpawnerAuthoring authoring)
            {
                var component = default(CapsulesSpawner);
                component.Player = GetEntity(authoring.Player, TransformUsageFlags.Dynamic);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
        
    }
}


