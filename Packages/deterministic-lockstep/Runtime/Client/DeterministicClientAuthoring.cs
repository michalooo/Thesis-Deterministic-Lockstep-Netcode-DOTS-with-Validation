using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    public struct DeterministicClient : IComponentData
    {
    }

    public struct DeterministicClientConnect : IComponentData, IEnableableComponent { }
    public struct DeterministicClientDisconnect : IComponentData, IEnableableComponent { } // less components, singleton
    public struct DeterministicClientSendData : IComponentData, IEnableableComponent { }
    
    public class DeterministicClientAuthoring : MonoBehaviour
    {
        class Baker : Baker<DeterministicClientAuthoring>
        {
            public override void Bake(DeterministicClientAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<DeterministicClient>(entity);
                
                AddComponent<DeterministicClientConnect>(entity);
                AddComponent<DeterministicClientDisconnect>(entity);
                AddComponent<DeterministicClientSendData>(entity);
                
                SetComponentEnabled<DeterministicClientConnect>(entity, false);
                SetComponentEnabled<DeterministicClientDisconnect>(entity, false);
                SetComponentEnabled<DeterministicClientSendData>(entity, false);
            }
        }
    }
}