# Project Title
Original Thesis Name: "Scalability and Democratization of Real Time Strategy and Fighting Games:
The Deterministic Lockstep Netcode Model with Client Prediction Combined
with the Data Oriented Tech Stack."

Current Name: "Deterministic Lockstep Netcode Model with Determinism validation and debugging tooling for Unity DOTS"

## Overview
The repository contains the source code for the package made for Unity DOTS which allows for creating games which utilize deterministic lockstep netcode model, validate determinism in such game, as well as provide nondeterminism debugging tooling. The repository also provides code and assets of a sample Pong game that demonstrates the usage of the package.

Tool was implemented using Unity's Data-Oriented Technology Stack (DOTS). It needs to be noted that currently this is a showcase of methodology rather than fully working solution which will be a matter of further development.

## Features provided by the package

- **Deterministic Lockstep Netcode Model:** Package allows to use deterministic lockstep netcode model in Unity DOTS which syncs the players by only sending their inputs. The package offers the basic version of deterministic lockstep without build-in lag mitigation techniques making it not production ready for fast paced games in the current form and serves more as an example and starting point of how to implement it.
- **Determinism Validation Tools:** On top of deterministic lockstep netcode model there is a validation functionality in place which allows to detects nondeterministic behavior by comparing game state hashes. How exactly it works in described later in the README.
- **Nondeterminism Debugging Tools:** When nondeterminism is detected, tools to help identify and resolve its sources is provided which loggs game state changes and allows to compare them. This project proposes a solution which may narrow down the code to be tested by using per-system validation which may point to exact system that caused desync.

## Getting Started
These instructions will get you a copy of the project up and running on your local machine for development and testing purposes.

### Prerequisites
- Installed Unity 6000.0.0.b16 editor

### Installation
1. **Clone the Repository**:
    ```sh
    git clone https://github.com/michalooo/Thesis-Deterministic-Lockstep-Netcode-DOTS-with-Validation.git
    ```

2. **Open in Unity**:
    - Open the project in Unity 6000.0.0.b16

