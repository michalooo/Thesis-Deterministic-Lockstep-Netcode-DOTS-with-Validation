using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that sends gathered player's input to the server. Inputs need to be gathered by the user.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct PlayerInputSendSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deterministicTime = SystemAPI.GetSingleton<DeterministicTime>();
            
            
            foreach (var (connectionReference, owner) in SystemAPI
                         .Query<RefRO<NetworkConnectionReference>, RefRO<GhostOwner>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                // Debug.Log("Sending player input to server");
                if (!SystemAPI.TryGetSingleton<PongInputs>(out var capsulesInputs))
                {
                    Debug.LogError("Inputs are not singleton");
                    return;
                }
                
                var rpc = new RpcBroadcastPlayerTickDataToServer
                {
                    PongGameInputs = capsulesInputs,
                    PlayerNetworkID = owner.ValueRO.connectionNetworkId,
                    FutureTick = deterministicTime.currentClientTickToSend,
                    HashesForFutureTick = deterministicTime.hashesForTheCurrentTick
                };

                rpc.Serialize(connectionReference.ValueRO.driverReference, connectionReference.ValueRO.connectionReference,
                    connectionReference.ValueRO.reliableSimulationPipelineReference);
                
                deterministicTime.hashesForTheCurrentTick.Dispose();
                deterministicTime.hashesForTheCurrentTick = new NativeList<ulong>(Allocator.Persistent);
                SystemAPI.SetSingleton(deterministicTime);
            }
        }
    }
}