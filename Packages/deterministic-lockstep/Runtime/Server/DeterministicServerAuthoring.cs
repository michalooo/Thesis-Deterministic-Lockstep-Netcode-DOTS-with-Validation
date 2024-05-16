using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    public struct DeterministicServer : IComponentData
    {
    }

    public struct DeterministicServerListen : IComponentData, IEnableableComponent { }
    public struct DeterministicServerRunSimulation : IComponentData, IEnableableComponent { }
    
    public class DeterministicServerAuthoring : MonoBehaviour
    {
        class Baker : Baker<DeterministicServerAuthoring>
        {
            public override void Bake(DeterministicServerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<DeterministicServer>(entity);
                
                AddComponent<DeterministicServerListen>(entity);
                AddComponent<DeterministicServerRunSimulation>(entity);
                
                SetComponentEnabled<DeterministicServerListen>(entity, false);
                SetComponentEnabled<DeterministicServerRunSimulation>(entity, false);
            }
        }
    }
}