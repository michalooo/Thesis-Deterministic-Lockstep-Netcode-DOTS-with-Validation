using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that sends gathered player's input to the server.
    /// Inputs need to be manually gathered and set by the user in the input struct.
    /// This system should run as the last in DeterministicSimulationSystemGroup.
    /// In single-player mode, this system only clears hashes without network operations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Default)]
    public partial struct PlayerInputSendSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicSimulationTime>();
            state.RequireForUpdate<DeterministicSettings>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var deterministicSettings = SystemAPI.GetSingleton<DeterministicSettings>();
            var deterministicTime = SystemAPI.GetSingleton<DeterministicSimulationTime>();
            
            // In single-player or system-level validation mode, skip network operations
            if (deterministicSettings.validationMode == DeterminismValidationMode.SinglePlayer ||
                deterministicSettings.validationMode == DeterminismValidationMode.SystemLevel)
            {
                // Clear hashes for next tick (hashes are collected by SinglePlayerValidationManager)
                deterministicTime.hashesForTheCurrentTick.Clear();
                SystemAPI.SetSingleton(deterministicTime);
                return;
            }
            
            // Multiplayer mode - send inputs to server
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