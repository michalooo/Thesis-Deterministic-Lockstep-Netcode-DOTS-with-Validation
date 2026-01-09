using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// MonoBehaviour for authoring deterministic entities.
    /// Adds the whitelist tag and automatically assigns a deterministic entity ID.
    /// </summary>
    public class DeterministicEntityAuthoring : MonoBehaviour
    {
        [Tooltip("If true, this entity will be included in whitelist-based validation.")]
        public bool includeInWhitelistValidation = true;
        
        class Baker : Baker<DeterministicEntityAuthoring>
        {
            public override void Bake(DeterministicEntityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Add deterministic entity ID
                var id = DeterministicLogger.Instance?.GetNextEntityId() ?? 0;
                AddComponent(entity, new DeterministicEntityID { id = id });
                
                // Add whitelist tag if enabled
                if (authoring.includeInWhitelistValidation)
                {
                    AddComponent<CountEntityForWhitelistedDeterminismValidation>(entity);
                }
            }
        }
    }
}
