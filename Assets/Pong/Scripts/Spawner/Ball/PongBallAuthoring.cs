using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// A component used to store velocity value in float3 format.
    /// </summary>
    [BurstCompile]
    public struct Velocity : IComponentData
    {
        public float3 value;
    }
    
    public class PongBallAuthoring : MonoBehaviour
    {
        class Baker : Baker<PongBallAuthoring>
        {
            public override void Bake(PongBallAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                var velocity = default(Velocity);
                AddComponent(entity, velocity);
            }
        }
        
    }
}