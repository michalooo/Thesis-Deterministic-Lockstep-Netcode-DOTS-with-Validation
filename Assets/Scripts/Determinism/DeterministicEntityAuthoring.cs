using Unity.Entities;
using UnityEngine;

/// <summary>
/// Tag component to mark an entity as part of the deterministic simulation checks.
/// </summary>
public struct DeterministicSimulation : IComponentData
{

}
//public struct UseInDeterministicFastHashCalculation : IComponentData //comment for now. Maybe in performance, environment flag
//{ // full hash per system for user experience
//}

/// <summary>
/// Behaviour to add the DeterministicSimulation component to an entity.
/// </summary>
public class DeterministicEntityAuthoring : MonoBehaviour
{
    //public bool UseInDeterministicFastHashCalculation;
    // public bool UseAutoCommandTarget;
    
    class Baker : Baker<DeterministicEntityAuthoring>
    {
        public override void Bake(DeterministicEntityAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<DeterministicSimulation>(entity);
            //if(authoring.UseInDeterministicFastHashCalculation) AddComponent<UseInDeterministicFastHashCalculation>(entity);
        }
    }
}