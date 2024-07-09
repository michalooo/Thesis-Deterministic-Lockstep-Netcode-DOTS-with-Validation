using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using DeterministicLockstep;
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
            var playerPrefab = SystemAPI.GetSingleton<PongPlayerSpawner>().Player;
            var queryOfConnectionsWithoutSpawnedPrefabs = SystemAPI.QueryBuilder().WithNone<PlayerSpawned>().WithAll<GhostOwner>().Build();

            if (queryOfConnectionsWithoutSpawnedPrefabs.IsEmpty) return;
            
            var ghostOwners = queryOfConnectionsWithoutSpawnedPrefabs.ToComponentDataArray<GhostOwner>(Allocator.Temp);
            var connectionEntities = queryOfConnectionsWithoutSpawnedPrefabs.ToEntityArray(Allocator.Temp);
            
            for(var i=0; i<=ghostOwners.Length-1; i++)
            {
                EntityManager.AddComponent<PlayerSpawned>(connectionEntities[i]);
                var spawnedPlayerPrefab = EntityManager.Instantiate(playerPrefab);
                        
                // Camera camera = Camera.main;
                float targetSpawnPositionX = ghostOwners[i].connectionNetworkId % 2 == 0 ? -8f : 8f;
                // Vector3 worldPosition = camera.ScreenToWorldPoint(new Vector3(targetSpawnPositionX, 0, camera.nearClipPlane));
                
                // Camera camera = Camera.main;
                // float targetSpawnPositionX = ghostOwners[i].connectionNetworkId % 2 == 0 ? 0.05f : 0.95f;
                // Vector3 viewportPosition = new Vector3(targetSpawnPositionX, 0.5f, camera.nearClipPlane);
                // Vector3 worldPosition = camera.ViewportToWorldPoint(viewportPosition);
                    
                EntityManager.AddComponentData(spawnedPlayerPrefab, new DeterministicEntityID { ID = DeterministicLogger.Instance.GetDeterministicEntityID(World.Name) });
                EntityManager.SetComponentData(spawnedPlayerPrefab, new LocalTransform
                {
                    Position = new float3(targetSpawnPositionX, 0f, 13f), 
                    Scale = 1f,
                    Rotation = quaternion.identity
                });
                EntityManager.SetName(spawnedPlayerPrefab, "Player");
                
                ghostOwners[i] = new GhostOwner() { connectionNetworkId = ghostOwners[i].connectionNetworkId, connectionCommandsTargetEntity = spawnedPlayerPrefab};
                EntityManager.AddComponentData(connectionEntities[i], ghostOwners[i]);
            }
        }
    }
}