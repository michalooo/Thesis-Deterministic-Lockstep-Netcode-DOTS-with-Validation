using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;

namespace DeterministicLockstep
{
    /// <summary>
    /// Component used to store the arriving player data for the server
    /// </summary>
    public struct StoredTicksAhead : IComponentData
    {
        public NativeQueue<RpcPlayersDataUpdate> entries; // be sure that there is no memory leak

        public StoredTicksAhead(bool b) // not possible to have parameterless constructor
        {
            entries = new NativeQueue<RpcPlayersDataUpdate>(Allocator.Persistent);
        }
    }

    /// <summary>
    /// Enableable tag Component used to mark if user input should be send to the server
    /// </summary>
    public struct PlayerInputDataToSend : IComponentData, IEnableableComponent {}

    /// <summary>
    /// Component used to store the player input data to use for user simulation
    /// </summary>
    public struct PlayerInputDataToUse : IComponentData, IEnableableComponent
    {
        public int playerNetworkId;
        public CapsulesInputs inputToUse;
        
        public bool playerDisconnected;
    }

    /// <summary>
    /// Component used to store information about the ticks and time for the game
    /// </summary>
    struct TickRateInfo : IComponentData
    {
        public int tickRate;
        public int tickAheadValue;

        public float delayTime;
        public int currentSimulationTick; // Received simulation tick from the server
        public int currentClientTickToSend; // We are sending input for the tick in the future
        public ulong hashForTheTick;
    }

    /// <summary>
    /// Component used to store connection info for every conection
    /// </summary>
    public struct NetworkConnectionReference : IComponentData
    {
        public NetworkDriver driver;
        public NetworkPipeline reliableSimulatorPipeline;
        public NetworkConnection connection;
    }

    /// <summary>
    /// Component used to store the networkID of the connection
    /// </summary>
    public struct GhostOwner : IComponentData
    {
        public int networkId;
    }

    /// <summary>
    /// Component used to store the target spawned entity for the connection of which the arriving input will be used
    /// </summary>
    public struct CommandTarget : IComponentData
    {
        public Entity targetEntity;
    }

    /// <summary> 
    /// An enableable tag component used to track if a ghost with an owner is owned by the local host or not.
    /// </summary>
    public struct GhostOwnerIsLocal : IComponentData, IEnableableComponent
    {
    } // added to different entites so it may cause desync if comparing amount of components

    /// <summary>
    /// Component used to tag connections for which a player prefab was spawned
    /// </summary>
    public struct PlayerSpawned : IComponentData
    {
    }
}