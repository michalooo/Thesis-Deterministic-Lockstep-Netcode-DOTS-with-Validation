using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public interface INetcodeRPC
{
    public static RpcDefinitions.RpcID GetID
    {
        get;
    }
}
public static class RpcDefinitions //can be deleted
{
    // Definition of different RPC types
    public enum RpcID
    {
        StartDeterministicSimulation,
        BroadcastAllPlayersInputs,
        SendPlayerInputToServer
    }

    // RPC that server sends to all clients at the start of the game, it contains info about all players network IDs so the corresponding
    // connections entities can be created. Also contains information about expected tickRate
    public struct RpcStartGameAndSpawnPlayers: INetcodeRPC //change name
    {
        public RpcID id;
        public NativeList<int> networkIDs;
        public NativeList<Vector3> initialPositions;
        public int tickrate;
        public int connectionID; //networkID
        
        public static RpcID GetID => RpcID.StartDeterministicSimulation; // check out
    }
    
    // RPC that server sends to all clients after reciving inputs from all of them. It contains info about all players network IDs so they can be identified
    // as well as their corresponding inputs to apply and the tick for which those should be assigned
    public struct RpcPlayersDataUpdate
    {
        public RpcID id;
        public NativeList<int> networkIDs;
        public NativeList<Vector2> inputs; 
        public int tick;
    }
    
    // RPC that clients send to the server at the beginning of each tick. It contains info about all player input
    public struct RpcPlayerDataUpdate
    {
        public RpcID id;
        public Vector2 playerInput;  // Horizontal + Vertical input
        public int currentTick;
        public int connectionID; // remove
    }
}






public static class RpcUtils
{
    // RPC serialization method that is used by the client to send its input to the server
    public static void SendRPCWithPlayerInput(NetworkDriver mDriver, NetworkPipeline simulatorPipeline, NetworkConnection connection, PlayerInputDataToSend playerInput, GhostOwner owner, int tickNumber)
    {
        var rpcMessage = new RpcDefinitions.RpcPlayerDataUpdate
        {
            id = RpcDefinitions.RpcID.SendPlayerInputToServer,
            playerInput = new Vector2(playerInput.horizontalInput, playerInput.verticalInput),
            currentTick = tickNumber,
            connectionID = owner.networkId
        };
        
        mDriver.BeginSend(simulatorPipeline, connection, out var writer);
        writer.WriteInt((int) rpcMessage.id);
        writer.WriteInt((int) rpcMessage.playerInput.x); // Horizontal input
        writer.WriteInt((int) rpcMessage.playerInput.y); // Vertical input
        writer.WriteInt(rpcMessage.currentTick);
        writer.WriteInt(rpcMessage.connectionID);
        if (writer.HasFailedWrites) // check out
        {
            mDriver.AbortSend(writer);
            throw new InvalidOperationException("Driver has failed writes.: " +
                                                writer.Capacity); //driver too small for the schema of this rpc
        }

        mDriver.EndSend(writer);
        Debug.Log("RPC send from client with input values");
    }
    
    // RPC deserialization method that is used by the server to deserialize the input from the client
    public static RpcDefinitions.RpcPlayerDataUpdate DeserializeClientUpdatePlayerRPC(DataStreamReader stream)
    {
        RpcDefinitions.RpcPlayerDataUpdate rpcMessage = new RpcDefinitions.RpcPlayerDataUpdate
        {
            // ID is read in the scope above in order to use a proper deserializer
            id = RpcDefinitions.RpcID.SendPlayerInputToServer,
            playerInput = new Vector2(stream.ReadInt(), stream.ReadInt()),
            currentTick = stream.ReadInt(),
            connectionID = stream.ReadInt()
        };

        Debug.Log("RPC recived in the server with player data update");
        return rpcMessage;
    }
    
    // RPC serialization method that is used by the server to send inputs off all clients to the server
    public static void SendRPCWithPlayersInput(NetworkDriver mDriver, NetworkPipeline simulatorPipeline, NetworkConnection connection, NativeList<int> networkIDs, NativeList<Vector2> inputs, int tickRate)
    {
        var rpcMessage = new RpcDefinitions.RpcPlayersDataUpdate
        {
            id = RpcDefinitions.RpcID.BroadcastAllPlayersInputs,
            networkIDs = networkIDs,
            inputs = inputs,
            tick = tickRate
        };
        
        mDriver.BeginSend(connection, out var writer);
        writer.WriteInt((int) rpcMessage.id);
        writer.WriteInt(rpcMessage.networkIDs.Length);
        for (int i = 0; i < rpcMessage.networkIDs.Length; i++)
        {
            writer.WriteInt(rpcMessage.networkIDs[i]);
        }
        for (int i = 0; i < rpcMessage.inputs.Length; i++)
        {
            writer.WriteInt((int) rpcMessage.inputs[i].x);
            writer.WriteInt((int) rpcMessage.inputs[i].y);
        }
        writer.WriteInt(rpcMessage.tick);
        
        if (writer.HasFailedWrites) // check out
        {
            mDriver.AbortSend(writer);
            throw new InvalidOperationException("Driver has failed writes.: " +
                                                writer.Capacity); //driver too small for the schema of this rpc
        }
        
        mDriver.EndSend(writer);
        Debug.Log("RPC with players input send from server");
    }
    
