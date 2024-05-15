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
    public interface INetcodeRPC
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
        StartDeterministicSimulation,
        BroadcastAllPlayersInputsToClients,
        BroadcastPlayerInputToServer,
        PlayersDesynchronized
    }

    /// <summary>
    /// Struct that is being send by the server when clients are desynchronized. Used to stop game execution
    /// </summary>
    public struct RpcPlayerDesynchronizationInfo : INetcodeRPC
    {
        public RpcID GetID => RpcID.PlayersDesynchronized;

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
        public NativeList<int>  NetworkIDs { get; set; }

        /// <summary>
        /// Game tick rate set by the server
        /// </summary>
        public int TickRate { get; set; }

        /// <summary>
        /// How many ticks in the future should user send (should be corrected so it can be adjusted to the ping)
        /// </summary>
        public int TickAhead { get; set;} 

        /// <summary>
        /// ID for this specific connection so we can set GhostOwnerIsLocal
        /// </summary>
        public int NetworkID { get; set; }

        public RpcID GetID => RpcID.StartDeterministicSimulation;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline? pipeline = null)
        {
            DataStreamWriter writer;
            if (!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
            else mDriver.BeginSend(pipeline.Value, connection, out writer);

            writer.WriteByte((byte)GetID);
            writer.WriteInt(NetworkIDs.Length);
            for (int i = 0; i < NetworkIDs.Length; i++)
            {
                writer.WriteInt(NetworkIDs[i]);
            }

            writer.WriteInt(TickRate);
            writer.WriteInt(TickAhead);
            writer.WriteInt(NetworkID);

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
            int count = reader.ReadInt();

            NetworkIDs = new NativeList<int>(count, Allocator.Temp);

            for (int i = 0; i < count; i++)
            {
                NetworkIDs.Add(reader.ReadInt());
            }

            TickRate = reader.ReadInt();
            TickAhead = reader.ReadInt();
            NetworkID = reader.ReadInt();

            Debug.Log("RPC from server about starting the game received");
        }
    }

    /// <summary>
    /// Struct that is being send by the clients to the server at the end of each tick. It contains info about all player inputs
    /// </summary>
    public struct RpcBroadcastPlayerInputToServer : INetcodeRPC
    {
        public PongInputs CapsuleGameInputs;
        public int PlayerNetworkID { get; set; } // don't needed since server knows the connection ID
        public int CurrentTick { get; set; }
        public ulong HashForCurrentTick { get; set; }
        
        public RpcBroadcastPlayerInputToServer(PongInputs? input)
        {
            CapsuleGameInputs = input ?? new PongInputs();
            PlayerNetworkID = 0;
            CurrentTick = 0;
            HashForCurrentTick = 0;
        }

        public RpcID GetID => RpcID.BroadcastPlayerInputToServer;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline? pipeline = null)
        {
            DataStreamWriter writer;
            if (!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
            else mDriver.BeginSend(pipeline.Value, connection, out writer);

            writer.WriteByte((byte)GetID);
            CapsuleGameInputs.SerializeInputs(ref writer);
            writer.WriteInt(PlayerNetworkID);
            writer.WriteInt(CurrentTick);
            
            if (Input.GetKey(KeyCode.R)) // testing purposes
            {
                writer.WriteULong(HashForCurrentTick +
                                  (ulong)Random.Range(0, 100)); // modify the position instead just the hash
            }
            else writer.WriteULong(HashForCurrentTick);

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
            CapsuleGameInputs.DeserializeInputs(ref reader);
            PlayerNetworkID = reader.ReadInt();
            CurrentTick = reader.ReadInt();
            HashForCurrentTick = reader.ReadULong();
            
            Debug.Log("RPC received in the server with player data update");
        }
    }
    
    /// <summary>
    /// Struct that is being send by the server to all clients after receiving inputs from all of them. It contains info about all players network IDs so they can be identified as well as their corresponding inputs to apply and the tick for which those should be assigned
    /// </summary>
    public struct RpcPlayersDataUpdate : INetcodeRPC
    {
        /// <summary>
        /// All of connected players ID so we can assign them to prefabs and connections
        /// </summary>
        public NativeList<int> NetworkIDs { get; set; }
        
        /// <summary>
        /// All of connected players inputs that should be applied
        /// </summary>
        public NativeList<PongInputs> PlayersCapsuleGameInputs { get; set; } 

        /// <summary>
        /// On which tick it should be applied (so for example first tick send can be received back as tick 9
        /// </summary>
        public int Tick { get; set; } 
        
        public RpcPlayersDataUpdate(NativeList<int>? networkIDs, NativeList<PongInputs>? inputs, int? tick)
        {
            NetworkIDs = networkIDs ?? new NativeList<int>(0, Allocator.Persistent);
            PlayersCapsuleGameInputs = inputs ?? new NativeList<PongInputs>(0, Allocator.Persistent);
            Tick = tick ?? 0;
        }

        public RpcID GetID => RpcID.BroadcastAllPlayersInputsToClients;

        public void Serialize(NetworkDriver mDriver, NetworkConnection connection,
            NetworkPipeline? pipeline = null) // set networkIDs before sending
        {
            DataStreamWriter writer;
            if (!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
            else mDriver.BeginSend(pipeline.Value, connection, out writer);

            writer.WriteByte((byte)GetID);
            writer.WriteInt(NetworkIDs.Length);
            for (int i = 0; i < NetworkIDs.Length; i++)
            {
                writer.WriteInt(NetworkIDs[i]);
            }

            writer.WriteInt(PlayersCapsuleGameInputs.Length);
            for (int i = 0; i < PlayersCapsuleGameInputs.Length; i++)
            {
                PlayersCapsuleGameInputs[i].SerializeInputs(ref writer);
            }

            writer.WriteInt(Tick);

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
            for (int i = 0; i < idCount; i++)
            {
                NetworkIDs.Add(reader.ReadInt());
            }
            
            int inputsCount = reader.ReadInt();
            PlayersCapsuleGameInputs = new NativeList<PongInputs>(inputsCount, Allocator.Persistent);
            for (int i = 0; i < inputsCount; i++)
            {
                var inputs = new PongInputs();
                inputs.DeserializeInputs(ref reader);
                PlayersCapsuleGameInputs.Add(inputs);
            }
            
            Tick = reader.ReadInt();
            
            

            Debug.Log("RPC from server with players data update received");
        }
    }

    
}