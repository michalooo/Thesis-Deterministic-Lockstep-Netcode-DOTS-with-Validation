using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using Random = UnityEngine.Random;

public interface INetcodeRPC
{
    RpcID GetID { get; }
    
    void Serialize(NetworkDriver mDriver, NetworkConnection connection, NetworkPipeline? simulatorPipeline = null);
    void Deserialize(DataStreamReader reader);
}

// Definition of different RPC types
public enum RpcID : byte
{
    StartDeterministicSimulation,
    BroadcastAllPlayersInputsToClients,
    BroadcastPlayerInputToServer
}

// RPC that server sends to all clients at the start of the game, it contains info about all players network IDs so the corresponding
// connections entities can be created. Also contains information about expected tickRate
public struct RpcStartDeterministicSimulation: INetcodeRPC
{
    public NativeList<int> NetworkIDs { get; set; }
    public int Tickrate { get; set; }
    public int NetworkID { get; set; }

    public RpcID GetID => RpcID.StartDeterministicSimulation;

    public RpcStartDeterministicSimulation(NativeList<int>? networkIDs, NativeList<Vector3>? initialPositions, int? tickrate, int? networkID)
    {
        NetworkIDs = networkIDs ?? new NativeList<int>(8, Allocator.Temp);
        Tickrate = tickrate ?? 60;
        NetworkID = networkID ?? 0;
    }

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
        writer.WriteInt(Tickrate);
        writer.WriteInt(NetworkID);
        
        if (writer.HasFailedWrites) 
        {
            mDriver.AbortSend(writer);
            throw new InvalidOperationException("Driver has failed writes.: " +
                                                writer.Capacity); //driver too small for the schema of this rpc
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

        Tickrate = reader.ReadInt();
        NetworkID = reader.ReadInt();

        Debug.Log("RPC from server about starting the game received");
    }
}
    
// RPC that server sends to all clients after reciving inputs from all of them. It contains info about all players network IDs so they can be identified
// as well as their corresponding inputs to apply and the tick for which those should be assigned
public struct RpcPlayersDataUpdate: INetcodeRPC
{
    public NativeList<int> NetworkIDs { get; set; }
    public NativeList<Vector2> Inputs { get; set; }
    public int Tick { get; set; }

    public RpcID GetID => RpcID.BroadcastAllPlayersInputsToClients;


    public RpcPlayersDataUpdate(NativeList<int>? networkIDs, NativeList<Vector2>? inputs, int? tick)
    {
        NetworkIDs = networkIDs ?? new NativeList<int>(0, Allocator.Temp);
        Inputs = inputs ?? new NativeList<Vector2>(0, Allocator.Temp);
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
            throw new InvalidOperationException("Driver has failed writes.: " +
                                                writer.Capacity); //driver too small for the schema of this rpc
        }
        
        mDriver.EndSend(writer);
        Debug.Log("RPC with players input send from server");
    }

    public void Deserialize(DataStreamReader reader)
    {
        reader.ReadByte(); // ID
        int count = reader.ReadInt();
        
        NetworkIDs = new NativeList<int>(count, Allocator.Temp);
        Inputs = new NativeList<Vector2>(count, Allocator.Temp);
        
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
    
// RPC that clients send to the server at the beginning of each tick. It contains info about all player input
public struct RpcBroadcastPlayerInputToServer: INetcodeRPC
{
    public Vector2 PlayerInput { get; set; } // Horizontal + Vertical input
    public int CurrentTick { get; set; }
    public ulong HashForCurrentTick { get; set; }

    public RpcID GetID => RpcID.BroadcastPlayerInputToServer;


    public RpcBroadcastPlayerInputToServer(Vector2? playerInput, int? currentTick)
    {
        PlayerInput = playerInput ?? Vector2.zero;
        CurrentTick = currentTick ?? 0;
        HashForCurrentTick = 0;
    }

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
            writer.WriteULong(HashForCurrentTick + (ulong) Random.Range(0, 100));
        }
        else writer.WriteULong(HashForCurrentTick);
        
        if (writer.HasFailedWrites) 
        {
            mDriver.AbortSend(writer);
            throw new InvalidOperationException("Driver has failed writes.: " +
                                                writer.Capacity); //driver too small for the schema of this rpc
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
       

        Debug.Log("RPC recived in the server with player data update");
    }
}