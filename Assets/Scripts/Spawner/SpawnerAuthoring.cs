using Unity.Entities;
using UnityEngine;

public struct Spawner : IComponentData
{
    public Entity Player;
}

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
