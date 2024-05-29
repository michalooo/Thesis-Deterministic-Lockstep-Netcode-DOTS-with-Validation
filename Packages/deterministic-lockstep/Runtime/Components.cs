using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;

namespace DeterministicLockstep
{
    /// <summary>
    /// Enum storing different server states
    /// </summary>
    public enum DeterministicServerWorkingMode
    {
        ListenForConnections,
        RunDeterministicSimulation,
        None
    }
    
    /// <summary>
    /// Enum storing different client states
    /// </summary>
    public enum DeterministicClientWorkingMode
    {
        Connect,
        Disconnect,
        SendData,
        PrepareGame,
        None
    }
    
    
    /// <summary>
    /// Component used to store the player input data to use for user simulation
    /// </summary>
    [BurstCompile]
    public struct PlayerInputDataToUse : IComponentData, IEnableableComponent
    {
        public int playerNetworkId;
        public PongInputs playerInputToApply;
        
        public bool isPlayerDisconnected;
    }

    /// <summary>
    /// Component used to store connection info for every connection
    /// </summary>
    [BurstCompile]
    public struct NetworkConnectionReference : IComponentData
    {
        public NetworkDriver driverReference;
        public NetworkPipeline reliableSimulationPipelineReference;
        public NetworkConnection connectionReference;
    }

    /// <summary>
    /// Component used to store the networkID of the connection
    /// </summary>
    [BurstCompile]
    public struct GhostOwner : IComponentData
    {
        public int connectionNetworkId;
    }

    /// <summary>
    /// Component used to store the target spawned entity for the connection of which the arriving input will be used
    /// </summary>
    [BurstCompile]
    public struct CommandTarget : IComponentData
    {
        public Entity connectionCommandsTargetEntity;
    }

    /// <summary> 
    /// An enableable tag component used to track if a ghost with an owner is owned by the local host or not.
    /// </summary>
    [BurstCompile]
    public struct GhostOwnerIsLocal : IComponentData, IEnableableComponent
    {
    } // added to different entities so it may cause desync if comparing amount of components

    /// <summary>
    /// Component used to tag connections for which a player prefab was spawned
    /// </summary>
    [BurstCompile]
    public struct PlayerSpawned : IComponentData
    {
    }
    
    /// <summary>
    /// Component used to store all the time related variables
    /// </summary>
    [BurstCompile]
    public struct DeterministicTime : IComponentData
    {
        /// <summary>
        /// Synchronized clock with the server
        /// </summary>
        public DateTime synchronizedDateTimeWithServer;
        public DateTime localTimeAtTheMomentOfSynchronization;
        public double timeToPostponeStartofSimulation;
        
        /// <summary>
        /// Variable storing the elapsed time which is used to control system groups
        /// </summary>
        public double deterministicLockstepElapsedTime; //seconds same as ElapsedTime

        /// <summary>
        /// Variable storing the time that has passed since the last frame
        /// </summary>
        public float realTime;

        /// <summary>
        /// Variable storing information of how many ticks we already processed for the current frame
        /// </summary>
        public int numTimesTickedThisFrame;

        /// <summary>
        /// Set constant value of what's the tick rate of the game
        /// </summary>
        public int GameTickRate;

        /// <summary>
        /// Value describing how many ticks ahead is client sending his inputs. This value is taking care of forced input latency (in ticks)
        /// </summary>
        public int forcedInputLatencyDelay;

        /// <summary>
        /// Variable that is used to calculate time before processing next tick
        /// </summary>
        public float timeLeftToSendNextTick;

        /// <summary>
        /// variable that takes count of which tick is being visually processed on the client
        /// </summary>
        public int currentSimulationTick;

        /// <summary>
        /// Variable that takes count of the current tick that we are sending to the server (future tick).
        /// </summary>
        public int currentClientTickToSend;

        /// <summary>
        /// Calculated hash for the current tick
        /// </summary>
        public NativeList<ulong> hashesForTheCurrentTick;

        /// <summary>
        /// Queue of RPCs that are received from the server with all clients inputs for a given tick.
        /// </summary>
        public NativeQueue<RpcBroadcastTickDataToClients> storedIncomingTicksFromServer; // be sure that there is no memory leak
    }
    
    /// <summary>
    /// Component used to mark server state
    /// </summary>
    [BurstCompile]
    public struct DeterministicServerComponent : IComponentData
    {
        public DeterministicServerWorkingMode deterministicServerWorkingMode;
    }
    
    /// <summary>
    /// Component used to mark client state
    /// </summary>
    [BurstCompile]
    public struct DeterministicClientComponent : IComponentData
    {
        public uint randomSeed;
        public DeterministicClientWorkingMode deterministicClientWorkingMode;
    }
    
    [BurstCompile]
    public struct PongInputs: IComponentData
    {
        public int verticalInput;

        public void SerializeInputs(ref DataStreamWriter writer)
        {
            writer.WriteInt(verticalInput);
        }

        public void
            DeserializeInputs(
                ref DataStreamReader reader) //question how user can know if the order will be correct? --> Same order as serialization
        {
            verticalInput = reader.ReadInt();
        }
    }
}