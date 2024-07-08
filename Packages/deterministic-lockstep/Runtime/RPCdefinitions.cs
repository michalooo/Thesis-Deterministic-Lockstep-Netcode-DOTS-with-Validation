using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Networking.Transport;

namespace DeterministicLockstep
{
    /// <summary>
    /// Enumeration of all possible Remote Procedure Calls (RPCs). This is used to identify the RPCs when deserializing them.
    /// </summary>
    public enum RpcID : byte
    {
        StartDeterministicGameSimulation, // Server -> Client
        BroadcastTickDataToClients, // Server -> Client
        PlayerDesynchronized, // Server -> Client
        LoadGame, // Server -> Client
        
        PlayerReady, // Client -> Server
        BroadcastPlayerTickDataToServer, // Client -> Server
        GameEnded, // Client -> Server
    }
    
    /// <summary>
    /// Struct sent by the client when the game is considered finished. It contains the final game hash and the network ID of the player sending it.
    /// </summary>
    [BurstCompile]
    public struct RpcEndGameHash
    {
        /// <summary>
        /// Hash for the game state for the last frame which allows for comparison of the game state between clients and ensuring that every client simulation resulted in the same final state.
        /// </summary>
        public ulong FinalGameHash { get; set; }
        
        /// <summary>
        /// Network id of the client sending RpcGameEnded.
        /// </summary>
        public int ClientNetworkID { get; set; }
        public RpcID GetID => RpcID.GameEnded;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline reliableSimulationPipeline)
        {
            mDriver.BeginSend(reliableSimulationPipeline, connection, out var writer);

            writer.WriteByte((byte)GetID);
            writer.WriteInt(ClientNetworkID);
            writer.WriteULong(FinalGameHash);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // Reading rpc ID to remove it from the stream
            ClientNetworkID = reader.ReadInt();
            FinalGameHash = reader.ReadULong();
        }
    }

    /// <summary>
    /// Struct sent by the server when nondeterminism is detected. It contains information about the tick at which nondeterminism was detected so the client may generate log file from said tick. If received, it will stop game execution and will trigger generation of nondeterminism debug tooling files.
    /// </summary>
    [BurstCompile]
    public struct RpcPlayerDesynchronization
    {
        /// <summary>
        /// Tick at which nondeterminism was detected.
        /// </summary>
        public ulong NonDeterministicTick { get; set; }
        public RpcID GetID => RpcID.PlayerDesynchronized;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline reliableSimulationPipeline)
        {
            mDriver.BeginSend(reliableSimulationPipeline, connection, out var writer);

            writer.WriteByte((byte)GetID);
            writer.WriteULong(NonDeterministicTick);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // Reading rpc ID to remove it from the stream
            NonDeterministicTick = reader.ReadULong();
        }
    }
    
    /// <summary>
    /// Struct sent by the server to clients. It signals to the client to load the game. 
    /// </summary>
    [BurstCompile]
    public struct RpcLoadGame
    {
        public RpcID GetID => RpcID.LoadGame;
        
        /// <summary>
        /// ID of the receiving client
        /// </summary>
        public int ClientNetworkID { get; set; }
        
        /// <summary>
        /// All client IDs that are connected to the server. It's used to assign them to prefabs and connections.
        /// </summary>
        public NativeList<int> NetworkIDsOfAllClients { get; set; }

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline reliableSimulationPipeline)
        {
            mDriver.BeginSend(reliableSimulationPipeline, connection, out var writer);

            writer.WriteByte((byte)GetID);
            writer.WriteInt(ClientNetworkID);
            writer.WriteInt(NetworkIDsOfAllClients.Length);
            foreach (var id in NetworkIDsOfAllClients)
            {
                writer.WriteInt(id);
            }

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // Reading rpc ID to remove it from the stream
            ClientNetworkID = reader.ReadInt();
            var count = reader.ReadInt();
            NetworkIDsOfAllClients = new NativeList<int>(count, Allocator.Temp);
            for (var i = 0; i < count; i++)
            {
                NetworkIDsOfAllClients.Add(reader.ReadInt());
            }
        }
    }
    
    /// <summary>
    /// Struct sent by the client to server, to indicate readiness to start the game. It contains the network ID of the player and the starting hash for the game.
    /// </summary>
    [BurstCompile]
    public struct RpcPlayerReady
    {
        public RpcID GetID => RpcID.PlayerReady;
        public int ClientNetworkID { get; set; }
        
        /// <summary>
        /// Game hash which will be compared to see if all clients have the same starting state.
        /// </summary>
        public ulong StartingHash { get; set; }

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline reliableSimulationPipeline)
        {
            mDriver.BeginSend(reliableSimulationPipeline, connection, out var writer);

            writer.WriteByte((byte)GetID);
            writer.WriteInt(ClientNetworkID);
            writer.WriteULong(StartingHash);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // Reading rpc ID to remove it from the stream
            ClientNetworkID = reader.ReadInt();
            StartingHash = reader.ReadULong();
        }
    }


    /// <summary>
    /// Struct sent by the server at the start of the game. It contains information about all player network IDs for connection entity creation, the expected tick rate, forced input latency in ticks, seed for player random actions, determinism hash calculation option, and the network ID of the current connection.
    /// </summary>
    [BurstCompile]
    public struct RpcStartDeterministicSimulation
    {
        /// <summary>
        /// All of connected players IDs so we can assign them to prefabs and connections
        /// </summary>
        public NativeList<int>  NetworkIDsOfAllClients { get; set; }

        /// <summary>
        /// Game tick rate set by the server/developer
        /// </summary>
        public int GameIntendedTickRate { get; set; }

        /// <summary>
        /// Forced input latency value, given in ticks.
        /// </summary>
        public int TicksOfForcedInputLatency { get; set;} 
        
        /// <summary>
        /// Seed for random number generation
        /// </summary>
        public uint SeedForPlayerRandomActions { get; set; }
        
        /// <summary>
        /// Determinism hash calculation option
        /// </summary>
        public int DeterminismHashCalculationOption { get; set; }
        
        public int ClientAssignedNetworkID { get; set; }

        public RpcID GetID => RpcID.StartDeterministicGameSimulation;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline reliableSimulationPipeline)
        {
            DataStreamWriter writer;
            mDriver.BeginSend(reliableSimulationPipeline, connection, out writer);

            writer.WriteByte((byte)GetID);
            
            writer.WriteInt(NetworkIDsOfAllClients.Length);
            foreach (var id in NetworkIDsOfAllClients)
            {
                writer.WriteInt(id);
            }

            writer.WriteInt(GameIntendedTickRate);
            writer.WriteInt(TicksOfForcedInputLatency);
            writer.WriteUInt(SeedForPlayerRandomActions);
            writer.WriteInt(DeterminismHashCalculationOption);
            writer.WriteInt(ClientAssignedNetworkID);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // Reading rpc ID to remove it from the stream
            
            var count = reader.ReadInt();
            NetworkIDsOfAllClients = new NativeList<int>(count, Allocator.Temp);

            for (var i = 0; i < count; i++)
            {
                NetworkIDsOfAllClients.Add(reader.ReadInt());
            }

            GameIntendedTickRate = reader.ReadInt();
            TicksOfForcedInputLatency = reader.ReadInt();
            SeedForPlayerRandomActions = reader.ReadUInt();
            DeterminismHashCalculationOption = reader.ReadInt();
            ClientAssignedNetworkID = reader.ReadInt();
        }
    }

    /// <summary>
    /// Struct sent by the clients to the server at the end of each tick with data to apply at the given future tick.
    /// </summary>
    [BurstCompile]
    [Serializable]
    public struct RpcBroadcastPlayerTickDataToServer
    {
        /// <summary>
        /// Input struct that is being send with filled out inputs
        /// </summary>
        public PongInputs PlayerGameInput;
        public int ClientNetworkID; 
        
        /// <summary>
        /// Future tick at which the inputs should be applied
        /// </summary>
        public int TickToApplyInputsOn;
        
        /// <summary>
        /// Depending on hash calculation option this can be empty, size(1) or size(systems). It's used to compare current state and detect nondeterminism
        /// </summary>
        public NativeList<ulong> HashesForTheTick { get; set; }
        public RpcID GetID => RpcID.BroadcastPlayerTickDataToServer;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline reliableSimulationPipeline)
        {
            if(!mDriver.IsCreated || !connection.IsCreated) return;
            
            mDriver.BeginSend(reliableSimulationPipeline, connection, out var writer);
            
            if(!writer.IsCreated) return;

            writer.WriteByte((byte)GetID);
            PlayerGameInput.SerializeInputs(ref writer);
            writer.WriteInt(ClientNetworkID);
            writer.WriteInt(TickToApplyInputsOn);
            
            var hashesSize = HashesForTheTick.Length;
            writer.WriteInt(hashesSize);
            foreach (var hash in HashesForTheTick)
            {
                writer.WriteULong(hash);
            }
            
            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes. Capacity: " + writer.Capacity + " Length: " + writer.Length + " Hashes: " + hashesSize);
            }
            
            mDriver.EndSend(writer);
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // Reading rpc ID to remove it from the stream
            PlayerGameInput.DeserializeInputs(ref reader);
            ClientNetworkID = reader.ReadInt();
            TickToApplyInputsOn = reader.ReadInt();
            
            var hashesSize = reader.ReadInt();
            HashesForTheTick = new NativeList<ulong>(hashesSize, Allocator.Persistent);
            for (var i = 0; i < hashesSize; i++)
            {
                HashesForTheTick.Add(reader.ReadULong());
            }
        }
    }
    
    /// <summary>
    /// Struct sent by the server to all clients after receiving inputs from all of them. It contained data of all clients for the given tick for the simulation to process.
    /// </summary>
    [BurstCompile]
    public struct RpcBroadcastTickDataToClients
    {
        /// <summary>
        /// All of connected players IDs corresponding to their inputs.
        /// </summary>
        public NativeList<int> NetworkIDsOfAllClients { get; set; }
        
        /// <summary>
        /// Inputs corresponding to client network IDs to apply.
        /// </summary>
        public NativeList<PongInputs> GameInputsFromAllClients { get; set; } 

        /// <summary>
        /// Tick on which the inputs should be applied.
        /// </summary>
        public int SimulationTick { get; set; } 

        public RpcID GetID => RpcID.BroadcastTickDataToClients;
        
        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline reliableSimulationPipeline)
        {
            if(!mDriver.IsCreated || !connection.IsCreated) return;
            
            mDriver.BeginSend(reliableSimulationPipeline, connection, out var writer);
        
            if(!writer.IsCreated) return;
            
            writer.WriteByte((byte)GetID);
            writer.WriteInt(NetworkIDsOfAllClients.Length);
            foreach (var id in NetworkIDsOfAllClients)
            {
                writer.WriteInt(id);
            }

            writer.WriteInt(GameInputsFromAllClients.Length);
            foreach (var inputStruct in GameInputsFromAllClients)
            {
                inputStruct.SerializeInputs(ref writer);
            }

            writer.WriteInt(SimulationTick);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }
            
            mDriver.EndSend(writer);
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // Reading rpc ID to remove it from the stream
            
            int idCount = reader.ReadInt();
            NetworkIDsOfAllClients = new NativeList<int>(idCount, Allocator.Persistent);
            for (var i = 0; i < idCount; i++)
            {
                NetworkIDsOfAllClients.Add(reader.ReadInt());
            }
            
            var inputsCount = reader.ReadInt();
            GameInputsFromAllClients = new NativeList<PongInputs>(inputsCount, Allocator.Persistent);
            for (var i = 0; i < inputsCount; i++)
            {
                var inputs = new PongInputs();
                inputs.DeserializeInputs(ref reader);
                GameInputsFromAllClients.Add(inputs);
            }
            
            SimulationTick = reader.ReadInt();
        }
    }
}