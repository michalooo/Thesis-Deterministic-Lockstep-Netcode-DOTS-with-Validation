using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;

namespace DeterministicLockstep
{
    /// <summary>
    /// Different possible server states used to control server behaviour.
    /// </summary>
    public enum DeterministicServerWorkingMode
    {
        ListenForConnections,
        RunDeterministicSimulation,
        Disconnect,
        None
    }
    
    /// <summary>
    /// Different possible client states used to control client behaviour.
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
    /// Component used to store the player input data to use for current simulation step.
    /// </summary>
    public struct PlayerInputDataToUse : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// ID of the player that the input data belongs to
        /// </summary>
        public int clientNetworkId;
        
        /// <summary>
        /// Inputs to apply for the current simulation step for the player with the given ID
        /// </summary>
        public PongInputs playerInputToApply;
        
        /// <summary>
        /// Indication if player was disconnected
        /// </summary>
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
    /// Component used to store the networkID of the connection and reference to the entity that is the target of the commands.
    /// </summary>
    public struct GhostOwner : IComponentData
    {
        /// <summary>
        /// Network ID of the connection that owns the Entity on the scene.
        /// </summary>
        public int connectionNetworkId;
        
        /// <summary>
        /// Reference to the entity that is the target of the commands.
        /// </summary>
        public Entity connectionCommandsTargetEntity;
    }

    /// <summary> 
    /// An enableable tag component used to track if an entity is owned by the local client or not.
    /// This component is usually added to different entities so it may cause desync if used in determinims validation.
    /// </summary>
    public struct GhostOwnerIsLocal : IComponentData, IEnableableComponent
    {
    } 

    /// <summary>
    /// Tag component used to tag connections for which a player prefab was spawned
    /// </summary>
    public struct PlayerSpawned : IComponentData
    {
    }
    
    /// <summary>
    /// Component used to store all the time related variables
    /// </summary>
    public struct DeterministicSimulationTime : IComponentData
    {
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
        /// variable that takes count of which tick is being processed on the client
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
        public NativeQueue<RpcBroadcastTickDataToClients> storedIncomingTicksFromServer;
    }

    /// <summary>
    /// Buffer element of component type used to mark components for validation
    /// </summary>
    public struct DeterministicComponent : IBufferElementData
    {
        public ComponentType Type;
    }
    
    /// <summary>
    /// Component used to mark current server working mode.
    /// </summary>
    public struct DeterministicServerComponent : IComponentData
    {
        public DeterministicServerWorkingMode deterministicServerWorkingMode;
    }
    
    /// <summary>
    /// Component used to mark current client working mode.
    /// </summary>
    public struct DeterministicClientComponent : IComponentData
    {
        public int clientNetworkId;
        public DeterministicClientWorkingMode deterministicClientWorkingMode;
    }
    
    /// <summary>
    /// To ensure deterministic sorting of entities when logging, this component should be added to entities on creation.
    /// It represents a unique, deterministic identifier.
    /// This identifier is a simple incrementing integer that is assigned when the entity is created.
    /// This way, the order of entity creation will determine the order of entities in the sorted log list for determinism validation, which should be deterministic as long as entities are created in a deterministic manner.
    /// </summary>
    public struct DeterministicEntityID : IComponentData, IComparable<DeterministicEntityID>
    {
        public int ID;

        public int CompareTo(DeterministicEntityID other)
        {
            return ID.CompareTo(other.ID);
        }
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