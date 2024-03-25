using Unity.Entities;
using UnityEngine;

public struct DeterministicSimulation : IComponentData
{

}
public struct UseInDeterministicFastHashCalculation : IComponentData
{
}

public class DeterministicEntityAuthoring : MonoBehaviour
{
    public bool UseInDeterministicFastHashCalculation;
    // public bool UseAutoCommandTarget;
    
    class Baker : Baker<DeterministicEntityAuthoring>
    {
        public override void Bake(DeterministicEntityAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<DeterministicSimulation>(entity);
            if(authoring.UseInDeterministicFastHashCalculation) AddComponent<UseInDeterministicFastHashCalculation>(entity);
        }
    }
}