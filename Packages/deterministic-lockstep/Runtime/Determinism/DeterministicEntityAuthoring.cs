using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Tag component to mark an entity as part of the determinism validation checks.
    /// </summary>
    public struct CountEntityForWhitelistedDeterminismValidation : IComponentData
    {
    }
    
    public class DeterministicEntityAuthoring : MonoBehaviour
    {
        class Baker : Baker<DeterministicEntityAuthoring>
        {
            public override void Bake(DeterministicEntityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<CountEntityForWhitelistedDeterminismValidation>(entity);
            }
        }
    }
}