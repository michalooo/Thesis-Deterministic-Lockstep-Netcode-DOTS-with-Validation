using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PongGame
{
    [BurstCompile]
    public struct Velocity : IComponentData
    {
        public float3 value;
    }

    /// <summary>
    /// Authoring function for the Spawner
    /// </summary>
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