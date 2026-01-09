using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;

namespace DeterministicLockstep
{
    /// <summary>
    /// Validation mode for determinism checking.
    /// </summary>
    public enum DeterminismValidationMode
    {
        /// <summary>
        /// Single-player mode - validate by running simulation multiple times and comparing hashes.
        /// </summary>
        SinglePlayer,
        
        /// <summary>
        /// Multiplayer mode - validate by comparing hashes between clients via server.
        /// </summary>
        Multiplayer,
        
        /// <summary>
        /// System-level mode - validate individual ECS systems in isolation.
        /// </summary>
        SystemLevel
    }
    
    /// <summary>
    /// Different possible server states used to control server behaviour.
    /// </summary>
    public enum DeterministicServerWorkingMode
    {
        ListenForConnections, // Server is waiting for listening for connections without running the simulation
        RunDeterministicSimulation, // Server starts running the simulation and validating client inputs and hashes
        Disconnect, // Server is disconnecting all clients
        None // Default state, server is not doing anything
    }
    
    /// <summary>
    /// Different possible client states used to control client behaviour.
    /// </summary>
    public enum DeterministicClientWorkingMode
    {
        Connect, // Client is connecting to the server
        Disconnect, // Client is disconnecting from the server
        RunDeterministicSimulation, // Client is running the simulation and sending inputs and hashes to the server
        ClientReady, // Client is ready to start the simulation (all the scenes and elements are loaded)
        LoadingGame, // Client is loading the game
        GameFinished, // Client has finished the game
        Desync, // Desync message was received from the server. The game stops
        RunSinglePlayerValidation, // Client is running single-player validation simulation
        None // Default state, client is not doing anything
    }
    
    
    /// <summary>
    /// Component used to store the player input data to use for current simulation step.
    /// It should be assumed that it contains the input data to use for current frame and its automatically updated by the package.
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
        public ComponentType type;
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
    /// Component used to store validation settings for single-player and system-level validation.
    /// </summary>
    public struct DeterminismValidationSettings : IComponentData
    {
        /// <summary>
        /// The validation mode to use (SinglePlayer, Multiplayer, SystemLevel).
        /// </summary>
        public DeterminismValidationMode validationMode;
        
        /// <summary>
        /// Number of times to run the simulation for comparison in single-player mode.
        /// </summary>
        public int numberOfRuns;
        
        /// <summary>
        /// Current run index when performing multiple run validation.
        /// </summary>
        public int currentRunIndex;
        
        /// <summary>
        /// Whether validation is currently in progress.
        /// </summary>
        public bool isValidationInProgress;
        
        /// <summary>
        /// Whether validation has completed.
        /// </summary>
        public bool isValidationComplete;
        
        /// <summary>
        /// Whether nondeterminism was detected during validation.
        /// </summary>
        public bool nondeterminismDetected;
        
        /// <summary>
        /// The tick at which nondeterminism was first detected (0 if none detected).
        /// </summary>
        public int firstNondeterministicTick;
        
        /// <summary>
        /// Total number of ticks to simulate for validation.
        /// </summary>
        public int ticksToSimulate;
    }
    
    /// <summary>
    /// To ensure deterministic sorting of entities when logging, this component should be added to entities on creation.
    /// It represents a unique, deterministic identifier.
    /// This identifier is a simple incrementing integer that is assigned when the entity is created.
    /// This way, the order of entity creation will determine the order of entities in the sorted log list for determinism validation, which should be deterministic as long as entities are created in a deterministic manner.
    /// </summary>
    public struct DeterministicEntityID : IComponentData, IComparable<DeterministicEntityID>
    {
        public int id;

        public int CompareTo(DeterministicEntityID otherEntityID)
        {
            return id.CompareTo(otherEntityID.id);
        }
    }
   
    /// <summary>
    /// Predefined struct for managing player inputs in the sample Pong game.
    /// This is an example implementation of IPlayerInputs.
    /// For your own game, create a similar struct that implements IPlayerInputs.
    /// </summary>
    [Serializable]
    public struct PongInputs : IComponentData, IPlayerInputs
    {
        public int verticalInput;

        public void SerializeInputs(ref DataStreamWriter writer)
        {
            writer.WriteInt(verticalInput);
        }

        public void DeserializeInputs(ref DataStreamReader reader)
        {
            verticalInput = reader.ReadInt();
        }
    }
    
    /// <summary>
    /// Empty input struct for single-player validation where no actual inputs are needed.
    /// </summary>
    [Serializable]
    public struct EmptyInputs : IComponentData, IPlayerInputs
    {
        public void SerializeInputs(ref DataStreamWriter writer)
        {
            // No inputs to serialize
        }

        public void DeserializeInputs(ref DataStreamReader reader)
        {
            // No inputs to deserialize
        }
    }
}