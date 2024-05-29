﻿using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PongGame
{
    
    /// <summary>
    /// Component used to store the player prefab entity
    /// </summary>
    [BurstCompile]
    public struct PongBallSpawner : IComponentData
    {
        public Entity Ball;
    }

    /// <summary>
    /// Authoring function for the Spawner
    /// </summary>
    public class PongBallSpawnerAuthoring : MonoBehaviour
    {
        public GameObject Ball;

        class Baker : Baker<PongBallSpawnerAuthoring>
        {
            public override void Bake(PongBallSpawnerAuthoring authoring)
            {
                var component = default(PongBallSpawner);
                component.Ball = GetEntity(authoring.Ball, TransformUsageFlags.Dynamic);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, component);
            }
        }
        
    }
}