using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public interface INetcodeRPC
{
    RpcID GetID { get; }
    
    void Serialize(NetworkDriver mDriver, NetworkConnection connection, int? connectionID, NetworkPipeline? simulatorPipeline = null);
    void Deserialize(DataStreamReader reader);
}

// Definition of different RPC types
public enum RpcID
{
    StartDeterministicSimulation,
    BroadcastAllPlayersInputsToClients,
    BroadcastPlayerInputToServer
}

// RPC that server sends to all clients at the start of the game, it contains info about all players network IDs so the corresponding
// connections entities can be created. Also contains information about expected tickRate
public struct RpcStartDeterministicSimulation: INetcodeRPC
{
    public NativeList<int> networkIDs;
    public NativeList<Vector3> initialPositions;
    public int tickrate;
    public int connectionID; //networkID
        
    public RpcID GetID => RpcID.StartDeterministicSimulation;
    
    public RpcStartDeterministicSimulation(NativeList<int>? networkIDs, NativeList<Vector3>? initialPositions, int? tickrate, int? connectionID)
    {
        this.networkIDs = networkIDs ?? new NativeList<int>(0, Allocator.Temp);
        this.initialPositions = initialPositions ?? new NativeList<Vector3>(0, Allocator.Temp);
        this.tickrate = tickrate ?? 0;
        this.connectionID = connectionID ?? 0;
    }

    public void Serialize(NetworkDriver mDriver, NetworkConnection connection, int? connectionID, NetworkPipeline? pipeline = null)
    {
        DataStreamWriter writer;
        if(!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
        else mDriver.BeginSend(pipeline.Value, connection, out writer);
        
        writer.WriteInt((int) GetID);
        writer.WriteInt(networkIDs.Length);
        for (int i = 0; i < networkIDs.Length; i++)
        {
            writer.WriteInt(networkIDs[i]);
            writer.WriteFloat(initialPositions[i].x);
            writer.WriteFloat(initialPositions[i].y);
            writer.WriteFloat(initialPositions[i].z);
        }
        writer.WriteInt(tickrate);
        writer.WriteInt(connectionID ?? 0);
        
        if (writer.HasFailedWrites) // check out
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
        // ID is read in the scope above in order to use a proper deserializer
        int count = reader.ReadInt();

        networkIDs = new NativeList<int>(count, Allocator.Temp);
        initialPositions = new NativeList<Vector3>(count, Allocator.Temp);

        for (int i = 0; i < count; i++)
        {
            networkIDs.Add(reader.ReadInt());
            initialPositions.Add(new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat()));
        }

        tickrate = reader.ReadInt();
        connectionID = reader.ReadInt();

        Debug.Log("RPC from server about starting the game received");
    }
}
    
// RPC that server sends to all clients after reciving inputs from all of them. It contains info about all players network IDs so they can be identified
// as well as their corresponding inputs to apply and the tick for which those should be assigned
public struct RpcPlayersDataUpdate: INetcodeRPC
{
    public NativeList<int> networkIDs;
    public NativeList<Vector2> inputs; 
    public int tick;
    
    public RpcID GetID => RpcID.BroadcastAllPlayersInputsToClients;
    
    public RpcPlayersDataUpdate(NativeList<int>? networkIDs, NativeList<Vector2>? inputs, int? tick)
    {
        this.networkIDs = networkIDs ?? new NativeList<int>(0, Allocator.Temp);
        this.inputs = inputs ?? new NativeList<Vector2>(0, Allocator.Temp);
        this.tick = tick ?? 0;
    }

    public void Serialize(NetworkDriver mDriver, NetworkConnection connection, int? connectionID, NetworkPipeline? pipeline = null)
    {
        DataStreamWriter writer;
        if(!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
        else mDriver.BeginSend(pipeline.Value, connection, out writer);
        
        writer.WriteInt((int) GetID);
        writer.WriteInt(networkIDs.Length);
        for (int i = 0; i < networkIDs.Length; i++)
        {
            writer.WriteInt(networkIDs[i]);
        }
        for (int i = 0; i < inputs.Length; i++)
        {
            writer.WriteInt((int) inputs[i].x);
            writer.WriteInt((int) inputs[i].y);
        }
        writer.WriteInt(tick);
        
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
        // ID is read in the scope above in order to use a proper deserializer
        int count = reader.ReadInt();
        
        networkIDs = new NativeList<int>(count, Allocator.Temp);
        inputs = new NativeList<Vector2>(count, Allocator.Temp);
        
        for (int i = 0; i < count; i++)
        {
            networkIDs.Add(reader.ReadInt());
        }
        
        for (int i = 0; i < count; i++)
        {
            inputs.Add(new Vector2(reader.ReadInt(), reader.ReadInt()));
        }
        
        tick = reader.ReadInt();
        
        Debug.Log("RPC from server with players data update received");
    }
}
    
// RPC that clients send to the server at the beginning of each tick. It contains info about all player input
public struct RpcBroadcastPlayerInputToServer: INetcodeRPC
{
    public Vector2 playerInput;  // Horizontal + Vertical input
    public int currentTick;
    public int connectionID; // remove
    
    public RpcID GetID => RpcID.BroadcastPlayerInputToServer;
    
    public RpcBroadcastPlayerInputToServer(Vector2? playerInput, int? currentTick, int? connectionID)
    {
        this.playerInput = playerInput ?? Vector2.zero;
        this.currentTick = currentTick ?? 0;
        this.connectionID = connectionID ?? 0;
    }

    public void Serialize(NetworkDriver mDriver, NetworkConnection connection, int? connectionID, NetworkPipeline? pipeline = null)
    {
        DataStreamWriter writer;
        if(!pipeline.HasValue) mDriver.BeginSend(connection, out writer);
        else mDriver.BeginSend(pipeline.Value, connection, out writer);
        
        writer.WriteInt((int) GetID); // error here with assigned ID, probably different serializer and deserializer
        writer.WriteInt((int) playerInput.x); // Horizontal input
        writer.WriteInt((int) playerInput.y); // Vertical input
        writer.WriteInt(currentTick);
        writer.WriteInt(connectionID ?? 0);
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
        // ID is read in the scope above in order to use a proper deserializer
        playerInput = new Vector2(reader.ReadInt(), reader.ReadInt());
        currentTick = reader.ReadInt();
        connectionID = reader.ReadInt();
       

        Debug.Log("RPC recived in the server with player data update");
    }
}