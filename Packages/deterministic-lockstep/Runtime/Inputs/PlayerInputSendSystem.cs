using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that sends gathered player's input to the server.
    /// Inputs need to be manually gathered and set by the user in the input struct.
    /// This system should run as the last in DeterministicSimulationSystemGroup
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct PlayerInputSendSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicSimulationTime>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var deterministicTime = SystemAPI.GetSingleton<DeterministicSimulationTime>();
            
            foreach (var (connectionReference, owner) in SystemAPI
                         .Query<RefRO<NetworkConnectionReference>, RefRO<GhostOwner>>()
                         .WithAll<GhostOwnerIsLocal>())
            {
                if (!SystemAPI.TryGetSingleton<PongInputs>(out var capsulesInputs))
                {
                    Debug.LogError("Inputs are not singleton");
                    return;
                }
                
                var rpc = new RpcBroadcastPlayerTickDataToServer
                {
                    PlayerGameInput = capsulesInputs,
                    ClientNetworkID = owner.ValueRO.connectionNetworkId,
                    TickToApplyInputsOn = deterministicTime.currentClientTickToSend,
                    HashesForTheTick = deterministicTime.hashesForTheCurrentTick,
                };

                rpc.Serialize(connectionReference.ValueRO.driverReference, connectionReference.ValueRO.connectionReference,
                    connectionReference.ValueRO.reliablePipelineReference);
                deterministicTime.hashesForTheCurrentTick.Dispose();
                deterministicTime.hashesForTheCurrentTick = new NativeList<ulong>(Allocator.Persistent);
                SystemAPI.SetSingleton(deterministicTime);
            }
        }
    }
}