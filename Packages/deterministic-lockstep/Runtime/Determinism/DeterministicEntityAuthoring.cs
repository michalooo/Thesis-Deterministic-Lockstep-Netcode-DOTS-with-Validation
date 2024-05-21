using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Tag component to mark an entity as part of the deterministic simulation checks.
    /// </summary>
    public struct EnsureDeterministicBehaviour : IComponentData
    {
    }
    
    /// <summary>
    /// Behaviour to add the DeterministicSimulation component to an entity.
    /// </summary>
    public class DeterministicEntityAuthoring : MonoBehaviour
    {

        class Baker : Baker<DeterministicEntityAuthoring>
        {
            public override void Bake(DeterministicEntityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnsureDeterministicBehaviour>(entity);
            }
        }
    }
}