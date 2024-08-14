using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using DeterministicLockstep;
using Unity.Mathematics;

namespace PongGame
{
    /// <summary>
    /// System used to spawn the player entity/prefab for the connections that are not yet "spawned"
    /// This status is marked by the absence or presence of the PlayerSpawned component on given connection entity
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
            var playerPrefab = SystemAPI.GetSingleton<PongPlayerSpawner>().player;
            var queryOfConnectionsWithoutSpawnedPrefabs = SystemAPI.QueryBuilder().WithNone<PlayerSpawned>().WithAll<GhostOwner>().Build();

            if (queryOfConnectionsWithoutSpawnedPrefabs.IsEmpty) return;
            
            var ghostOwners = queryOfConnectionsWithoutSpawnedPrefabs.ToComponentDataArray<GhostOwner>(Allocator.Temp);
            var connectionEntities = queryOfConnectionsWithoutSpawnedPrefabs.ToEntityArray(Allocator.Temp);
            
            for(var i=0; i<=ghostOwners.Length-1; i++)
            {
                EntityManager.AddComponent<PlayerSpawned>(connectionEntities[i]);
                var spawnedPlayerPrefab = EntityManager.Instantiate(playerPrefab);
                
                var targetSpawnPositionX = ghostOwners[i].connectionNetworkId % 2 == 0 ? -8f : 8f; // For Pong game we only have 2 players and consider left or right side of the screen
                    
                EntityManager.AddComponentData(spawnedPlayerPrefab, new DeterministicEntityID { id = DeterministicLogger.Instance.GetDeterministicEntityID(World.Name) });
                EntityManager.SetComponentData(spawnedPlayerPrefab, new LocalTransform
                {
                    Position = new float3(targetSpawnPositionX, 0f, 13f), //TODO: Hardcoded z-position
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