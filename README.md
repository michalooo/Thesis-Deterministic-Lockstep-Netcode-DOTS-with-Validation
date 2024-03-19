# Scenes

The game cosist of 3 scenes 
1. **Menu** --> In this scene player can choose to either host a game or join one (right now only on localhost). There is also an option to Exit
2. **Loading** --> In this scene players can connect to the server/host and game will start when host will press space.
3. **Game** --> During this stage no new connections are accepted and players will start to exchange input with the server

# Systems

- **RPCdefinitions** --> This static class contains all the functions and structs necessary to serialize and deserialize custom RPC messages in which we can distuinguish
  - **RpcStartGameAndSpawnPlayers**: RPC type used when starting game and used to send initial data regarding all players to everyone by the server
  - **RpcPlayersDataUpdate**: RPC type used by the server to send data with input from all players
  - **RpcPlayerDataUpdate**: RPC type used by the client to send input data to the server
- **ServerBehaviour** --> This system is responsible for handling connections via Unity Transport from the server side and also for handling incoming RPCs in which right now we can only distinguish 1 options which is input data coming from the client. After reciving said data the input is saved in a dictionary (which is covering different ticks and all related inputs) and if all the players inputs are registered for the lates tick the aproperiate RPC is send to all clients with inputs from other players. At the beginning of the game this system is also responsible for sending initial data to clients together with specified tickRate for the game.
- **ClientBehaviour** --> This system is responsible for handling connections via Unity Transport from the client side and also for handling incoming RPCs in which right now we can distinguish 2 options which is:
  - **PlayersDataUpdate**: When reciving this rpc proper components are enabled and data assigned to process
  - **StartGameAndSpawnPlayers**: When reciving this rpc the connectionEntities and player prefabs are created with aproperiate components (like CommandTarget, GhostOwner, etc)
- **SpawnPlayerSystem** --> System responsible for spawning players if connectionEntity exists and is without PlayerSpawned tag component
- **PlayerUpdateSystem** --> System responsible for updating players positions when PlayerInputDataToUse component is enabled
- **PlayerInputGatherAndSendSystem** --> System responsible for gathering and sending local player input to the server when PlayerInputDataToSend component is enabled. The current tickRate for the game will also increase after sending the input.
- **PlayerSendSystem** --> System responsible for enabling PlayerInputDataToSend component when the tickrate is proper
- **MenuHandler** --> System responsible for managing menu buttons and also creating needed server and client worlds when changing the scene to "Loading"
