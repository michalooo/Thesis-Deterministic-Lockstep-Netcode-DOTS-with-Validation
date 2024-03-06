using Unity.Entities;

// This script should be responsible for executing lockstep logic
// Do I need it? Because I can just use the StateSync system to do the same thing so basically first I'm doing
     // 1) InputHandling gathers Input and sends it via transport
     // 2) StateSync waits for inputs from all other players and then updates the state + saves it for debugging
// How should then the logic for ClientBehaviour and ServerBehaviour look like?
// How to propagate the frameRate to all clients so we are in sync?
public partial class LockstepSystem : SystemBase {
    private int currentFrame = 0;
    private const int frameRate = 30; // Frames per second

    protected override void OnUpdate() {
        // float deltaTime = Time.DeltaTime;
        //
        // if (currentFrame % (int)(1 / deltaTime) == 0) {
        //     // Execute lockstep logic every frameRate frames
        //     LockstepLogic();
        // }
        //
        // currentFrame++;
    }

    private void LockstepLogic() {
        // Example: Execute lockstep logic, including input handling and state synchronization
        // Example: Input handling logic
        // EntityManager.ForEach((Entity entity, ref PlayerInputComponent input) => {
        //     // Process player input for this frame
        // }).Run();
        //
        // // Example: State synchronization logic
        // EntityManager.ForEach((Entity entity, ref PositionComponent position) => {
        //     // Synchronize entity positions with other clients
        // }).Run();

        // Other lockstep logic as needed
    }
    
//     // Client
//     while (true) {
//         // Wait for the server to send the next game state update over TCP
//         GameState gameState = ReceiveGameStateFromServer();
//     
//         // Process the received game state update
//         ProcessGameStateUpdate(gameState);
//     
//         // Send acknowledgment to the server
//         SendAcknowledgmentToServer();
//     }
//
// // Server
//     while (true) {
//         // Collect player inputs from all clients over TCP
//         CollectPlayerInputsFromClients();
//     
//         // Simulate the game state based on collected inputs
//         SimulateGameState();
//     
//         // Send the updated game state to all clients over TCP
//         SendGameStateToClients();
//     
//         // Wait for acknowledgments from all clients
//         WaitForAcknowledgmentsFromClients();
//     }
}