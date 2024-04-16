using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Interface for RPCs, all RPCs must implement this interface to ensure that we can get id from it, serialize it and deserialize it
/// </summary>
public interface INetcodeRPC // Interface for RPCs, all RPCs must implement this interface to ensure that we can get id from it, serialize it and deserialize it
{
    RpcID GetID { get; }
    
    void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline? simulatorPipeline = null);
    void Deserialize(DataStreamReader reader);
}

/// <summary>
/// Enum with all possible RPCs, this is used to identify the RPCs when serializing and deserializing them (one byte is enough space to cover them)
/// </summary>
public enum RpcID : byte // Enum with all possible RPCs, this is used to identify the RPCs when serializing and deserializing them (one byte is enough space to cover them)
{
    StartDeterministicSimulation,
    BroadcastAllPlayersInputsToClients,
    BroadcastPlayerInputToServer,
    PlayersDesynchronized
}

/// <summary>
/// Struct that is being send by the server when clients are desynchronized. Used to stop game execution
/// </summary>
public struct RpcPlayerDesynchronizationInfo: INetcodeRPC // RPC that is being send by the server when clients are desynchronized. Used to stop game execution
{
    public RpcID GetID => RpcID.PlayersDesynchronized;
    public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline? pipeline = null)
    {
        DataStreamWriter writer;
        if(!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
        else mDriver.BeginSend(pipeline.Value, connection, out writer);
        
        writer.WriteByte((byte) GetID);

        if (writer.HasFailedWrites) 
        {
            mDriver.AbortSend(writer);
            throw new InvalidOperationException("Driver has failed writes.: " + writer.Capacity);
        }
        
        mDriver.EndSend(writer);
        Debug.Log("RPC stating that players disconnected send from server");
    }

    public void Deserialize(DataStreamReader reader)
    {
        reader.ReadByte(); // ID
       
        Debug.Log("RPC stating that players disconnected received");
    }
}


/// <summary>
/// Struct that is being send by the server at the start of the game. It contains info about all players network IDs so the corresponding connections entities can be created. Additionally, it contains information about expected tickRate etc
/// </summary>
public struct RpcStartDeterministicSimulation: INetcodeRPC
{
    public NativeList<int> NetworkIDs { get; set; } // all of connected players ID so we can assign them to prefabs and connections
    public int TickRate { get; set; } // game tick rate set by the server
    public int TickAhead { get; set; } // how many ticks in the future should user send (should be corrected so it can be adjusted to the ping)
    public int NetworkID { get; set; } // ID for this specific connection so we can set GhostOwnerIsLocal

    public RpcID GetID => RpcID.StartDeterministicSimulation;

    public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline? pipeline = null) // set connection ID before sending
    {
        DataStreamWriter writer;
        if(!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
        else mDriver.BeginSend(pipeline.Value, connection, out writer);
        
        writer.WriteByte((byte) GetID);
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

    public void Deserialize(DataStreamReader reader)
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
/// Struct that is being send by the server to all clients after receiving inputs from all of them. It contains info about all players network IDs so they can be identified as well as their corresponding inputs to apply and the tick for which those should be assigned
/// </summary>
public struct RpcPlayersDataUpdate: INetcodeRPC
{
    public NativeList<int> NetworkIDs { get; set; } // all of connected players ID so we can assign them to prefabs and connections
    public NativeList<Vector2> Inputs { get; set; } // all of connected players inputs that should be applied
    public int Tick { get; set; } // on which tick it should be applied (so for example first tick send can be received back as tick 9

    public RpcID GetID => RpcID.BroadcastAllPlayersInputsToClients;

    public RpcPlayersDataUpdate(NativeList<int>? networkIDs, NativeList<Vector2>? inputs, int? tick)
    {
        NetworkIDs = networkIDs ?? new NativeList<int>(0, Allocator.Persistent);
        Inputs = inputs ?? new NativeList<Vector2>(0, Allocator.Persistent);
        Tick = tick ?? 0;
    }

    public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline? pipeline = null) // set networkIDs before sending
    {
        DataStreamWriter writer;
        if(!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
        else mDriver.BeginSend(pipeline.Value, connection, out writer);
        
        writer.WriteByte((byte) GetID);
        writer.WriteInt(NetworkIDs.Length);
        for (int i = 0; i < NetworkIDs.Length; i++)
        {
            writer.WriteInt(NetworkIDs[i]);
        }
        for (int i = 0; i < Inputs.Length; i++)
        {
            writer.WriteInt((int) Inputs[i].x);
            writer.WriteInt((int) Inputs[i].y);
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

    public void Deserialize(DataStreamReader reader)
    {
        reader.ReadByte(); // ID
        int count = reader.ReadInt();
        
        NetworkIDs = new NativeList<int>(count, Allocator.Persistent);
        Inputs = new NativeList<Vector2>(count, Allocator.Persistent);
        
        for (int i = 0; i < count; i++)
        {
            NetworkIDs.Add(reader.ReadInt());
        }
        
        for (int i = 0; i < count; i++)
        {
            Inputs.Add(new Vector2(reader.ReadInt(), reader.ReadInt()));
        }
        
        Tick = reader.ReadInt();
        
        Debug.Log("RPC from server with players data update received");
    }
}

/// <summary>
/// Struct that is being send by the clients to the server at the end of each tick. It contains info about all player inputs
/// </summary>
public struct RpcBroadcastPlayerInputToServer: INetcodeRPC
{
    public Vector2 PlayerInput { get; set; } // Horizontal + Vertical input
    public int CurrentTick { get; set; }
    public ulong HashForCurrentTick { get; set; }

    public RpcID GetID => RpcID.BroadcastPlayerInputToServer;

    public void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline? pipeline = null) // set Hash before sending
    {
        DataStreamWriter writer;
        if(!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
        else mDriver.BeginSend(pipeline.Value, connection, out writer);
        
        writer.WriteByte((byte) GetID);
        writer.WriteInt((int) PlayerInput.x); // Horizontal input
        writer.WriteInt((int) PlayerInput.y); // Vertical input
        writer.WriteInt(CurrentTick);
        if (Input.GetKey(KeyCode.R))
        {
            writer.WriteULong(HashForCurrentTick + (ulong) Random.Range(0, 100)); // modify the position instead just the hash
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

    public void Deserialize(DataStreamReader reader)
    {
        reader.ReadByte(); // ID
        PlayerInput = new Vector2(reader.ReadInt(), reader.ReadInt());
        CurrentTick = reader.ReadInt();
        HashForCurrentTick = reader.ReadULong();
        
        Debug.Log("RPC received in the server with player data update");
    }
}