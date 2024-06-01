using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DeterministicLockstep
{
    /// <summary>
    /// Interface for RPCs, all RPCs must implement this interface to ensure that we can get id from it, serialize it and deserialize it
    /// </summary>
    public interface INetcodeRPC // Maybe would be nice to somehow enforce this RPC? On the other hand it's locally only
    {
        RpcID GetID { get; }

        void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline reliableSimulationPipeline);
        void Deserialize(ref DataStreamReader reader);
    }

    /// <summary>
    /// Enum with all possible RPCs, this is used to identify the RPCs when serializing and deserializing them (one byte is enough space to cover them)
    /// </summary>
    public enum RpcID : byte
    {
        StartDeterministicGameSimulation,
        BroadcastTickDataToClients,
        BroadcastPlayerTickDataToServer,
        PlayersDesynchronizedMessage,
        TestClientPing,
        LoadGame,
        PlayerReady,
        // PlayerConfiguration,
    }
    
    /// <summary>
    /// Struct that is being send by the server to the client in order to measure ping between them
    /// </summary>
    [BurstCompile]
    public struct RpcTestPing : INetcodeRPC
    {
        public RpcID GetID => RpcID.TestClientPing;
        public double TimeInMilisecondsWhenMessageWasSendFromServer { get; set; }
        public int PlayerNetworkID { get; set; }
        public double PingMeasuredForThisRPC { get; set; }

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline reliableSimulationPipeline)
        {
            DataStreamWriter writer;
            mDriver.BeginSend(reliableSimulationPipeline, connection, out writer);

            writer.WriteByte((byte)GetID);
            writer.WriteDouble(TimeInMilisecondsWhenMessageWasSendFromServer);
            writer.WriteInt(PlayerNetworkID);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
            // Debug.Log("RPC for testing ping send from server");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID
            TimeInMilisecondsWhenMessageWasSendFromServer = reader.ReadDouble();
            PlayerNetworkID = reader.ReadInt();

            // Debug.Log("RPC for testing ping received");
        }
    }

    /// <summary>
    /// Struct that is being send by the server when clients are desynchronized. Used to stop game execution
    /// </summary>
    [BurstCompile]
    public struct RpcPlayerDesynchronizationMessage : INetcodeRPC
    {
        public RpcID GetID => RpcID.PlayersDesynchronizedMessage;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline reliableSimulationPipeline)
        {
            DataStreamWriter writer;
            mDriver.BeginSend(reliableSimulationPipeline, connection, out writer);

            writer.WriteByte((byte)GetID);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
            // Debug.Log("RPC stating that players disconnected send from server");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID

            // Debug.Log("RPC stating that players disconnected received");
        }
    }
    
    /// <summary>
    /// Struct that is being send by the server when clients are desynchronized. Used to stop game execution
    /// </summary>
    [BurstCompile]
    public struct RpcLoadGame : INetcodeRPC
    {
        public RpcID GetID => RpcID.LoadGame;
        public int ClientNetworkID { get; set; }

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline reliableSimulationPipeline)
        {
            DataStreamWriter writer;
            mDriver.BeginSend(reliableSimulationPipeline, connection, out writer);

            writer.WriteByte((byte)GetID);
            writer.WriteInt(ClientNetworkID);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
            // Debug.Log("RPC telling clients to load the game send from server");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID
            ClientNetworkID = reader.ReadInt();

            // Debug.Log("RPC ordering to load the game received");
        }
    }
    
    /// <summary>
    /// Struct that is being send by the server when clients are desynchronized. Used to stop game execution
    /// </summary>
    [BurstCompile]
    public struct RpcPlayerReady : INetcodeRPC
    {
        public RpcID GetID => RpcID.PlayerReady;
        public int PlayerNetworkID { get; set; }

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline reliableSimulationPipeline)
        {
            DataStreamWriter writer;
            mDriver.BeginSend(reliableSimulationPipeline, connection, out writer);

            writer.WriteByte((byte)GetID);
            writer.WriteInt(PlayerNetworkID);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
            // Debug.Log("RPC telling that the client is ready send to server");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID
            PlayerNetworkID = reader.ReadInt();

            // Debug.Log("RPC telling that player is ready received");
        }
    }


    /// <summary>
    /// Struct that is being send by the server at the start of the game. It contains info about all players network IDs so the corresponding connections entities can be created. Additionally, it contains information about expected tickRate etc
    /// </summary>
    [BurstCompile]
    public struct RpcStartDeterministicSimulation : INetcodeRPC
    {
        /// <summary>
        /// Current clock time of the server
        /// </summary>
        public double TodaysMiliseconds { get; set; }
        
        /// <summary>
        /// Time after which the simulation should start
        /// </summary>
        public double PostponedStartInMiliseconds { get; set; }
        
        /// <summary>
        /// Ping that server has with the client
        /// </summary>
        public double PingInMilliseconds { get; set; }
        
        /// <summary>
        /// All of connected players ID so we can assign them to prefabs and connections
        /// </summary>
        public NativeList<int>  PlayersNetworkIDs { get; set; }

        /// <summary>
        /// Game tick rate set by the server
        /// </summary>
        public int TickRate { get; set; }

        /// <summary>
        /// Forced input latency value, given in ticks.
        /// </summary>
        public int TicksOfForcedInputLatency { get; set;} // can it be corrected so it can be adjusted to the players ping

        /// <summary>
        /// ID for this specific connection so we can set GhostOwnerIsLocal
        /// </summary>
        public int ThisConnectionNetworkID { get; set; }
        
        public uint SeedForPlayerRandomActions { get; set; }
        
        public int DeterminismHashCalculationOption { get; set; }

        public RpcID GetID => RpcID.StartDeterministicGameSimulation;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline reliableSimulationPipeline)
        {
            DataStreamWriter writer;
            mDriver.BeginSend(reliableSimulationPipeline, connection, out writer);

            writer.WriteByte((byte)GetID);
            writer.WriteDouble(TodaysMiliseconds);
            writer.WriteDouble(PostponedStartInMiliseconds);
            writer.WriteDouble(PingInMilliseconds);
            
            writer.WriteInt(PlayersNetworkIDs.Length);
            foreach (var id in PlayersNetworkIDs)
            {
                writer.WriteInt(id);
            }

            writer.WriteInt(TickRate);
            writer.WriteInt(TicksOfForcedInputLatency);
            writer.WriteInt(ThisConnectionNetworkID);
            writer.WriteUInt(SeedForPlayerRandomActions);
            writer.WriteInt(DeterminismHashCalculationOption);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
            // Debug.Log("RPC with start game request send from server");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID
            TodaysMiliseconds = reader.ReadDouble();
            PostponedStartInMiliseconds = reader.ReadDouble();
            PingInMilliseconds = reader.ReadDouble();
            
            var count = reader.ReadInt();
            PlayersNetworkIDs = new NativeList<int>(count, Allocator.Temp);

            for (var i = 0; i < count; i++)
            {
                PlayersNetworkIDs.Add(reader.ReadInt());
            }

            TickRate = reader.ReadInt();
            TicksOfForcedInputLatency = reader.ReadInt();
            ThisConnectionNetworkID = reader.ReadInt();
            SeedForPlayerRandomActions = reader.ReadUInt();
            DeterminismHashCalculationOption = reader.ReadInt();

            // Debug.Log("RPC from server about starting the game received");
        }
    }

    /// <summary>
    /// Struct that is being send by the clients to the server at the end of each tick. It contains info about all player inputs
    /// </summary>
    [BurstCompile]
    public struct RpcBroadcastPlayerTickDataToServer : INetcodeRPC
    {
        public PongInputs PongGameInputs;
        public int PlayerNetworkID { get; set; } // don't needed since server knows the connection ID?
        public int FutureTick { get; set; }
        public NativeList<ulong> HashesForFutureTick { get; set; } // empty, size(1) or size(systems) depending on the determinism check option
        public RpcID GetID => RpcID.BroadcastPlayerTickDataToServer;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline reliableSimulationPipeline)
        {
            DataStreamWriter writer;
            if(!mDriver.IsCreated || !connection.IsCreated) return;
            
            mDriver.BeginSend(reliableSimulationPipeline, connection, out writer);
            
            if(!writer.IsCreated) return;

            writer.WriteByte((byte)GetID);
            PongGameInputs.SerializeInputs(ref writer);
            writer.WriteInt(PlayerNetworkID);
            writer.WriteInt(FutureTick);
            
            var hashesSize = HashesForFutureTick.Length;
            writer.WriteInt(hashesSize);
            foreach (var hash in HashesForFutureTick)
            {
                writer.WriteULong(hash);
            }
            
            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes. Capacity: " + writer.Capacity + " Length: " + writer.Length + " Hashes: " + hashesSize);
            }
            
            mDriver.EndSend(writer);
            // Debug.Log("RPC send from client with input values");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID
            PongGameInputs.DeserializeInputs(ref reader);
            PlayerNetworkID = reader.ReadInt();
            FutureTick = reader.ReadInt();
            
            var hashesSize = reader.ReadInt();
            HashesForFutureTick = new NativeList<ulong>(hashesSize, Allocator.Persistent);
            for (var i = 0; i < hashesSize; i++)
            {
                HashesForFutureTick.Add(reader.ReadULong());
            }
            
            // Debug.Log("RPC received in the server with player data update");
        }
    }
    
    /// <summary>
    /// Struct that is being send by the server to all clients after receiving inputs from all of them. It contains info about all players network IDs so they can be identified as well as their corresponding inputs to apply and the tick for which those should be assigned
    /// </summary>
    [BurstCompile]
    public struct RpcBroadcastTickDataToClients : INetcodeRPC
    {
        /// <summary>
        /// All of connected players ID so we can assign them to prefabs and connections
        /// </summary>
        public NativeList<int> NetworkIDs { get; set; }
        
        /// <summary>
        /// All of connected players inputs that should be applied
        /// </summary>
        public NativeList<PongInputs> PlayersPongGameInputs { get; set; } 

        /// <summary>
        /// On which tick it should be applied (so for example first tick send can be received back as tick 9)
        /// </summary>
        public int SimulationTick { get; set; } 

        public RpcID GetID => RpcID.BroadcastTickDataToClients;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline reliableSimulationPipeline)
        {
            DataStreamWriter writer;
            if(!mDriver.IsCreated || !connection.IsCreated) return;
            
            mDriver.BeginSend(reliableSimulationPipeline, connection, out writer);
        
            if(!writer.IsCreated) return;
            
            writer.WriteByte((byte)GetID);
            writer.WriteInt(NetworkIDs.Length);
            foreach (var id in NetworkIDs)
            {
                writer.WriteInt(id);
            }

            writer.WriteInt(PlayersPongGameInputs.Length);
            foreach (var inputStruct in PlayersPongGameInputs)
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
            // Debug.Log("RPC with players input send from server");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID
            
            int idCount = reader.ReadInt();
            NetworkIDs = new NativeList<int>(idCount, Allocator.Persistent);
            for (var i = 0; i < idCount; i++)
            {
                NetworkIDs.Add(reader.ReadInt());
            }
            
            var inputsCount = reader.ReadInt();
            PlayersPongGameInputs = new NativeList<PongInputs>(inputsCount, Allocator.Persistent);
            for (var i = 0; i < inputsCount; i++)
            {
                var inputs = new PongInputs();
                inputs.DeserializeInputs(ref reader);
                PlayersPongGameInputs.Add(inputs);
            }
            
            SimulationTick = reader.ReadInt();
            
            // Debug.Log("RPC from server with players data update received");
        }
    }
    
    // /// <summary>
    // /// Struct that is being send by the clients only at the beginning (after connecting to a server) with their specific informations
    // /// </summary>
    // public struct RpcPlayerConfiguration : INetcodeRPC
    // {
    //     public NativeList<FixedString64Bytes> DeterministicSystemNamesDebug { get; set; } // used to mark specific systems when indeterministic
    //     public RpcID GetID => RpcID.PlayerConfiguration;
    //
    //     public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
    //         NetworkPipeline? pipeline = null)
    //     {
    //         DataStreamWriter writer;
    //         if (!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
    //         else mDriver.BeginSend(pipeline.Value, connection, out writer);
    //
    //         writer.WriteByte((byte)GetID);
    //         
    //         var systemAmount = DeterministicSystemNamesDebug.Length;
    //         writer.WriteInt(systemAmount);
    //         foreach (var systemName in DeterministicSystemNamesDebug)
    //         {
    //             writer.WriteFixedString64(systemName);
    //         }
    //         
    //         if (writer.HasFailedWrites)
    //         {
    //             mDriver.AbortSend(writer);
    //             throw new InvalidOperationException("Driver has failed writes. Capacity: " + writer.Capacity + " Length: " + writer.Length + " Names: " + systemAmount);
    //         }
    //         
    //         mDriver.EndSend(writer);
    //         Debug.Log("RPC send from client with client config values");
    //     }
    //
    //     public void Deserialize(ref DataStreamReader reader)
    //     {
    //         reader.ReadByte(); // ID
    //         
    //         var systemsAmount = reader.ReadInt();
    //         DeterministicSystemNamesDebug = new NativeList<FixedString64Bytes>(systemsAmount, Allocator.Persistent);
    //         for (var i = 0; i < systemsAmount; i++)
    //         {
    //             DeterministicSystemNamesDebug.Add(reader.ReadFixedString64());
    //         }
    //         
    //         Debug.Log("RPC received in the server with player data update");
    //     }
    // }
}