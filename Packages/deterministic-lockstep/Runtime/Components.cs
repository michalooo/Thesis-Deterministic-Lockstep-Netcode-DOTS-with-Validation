using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Enum storing different server states
    /// </summary>
    public enum DeterministicServerWorkingMode
    {
        ListenForConnections,
        RunDeterministicSimulation,
        Disconnect,
        None
    }
    
    /// <summary>
    /// Enum storing different client states
    /// </summary>
    public enum DeterministicClientWorkingMode
    {
        Connect,
        Disconnect,
        RunDeterministicSimulation,
        ClientReady,
        LoadingGame,
        GameFinished,
        Desync,
        None
    }
    
    
    /// <summary>
    /// Component used to store the player input data to use for user simulation
    /// </summary>
    public struct PlayerInputDataToUse : IComponentData, IEnableableComponent
    {
        public int playerNetworkId;
        public PongInputs playerInputToApply;
        
        public bool isPlayerDisconnected;
    }

    /// <summary>
    /// Component used to store connection info for every connection
    /// </summary>
    public struct NetworkConnectionReference : IComponentData
    {
        public NetworkDriver driverReference;
        public NetworkPipeline reliablePipelineReference;
        public NetworkConnection connectionReference;
    }

    /// <summary>
    /// Component used to store the networkID of the connection
    /// </summary>
    public struct GhostOwner : IComponentData
    {
        public int connectionNetworkId;
        public Entity connectionCommandsTargetEntity;
    }

    /// <summary> 
    /// An enableable tag component used to track if a ghost with an owner is owned by the local host or not.
    /// </summary>
    public struct GhostOwnerIsLocal : IComponentData, IEnableableComponent
    {
    } // added to different entities so it may cause desync if comparing amount of components

    /// <summary>
    /// Component used to tag connections for which a player prefab was spawned
    /// </summary>
    public struct PlayerSpawned : IComponentData
    {
    }
    
    /// <summary>
    /// Component used to store all the time related variables
    /// </summary>
    public struct DeterministicTime : IComponentData
    {
        /// <summary>
        /// Synchronized clock with the server
        /// </summary>
        public TimeSpan serverTimestampUTC;
        public TimeSpan localTimestampAtTheMomentOfSynchronizationUTC;
        public double timeToPostponeStartofSimulationInMiliseconds;
        public double playerAveragePing;
        
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
        public double timeLeftToSendNextTick;

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

    public struct DeterministicComponent : IBufferElementData
    {
        public ComponentType Type;
    }
    
    
    /// <summary>
    /// Component used to mark server state
    /// </summary>
    public struct DeterministicServerComponent : IComponentData
    {
        public DeterministicServerWorkingMode deterministicServerWorkingMode;
    }
    
    /// <summary>
    /// Component used to mark client state
    /// </summary>
    public struct DeterministicClientComponent : IComponentData
    {
        public uint randomSeed;
        public int clientNetworkId;
        public DeterministicClientWorkingMode deterministicClientWorkingMode;
    }
   
    /// <summary>
    /// Predefined struct for managing player inputs in the sample Pong game
    /// </summary>
    [Serializable]
    public struct PongInputs: IComponentData
    {
        public int verticalInput;

        public void SerializeInputs(ref DataStreamWriter writer)
        {
            writer.WriteInt(verticalInput);
        }

        public void
            DeserializeInputs(
                ref DataStreamReader reader)
        {
            verticalInput = reader.ReadInt();
        }
    }
}