using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using DeterministicLockstep;
using Unity.Burst;
using Unity.Mathematics;

namespace PongGame
{
    /// <summary>
    /// System used to spawn the player prefab for the connections that are not spawned yet
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PongPlayerSpawnerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PongPlayerSpawner>();
            RequireForUpdate<PongInputs>();
        }
        
        protected override void OnUpdate()
        {
            var prefab = SystemAPI.GetSingleton<PongPlayerSpawner>().Player;
            var query = SystemAPI.QueryBuilder().WithNone<PlayerSpawned>().WithAll<GhostOwner>().Build();

            if (query.IsEmpty) return;
            
            var ghostOwners = query.ToComponentDataArray<GhostOwner>(Allocator.Temp);
            var connectionEntities = query.ToEntityArray(Allocator.Temp);
            
            

            for(var i=0; i<=ghostOwners.Length-1; i++)
            {
                EntityManager.AddComponent<PlayerSpawned>(connectionEntities[i]);
                var player = EntityManager.Instantiate(prefab);
                        
                if (ghostOwners[i].connectionNetworkId % 2 == 0)
                {
                    Camera cam = Camera.main;
                    float targetXPosition = 0.05f * Screen.width;
                    Vector3 worldPosition = cam.ScreenToWorldPoint(new Vector3(targetXPosition, 0, cam.nearClipPlane));
                    
                    EntityManager.SetComponentData(player, new LocalTransform
                    {
                        Position = new float3(worldPosition.x, worldPosition.y, 13f), 
                        Scale = 1f,
                        Rotation = quaternion.identity
                    });
                }
                else
                {
                    Camera cam = Camera.main;
                    float targetXPosition = 0.95f * Screen.width;
                    Vector3 worldPosition = cam.ScreenToWorldPoint(new Vector3(targetXPosition, 0, cam.nearClipPlane));
                    
                    EntityManager.SetComponentData(player, new LocalTransform
                    {
                        Position = new float3(worldPosition.x, worldPosition.y, 13f), 
                        Scale = 1f,
                        Rotation = quaternion.identity
                    });
                }
                
                ghostOwners[i] = new GhostOwner() { connectionNetworkId = ghostOwners[i].connectionNetworkId, connectionCommandsTargetEntity = player};
                EntityManager.AddComponentData(connectionEntities[i], ghostOwners[i]); // is it necessary for the package? This is user implementation
            }
        }
    }
}