3. **Download necessary packages**:
    - The project already contains modified version of com.unity.entities@1.2.3
    - Download any other packages which are listed in the project`s "manifest.json"

4. **Start the sample**:
    - In the editor, navigate to Assets/Pong/Scenes and open "PongMenu" scene
    - Click "Play" button in the editor to start the game

## Sample Game Description
The sample game provided in the package is a modified version of the classic Pong. Two players compete by controlling paddles to hit a ball back and forth. The game starts by spawning one ball per tick until 1000 balls are spawned. The game counts the score, and when it is over, the game stops, waits for 5 seconds, and then returns to the menu.

The following elements were implemented for the sample:

### Entities
1. **BallSpawner**: A ball spawner entity is created in the scene and positioned in the middle of the game field. It has an authoring component to which the ball Prefab is attached, which is later
used by the BallSpawnSystem to reference and spawn it. The ball will be spawned by the BallSpawnSystem later, which will use the ball spawner entity position and spawn the ball Prefab there.

2. **PlayerSpawner**:
Similarly to the BallSpawner entity, the PlayerSpawner entity is also created in the subscene and has an authoring component with PongPlayerSpawner component where a player Prefab is attached. It is used by the PlayerSpawnSystem to spawn players at the start of the game while referencing the player prefab.

3. **Ball**:
Ball entites are created once per simulation tick by the BallSpawnSystem. They are spawned in the position of a ball spawner entity, transforming into the ball Prefab into the ball Prefab assigned to it. Then the velocity component value is set, and the entity is considered spawned.

4. **Player**:
Two player entities are created at the beginning of the game via the PlayerSpawnSystem, transforming the created player Prefab into an entity. They are created at predefined positions on each side of the game scene.

### Components
1. **Velocity**: The velocity component is used to indicate the ball’s speed and direction in the form of a float3 value. This component is assigned to an entity when the Prefab is created as an entity by the spawner. The value of this component is set randomly on entity creation to randomise the speed and direction of the ball with constraints of maximum and minimum speed and angle.

2. **PlayerInput**: PlayerInput is a singleton component which for now is implemented inside of the package and the future work will include the separation of it from the package. It holds the current player input, which, in the case of the Pong game, is represented by a VerticalInput float value indicating the direction and magnitude of the player’s movement. In particular, PlayerInput does not store raw input (such as key presses like ”w”), but rather the transformed input that signifies the exact action.

3. **PongBallSpawner**: PongBallSpawner component contains ball entity object to spawn and its baked automatically into BallSpawner Entity.

4. **PongPlayerSpawner**: Similarly to PongBallSpawner component, PongPlayerSpawner component contains player entity object to spawn and its baked automatically into player spawner entity.

5. **Player**: The Player component is a tag component used to mark entities that represent players. This is important for querying purposes, as otherwise, the player entities would only have a Transform component assigned, which is insufficient for creating player-specific queries.

### Systems

1. **PlayerSpawnSystem**:
Responsible for filtering the PongPlayerSpawner component, which exists as a single instance in the scene. This system spawns two players at the beginning of the game by querying the PlayerSpawner component and retrieving the associated entity. After the initial player spawn, the system is disabled as no additional player spawns are required.

2. **InputGatherSystem**:
Runs before the PlayerMovementSystem in each frame, detecting and interpreting player key presses to populate the PlayerInput component. This system queries the PlayerInput component, a singleton component with only one instance in the game, and updates its value. In the sample Pong game, the "w" and "s" inputs are interpreted as values of 1 or -1 for the verticalInput in this component, indicating player movement direction.

3. **PlayerMovementSystem**:
Updates player positions based on the PlayerInput component. It queries for player and transform components and moves players up or down according to the input values, multiplied by deltaTime to ensure consistent movement. Both players move locally as the base game is not yet multiplayer.

4. **BallSpawnSystem**:
Filters the PongBallSpawner component, of which only one instance exists in the scene, to spawn one ball per frame until 1000 balls have been spawned. It queries the BallSpawner component, retrieves the associated entity, and spawns a ball at the spawner’s position. The system then randomly sets the velocity component of the spawned ball entity.

5. **BallMovementSystem**:
Moves the balls every frame according to their Velocity component values. This system filters for transform and velocity components (present on ball entities) and utilizes parallel jobs for efficiency due to the large number of balls. It creates a query to gather entities with LocalTransform and Velocity components, stores specific components in temporary arrays, and updates ball positions through a parallel job (BallMovementJob). The job uses EntityCommandBuffer.ParallelWriter to ensure safe parallel updates, calculating new positions based on velocity and deltaTime. After the job completes, the command buffer is played back to apply changes, and temporary arrays are disposed of to free up memory.

6. **BallBounceSystem**:
Checks for ball collisions with walls or players. It has two queries: one for entities with velocity and transform components, and another for entities with transform and player components. Using these queries, the system updates the ball’s velocity component to reflect bounces upon collision with walls or players.

7. **BallDestroySystem**:
Queries for transform and velocity components to check if a ball has crossed the boundary line. If a ball entity crosses the boundary, it is destroyed, and points are added accordingly via the UIManager class.

### Other

1. **Game Scenes**
The game consists of two main scenes: a menu scene and a game scene. The menu scene contains buttons for starting the game and other functionalities. The game scene includes the core game elements such as the middle line, points text, player spawner, and ball spawner entities. The spawner entities include only the transform component and are not visually represented.

2. **Prefabs**
Prefabs used in the game include a ball prefab and a player prefab. The ball prefab, for example, includes a transform component, a sprite renderer for visualization, and an authoring script that adds a velocity component during entity creation.

3. **Monobehaviour scripts**
- **GameManager**: Handles points management, game end conditions, and determining the winner. It stops the execution of other systems and displays the game result when a game ends.
- **PongMenuHandler**: Manages the behavior of buttons in the menu scene, such as starting the game and creating server and client worlds when the "Host Game" button is pressed.

## Package Lockstep Netcode Model Description and Usage

Package implements a basic deterministic lockstep netcode model with added forced input latency. This netcode model is used by the sample game and may be used in custom game implementation. The package is not using any netcode packages provided by Unity like for example Netcode for Entities and instead all of netcode necessary elements are created within this package.
The most important parts of it are:

- **Connection entity**: When client connects to the server an connection entity is created to handle the data for the specific connection. This includes information about the local player’s connection
to the server as well as the representation of remote player connections (in the case of the Pong game, there would be two such connection entities). Connection entity is used to represent each player’s networked state.
- **NetworkConnectionRefference component**: This component, added automatically by the package to the connection entity, stores the necessary parameters of the transport package to properly track each client’s connection. It ensures that the connection details for each client are correctly managed, specifically tracking where information should be sent and from where it should be received.
- **GameSettings component**: The package provides an authoring component with the necessary data, which the developer needs to add to the scene. In the case of the Pong game, the player can modify
these settings via a menu, which would be restricted in a production game but is allowed here for the ease of testing. When hosting a game, the server will transmit the information from these settings (such as the intended tick rate) to connected users to ensure their simulations use the same settings, which is crucial for determinism.
- **DeterministicTime component**: During the simulation, the package needs to keep track of changing data, such as the current simulation tick, the timing for the next tick (which may vary depending on the
intended simulation speed), and inputs sent by the server for future use while using forced input latency. This component is automatically created and managed by the package, utilizing the settings defined by the developer.
- **GhostOwner component**: This component is automatically added by the package to the connection entity. It creates a link between the connection entity and the actual player entity spawned in the game
scene. The GhostOwner component holds information about the player’s network ID, which is uniquely assigned by the server, and the entity that should be affected by commands saved in the connection entity.
Upon the creation of this component, the ID is filled out, as it is created for a valid client. However, the entity reference needs to be added by the developer in the PlayerSpawnSystem when spawning the player entity. This reference is necessary to specify which player should be in control of which entity.
- **GhostOwnerIsLocal component**: This is a tag component added by the package to the connection entity representing the local client. It simplifies identifying the local client and directly accessing related data.
- **PlayerSpawned**: The PlayerSpawned component is a tag component used to mark whether a player entity has been spawned for a given connection. When a player entity is spawned, the PlayerSpawned component should be added to the connection entity, and the GhostOwner entity reference should be updated to point to the newly created player entity.
- **PlayerInputDataToUse**: PlayerInputDataToUse is a component added by the package to the connection entity to hold input-related information. The input data from the server will first be loaded into these components. Then, the system using this data will run, followed by the system gathering inputs, and finally, the system that sends inputs based on the local connection’s values. The component is added automatically to the connection entity. 
- **RPC definitions and network communication**: Communication between clients and server is handled through various RPCs. Each RPC consists of data fields, a serialization method that sends the data over the internet, and a deserialization method that reconstructs the RPC data from the received network data. **Unity’s transport package** is used to send data over the internet. If data arrives and is part of an RPC, the server deserializes this data to handle it. The same applies to the client for RPC messages sent from the server. An important note is that all RPCs are created, serialised, and deserialised by the package, and developers don’t need to do anything about them. The **LoadGame RPC** is sent by the server to clients when the host starts the game (by pressing Space button in Pong game) to instruct each connected client to load the game and prepare for the simulation. This includes sending all game settings to ensure the simulation is set up consistently across all clients. Once a client is ready (the game is loaded), it sends a **PlayerReady RPC** to the server, signalling its readiness to start the simulation. When the server receives a **PlayerReady** message from every client, it sends a **StartDeterministicGameSimulation RPC** to signal the clients to begin the Lockstep simulation. From that point onward, clients send a **BroadcastPlayerTickDataToServer RPC** every tick, containing the necessary data for that tick. Upon collecting data from all clients, the server sends a **BroadcastTickDataToClients RPC** to each client, containing the summarised data of every player’s input for the tick to process.
- **PlayerInputSendSystem**: The general purpose of this system is to sends inputs to the server at the end of the DeterministicSimulationSystemGroup. This system is created by the package and queries for the GhostOwnerIsLocal and PlayerInputDataToUse components to get the input values of the local player. These inputs should be constantly updated by the developer, so the assumption is that they are the latest user inputs since the PlayerInputSendSystem runs as the last system. Every time this system runs, the BroadcastPlayerTickDataToServer RPC is created, the input data is saved into it, and then the RPC is serialised and sent to the server along with the client ID and the tick for which the data is intended. Currently, it is necessary to use code generation to fetch the input type and data from the package perspective, which is a significant challenge that was omitted for this phase of package development. Currently, the input structure needs to be defined within the package. However, in the future, the package should allow users to define their own input structures outside of it by implementing an interface from the package that enforces the creation of serialisation and deserialisation methods. The package should then generate the necessary code dynamically.
- **ClientBehaviourSystem**: The ClientBehaviourSystem is responsible for the client side of the connection. It handles incoming RPCs from the server and is influenced by the mode value of the DeterministicClient component. The ClientBehaviourSystem operates in a ClientSimulation world. Therefore, developer should create such a world with this flag at the beginning of the game. Upon creation, the system will create a DeterministicClientComponent and set its DeterministicClientWorkingMode value to None. From this point, the system has two primary functions. First, it listens for changes to the
DeterministicClientWorkingMode and acts accordingly. The second function is to listen for incoming RPCs from the server. Upon receiving a LoadGame RPC, it saves the settings that come with it to the DeterministicSettings component and changes the server mode to LoadingGame. Upon receiving a BroadcastTickDataToClients RPC, it saves the incoming inputs to the appropriate PlayerInputDataToUse components for each connection. Upon receiving a StartDeterministicGameSimulation  RPC, it creates the connection entities for each client.
- **ServerBehaviourSystem**: In contrast to the ClientBehaviourSystem, the ServerBehaviourSystem operates in a Server Simulation world, defined by the DOTS world flag. The ServerBehaviourSystem is responsible for the server side of the connection. It handles connections with clients, processes incoming RPCs from the clients, and is influenced by the mode value of the DeterministicServer component. First, when the mode is switched to ListenForConnections, the server saves detected connection requestt. Each connection is assigned a unique ID (its position in the list), which is later sent to clients to allow them to identify their connection when the server sends a package with inputs for each connection ID. Secondly, when the mode is set to RunDeterministicSimulation, no new connections are allowed. After sending a LoadGame RPC to every client, the server waits for a PlayerReady RPC from each client. Upon receiving such an RPC, the server adds it to the list under the corresponding connection ID. If not all RPCs have arrived, the server continues to wait. Once all PlayerReady RPCs have been received, the server sends a StartDeterministicGameSimulation RPC to the clients. From this point, it listens for incoming RPCs from clients containing their inputs. The server needs to store all incoming RPCs that contain client input information. For this purpose, it uses a dictionary called everyTickInputBuffer. The server checks if the incoming data from connections can be interpreted as a valid RPC message by inspecting the first byte of the information,which represents the RPC ID. Appropriate checks are in place to ensure that messages that cannot be parsed into an RPC structure are handled during the deserialisation method of the RPC. Valid RPCs are saved in the dictionary, where the keys represent the ticks (e.g., 1st, 2nd, etc.), and each tick has a NativeList of RPCs containing player inputs and IDs. Incoming RPCs are added to the appropriate list based on the tick they are intended for, with checks to ensure that the RPC hasn’t been received and added twice. Every time data is added to the dictionary, the server checks if all inputs for the given tick have been received. If so, the server sends an RPC containing inputs from every client for that tick to every client. This process continues as long as the server is in RunDeterministicSimulation mode. Additionally, the server maintains a counter to track the last
processed tick, which is later used for determinism validation.
- **System group definitions and system order**: To ensure a consistent simulation speed, such as 30 FPS or 60 FPS, the package uses a ComponentSystemGroup called DeterministicSimulationSystemGroup. This group controls the execution rate of game-critical systems, ensuring they run at a consistent pace regardless of frame time variability. A RateManager inside this group uses the ShouldGroupUpdate function to determine whether the systems should run in the current frame based on the desired frame rate and the time it took to process the last frame. The RateManager ensures systems run at the desired pace by checking deltaTime and managing timeLeftToSendNextTick. The MaxTicksPerFrame setting caps the number of catch-up frames to prevent performance issues. The package also verifies input availability from the server before processing the next frame to ensure accurate synchronization. 
Developer should add all systems that are affecting simulation state to this system group while avoiding ordering it as last since at the end the build in systems of hashing and input sending are placed.

### Client and Server Modes
In order to control the behaviour of server and client, approperiate singleton component exists (DeterministicServer and DeterministicClient) which contains an enum named DeterministicClientWorkingMode which represents the current working mode of the client or server. This state can be changed manually by developer. This component is created automatically by the package.

#### Server Modes
For the server within server world the following modes are available: 

- **None**: This is the initial mode. In this state no actions are being executed by the ServerBehaviourSystem.
- **Disconnect**: This is the mode that can be set by developer (usuall when certain conditions of a game end will be met). If this mode will be set, all clients will be disconnected from the server.
- **ListenForConnections**: This is the mode that can be set by developer (usually when the game is in a "lobby" scene). If this mode is set, the server will listen for connections which maximum number is restricted by allowedConnections parameter on DeterministicSettings. If a client will connect to the server it will receive all data about the game settings from the server.
- **RunDeterministicSimulation**: This is the mode that can be set by developer (usually when the game is started). If this mode will be set, the ServerBehaviour system will manage coordinating all the connections, their readiness to start the game and will collect all players inputs and hashes before sending them back.


#### Client Modes
For the client within client world the following modes are available: 

- **None**: This is the initial mode. In this state no actions are being executed by the ClientBehaviourSystem.
- **Disconnect**: This is the mode that can be set by developer. If this mode will be set, the client will be disconnected from the server.
- **Connect**: This is the mode that can be set by developer. When this mode wil be set, the client will attemp to connect to the server with values of address and port taken from the DeterministicSettings.
- **LoadingGame**: This mode is being set by the package when user when an LoadGame RPC will be send from the server. Developer should detect that this mode is on and then load all game scenes and other elements that should be active at the beginning of the game. When the game scene is loaded the mode should be changed to ClientReady.
- **ClientReady**: This is the mode that can be set by developer. If this mode will be set, client sends readiness message to the server and upon receiving message back about starting the game the simulation starts.
- **RunDeterministicSimulation**: This mode is being set by the package upon receiving confirmation from the server that all clients are ready to start the game. In this mode the client will run lockstep simulation.
- **Desync**: This mode is being set by the package when nondeterminism will be detected by the server (Desync RPC received by the client). It's important to perform some action when this mode is set because otherwise the game will just look frozen because the simulation is not running in this mode. In sample Pong game the message indicating this is shown.
- **GameFinished**: This is the mode that can be set by developer when the finish condition for the game is met. If this mode will be set, the client sends the final hash to the server and ends the game after confirmation that there was no nondeterminism issues in this last check.

## Determinism Validation and Debugging Tooling
The determinism validation is performed by hashing the state of the game and comparing the hashes between clients on each tick. Because hashing entire state of the game would be to costly performance wise the approach of marking specific components and entities for validation is used.
In order to include a component in the validation process, the developer needs to add the desired component types to the DeterministicComponentList, which uses a DynamicTypeList to store these types. This singleton component is automatically created and includes essential components like for example LocalTransform or GameSettings. Developers can query and add additional component types to this list at the beginning of the game. 
For entity validation, the CountEntityForWhitelistedDeterminismValidation component should be added to the prefabs of entities to be validated (present in DeterministicEntityAuthoring). This setup allows for either FullStateValidation or WhitelistedStateValidation, providing flexibility for both debugging and production use.

The game includes a determinism validation system with several validation options:

1. **None**: No validation is being performed.
2. **WhitelistHashPerTick**: Hash of marked components only on marked entities per each tick is being computed.
3. **FullStateHashPerTick**: Hash of all marked components per each tick is being computed.
4. **WhitelistHashPerSystem**: Hash of marked components only on marked entities per each system in the tick is being computed.
5. **FullStateHashPerSystem**: Hash of all marked components per each system in the tick is being computed.

In order to guarantee the deterministic order of hashing and later debugging files for now an approach is to add an DeterministicEntityID component to entities that are considered for validation, representing a unique, deterministic identifier (incremented each time). This approach has a drawback that developers must remember to add this component to each entity they create (e.g., every time a ball is spawned) and increment the counter deterministically. Incorrect sorting due to oversight can result in incorrect final hash values. Only entities with this component are considered for validation, making it crucial for developers to ensure this ID is added to any entity they want to include in validation. The current drawback of needing to ensure that DeterministicEntityID is added to an entity will be addressed in a future iteration by creating code that automatically adds and increments its value for every entity created or added to the scene. This would result in the need for only a deterministic order of entity creation, which, while still challenging, would be an improvement.

The validation system hashes game state components and sends hashes to the server for validation. If desync is detected, RPC to signal this event is send to clients to stop game execution and log files are generated for debugging.

### Log File Generation
When desync occurs, log files are generated to help identify the source of nondeterminism:

- **ServerInputRecording**: Records all player inputs and saves them in a readable format upon desync on server.
- **Client Logs**: Includes hashes for the tick during which desync occured and logs the state of the game on each client.
- **System Info**: Provides information about the client machine on each client.
- **Game Settings**: Records game settings used during the game on each client.

### Replay functionality
It's possible to replay the game in order to use different validation method to obtain informations about a particular desync. In order to use it GameSettings and ServerInputRecording files should be placed in main NondeterminismLogs folder and **isReplayFromFile** variable in DeterministicSettings should be set to true. It allows for local simulation based on save inputs and settings. In this mode the game is played locally and because of that the generated file will contain informations per each tick (not only the last one). 
The advantage of this approach is that the developer can choose a different validation method for the replay (e.g., per-system validation) and examine the logs of the simulation again. Since this is a local resimulation, it will only create log files on one device. Therefore, it is important to use a machine with the same specifications as the one where nondeterminism was detected, which can be specified based on the ClientSettings file. Nondeterminism issues may be stable, appearing on the same tick every time (e.g., an issue related to spawning the last ball on tick 1000 in the sample Pong game), or they may be unstable, appearing on seemingly random ticks. To address this, the replay file will not only hash the final state but also generate a file with hash information for every tick in the game. This allows for detecting the first variable that diverged between two runs.

## Package Usage With Other Game
In order to use deterministic lockstep netcode model implemented by this package you can follow the implementation of the sample Pong game.
The most important aspects are that in order for it to work, several steps need to be done.

- Create Client and Server worlds on which ClientBehaviourSystem and ServerBehaviourSystem will run.
- Upon creation of those worlds modify DeterministicSettings component as needed with game parameters.
- Implement an input struct following the implementation of the one for PongSample, the struct must for now be implemented within the package and needs to contain Serialize and Deserialize methods in the same way as the PongInput struct.
- Set all systems that are affecting simulation state to be part of DeterministicSimulationSystemGroup. This system group allows to run the systems inside with predefined in settings speed and also allows to point nondeterministic system in this group. If you have a systsem that is not affecting simulation state (for example connection handling that handles incoming information) it can be outside of the group.
- Choose validation method.
- When nondeterminism will be detected investigate generated files on both client and server and if those logs are not suffiecent to find the source of nondeterminism then use replay functionality and more detailed debug option.

The package will be a subject for modifications and improvements and this README may change or be updated.































## Future Improvement Plans

The following elements are considered as parts of "future work" and will be implemented in the future:

### Netcode Model Enhancements

- **Player Prediction and Rollback**
Integrate player prediction and rollback mechanisms to create a fully functional GGPO solution.

- **Dynamic Player Join**
Enable players to join an ongoing game by transferring the current game state to the new player and synchronizing them with the ongoing session.

- **Desynchronization Recovery**
Implement a system where the host can send the authoritative game state to a desynchronized client, allowing the game to continue the game and generating logs for later verification.

- **Custom Input Structures placed outside of the package**
Utilize code generation to allow users to implement their own input structures outside the package, providing greater separation.

### Determinism Validation and Debugging Tool Improvements

- **Automatic DeterministicID Assignment:**
  - Automate the assignment of `DeterministicID` to ensure all components are considered for validation, reducing the chance of human error.
  
- **Enhanced Logging:**
  - Implement code generation to log fields of user-created components, providing more detailed information for debugging.

- **Performance Optimization:**
  - Speed up the resimulation framework by skipping visual updates and iterating through the `DeterministicSystemGroup` as fast as possible.

- **Simultaneous Server Simulation:**
  - Allow the server to perform simulations simultaneously to continue from where it stopped, though this must ensure the exact state match across clients.

- **Automated File Comparison Tool:**
  - Develop a tool for automated file comparisons to quickly identify and summarize the exact data that diverged, streamlining the debugging process.

## Contributing

Any contributors are welcome to help improve this package. To contribute:

1. **Fork the Repository**: Create a personal fork of the repository on GitHub.
2. **Clone the Fork**: Clone your fork to your local machine.
3. **Create a Branch**: Create a new branch for your feature or bug fix.
4. **Make Changes**: Implement your changes in the new branch.
5. **Commit and Push**: Commit your changes and push the branch to your fork on GitHub.
6. **Create a Pull Request**: Open a pull request from your fork's branch to the main repository's main branch.

<!-- ## Authors
Michał Chrobot: Original author and main developer. -->

## License
The package is available under The MIT License. This means it is free for commercial and non-commercial use. Attribution is not required, but appreciated.
<!-- ## Acknowledgments -->