    // RPC deserialization method that is used by the clients to deserialize the message from server with input from the other clients
    public static RpcDefinitions.RpcPlayersDataUpdate DeserializeServerUpdatePlayersRPC(DataStreamReader stream)
    {
        RpcDefinitions.RpcPlayersDataUpdate rpcMessage = new RpcDefinitions.RpcPlayersDataUpdate();
        // ID is read in the scope above in order to use a proper deserializer
        int count = stream.ReadInt();
        
        rpcMessage.networkIDs = new NativeList<int>(count, Allocator.Temp);
        rpcMessage.inputs = new NativeList<Vector2>(count, Allocator.Temp);
        
        for (int i = 0; i < count; i++)
        {
            rpcMessage.networkIDs.Add(stream.ReadInt());
        }
        
        for (int i = 0; i < count; i++)
        {
            rpcMessage.inputs.Add(new Vector2(stream.ReadInt(), stream.ReadInt()));
        }
        
        rpcMessage.tick = stream.ReadInt();
        
        Debug.Log("RPC from server with players data update received");
        return rpcMessage;
    }
    
    // RPC serialization method that is used by the server to send request to start game to the clients and the initial state of the game
    public static void SendRPCWithStartGameRequest(NetworkDriver mDriver, NetworkPipeline simulatorPipeline, NetworkConnection connection, NativeList<int> networkIDs, NativeList<Vector3> initialPositions, int tickRate, int connectionID)
    {
        var rpcMessage = new RpcDefinitions.RpcStartGameAndSpawnPlayers()
        {
            id = RpcDefinitions.RpcID.StartDeterministicSimulation,
            networkIDs = networkIDs,
            initialPositions = initialPositions,
            tickrate = tickRate,
            connectionID = connectionID,
        };
        
        mDriver.BeginSend(connection, out var writer);
        writer.WriteInt((int) rpcMessage.id);
        writer.WriteInt(rpcMessage.networkIDs.Length);
        for (int i = 0; i < rpcMessage.networkIDs.Length; i++)
        {
            writer.WriteInt(rpcMessage.networkIDs[i]);
            writer.WriteFloat(rpcMessage.initialPositions[i].x);
            writer.WriteFloat(rpcMessage.initialPositions[i].y);
            writer.WriteFloat(rpcMessage.initialPositions[i].z);
        }
        writer.WriteInt(rpcMessage.tickrate);
        writer.WriteInt(rpcMessage.connectionID);
        
        if (writer.HasFailedWrites) // check out
        {
            mDriver.AbortSend(writer);
            throw new InvalidOperationException("Driver has failed writes.: " +
                                                writer.Capacity); //driver too small for the schema of this rpc
        }
        
        mDriver.EndSend(writer);
        Debug.Log("RPC with start game request send from server");
    }

    // RPC deserialization method that is used by the clients to deserialize the initial request from the server to start the game
    public static RpcDefinitions.RpcStartGameAndSpawnPlayers DeserializeServerStartGameRpc(DataStreamReader stream)
    {
        RpcDefinitions.RpcStartGameAndSpawnPlayers rpcMessage = new RpcDefinitions.RpcStartGameAndSpawnPlayers();
        // ID is read in the scope above in order to use a proper deserializer
        int count = stream.ReadInt();

        rpcMessage.networkIDs = new NativeList<int>(count, Allocator.Temp);
        rpcMessage.initialPositions = new NativeList<Vector3>(count, Allocator.Temp);

        for (int i = 0; i < count; i++)
        {
            rpcMessage.networkIDs.Add(stream.ReadInt());
            rpcMessage.initialPositions.Add(new Vector3(stream.ReadFloat(), stream.ReadFloat(), stream.ReadFloat()));
        }

        rpcMessage.tickrate = stream.ReadInt();
        rpcMessage.connectionID = stream.ReadInt();

        Debug.Log("RPC from server about starting the game received");
        return rpcMessage;
    }
}