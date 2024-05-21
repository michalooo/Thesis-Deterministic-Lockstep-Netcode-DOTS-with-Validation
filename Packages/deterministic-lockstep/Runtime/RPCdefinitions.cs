using System;
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

        void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline? simulatorPipeline = null);
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
        PlayersDesynchronizedMessage
    }

    /// <summary>
    /// Struct that is being send by the server when clients are desynchronized. Used to stop game execution
    /// </summary>
    public struct RpcPlayerDesynchronizationMessage : INetcodeRPC
    {
        public RpcID GetID => RpcID.PlayersDesynchronizedMessage;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline? pipeline = null)
        {
            DataStreamWriter writer;
            if (!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
            else mDriver.BeginSend(pipeline.Value, connection, out writer);

            writer.WriteByte((byte)GetID);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
            Debug.Log("RPC stating that players disconnected send from server");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID

            Debug.Log("RPC stating that players disconnected received");
        }
    }


    /// <summary>
    /// Struct that is being send by the server at the start of the game. It contains info about all players network IDs so the corresponding connections entities can be created. Additionally, it contains information about expected tickRate etc
    /// </summary>
    public struct RpcStartDeterministicSimulation : INetcodeRPC
    {
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

        public RpcID GetID => RpcID.StartDeterministicGameSimulation;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline? pipeline = null)
        {
            DataStreamWriter writer;
            if (!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
            else mDriver.BeginSend(pipeline.Value, connection, out writer);

            writer.WriteByte((byte)GetID);
            writer.WriteInt(PlayersNetworkIDs.Length);
            foreach (var id in PlayersNetworkIDs)
            {
                writer.WriteInt(id);
            }

            writer.WriteInt(TickRate);
            writer.WriteInt(TicksOfForcedInputLatency);
            writer.WriteInt(ThisConnectionNetworkID);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }

            mDriver.EndSend(writer);
            Debug.Log("RPC with start game request send from server");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID
            var count = reader.ReadInt();

            PlayersNetworkIDs = new NativeList<int>(count, Allocator.Temp);

            for (var i = 0; i < count; i++)
            {
                PlayersNetworkIDs.Add(reader.ReadInt());
            }

            TickRate = reader.ReadInt();
            TicksOfForcedInputLatency = reader.ReadInt();
            ThisConnectionNetworkID = reader.ReadInt();

            Debug.Log("RPC from server about starting the game received");
        }
    }

    /// <summary>
    /// Struct that is being send by the clients to the server at the end of each tick. It contains info about all player inputs
    /// </summary>
    public struct RpcBroadcastPlayerTickDataToServer : INetcodeRPC
    {
        public PongInputs PongGameInputs;
        public int PlayerNetworkID { get; set; } // don't needed since server knows the connection ID?
        public int FutureTick { get; set; }
        public ulong HashForFutureTick { get; set; }

        public RpcID GetID => RpcID.BroadcastPlayerTickDataToServer;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline? pipeline = null)
        {
            DataStreamWriter writer;
            if (!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
            else mDriver.BeginSend(pipeline.Value, connection, out writer);

            writer.WriteByte((byte)GetID);
            PongGameInputs.SerializeInputs(ref writer);
            writer.WriteInt(PlayerNetworkID);
            writer.WriteInt(FutureTick);
            
            if (Input.GetKey(KeyCode.R)) // testing purposes
            {
                writer.WriteULong(HashForFutureTick +
                                  (ulong) Random.Range(0, 100)); // modify the position instead just the hash?
            }
            else writer.WriteULong(HashForFutureTick);

            if (writer.HasFailedWrites)
            {
                mDriver.AbortSend(writer);
                throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
            }
            
            mDriver.EndSend(writer);
            Debug.Log("RPC send from client with input values");
        }

        public void Deserialize(ref DataStreamReader reader)
        {
            reader.ReadByte(); // ID
            PongGameInputs.DeserializeInputs(ref reader);
            PlayerNetworkID = reader.ReadInt();
            FutureTick = reader.ReadInt();
            HashForFutureTick = reader.ReadULong();
            
            Debug.Log("RPC received in the server with player data update");
        }
    }
    
    /// <summary>
    /// Struct that is being send by the server to all clients after receiving inputs from all of them. It contains info about all players network IDs so they can be identified as well as their corresponding inputs to apply and the tick for which those should be assigned
    /// </summary>
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
            NetworkPipeline? pipeline = null)
        {
            DataStreamWriter writer;
            if (!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
            else mDriver.BeginSend(pipeline.Value, connection, out writer);

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
            Debug.Log("RPC with players input send from server");
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
            
            Debug.Log("RPC from server with players data update received");
        }
    }
}