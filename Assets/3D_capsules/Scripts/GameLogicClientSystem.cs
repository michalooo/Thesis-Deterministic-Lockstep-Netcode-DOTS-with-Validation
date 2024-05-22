// using DeterministicLockstep;
// using Unity.Entities;
// using UnityEngine;
// using UnityEngine.SceneManagement;
//
// namespace CapsulesGame
// {
//     [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
//     [UpdateInGroup(typeof(UserSystemGroup))]
//     public partial class GameLogicClientSystem : SystemBase
//     {
//         private DeterministicClientComponent _client;
//
//         protected override void OnCreate()
//         {
//             RequireForUpdate<DeterministicClientComponent>();
//         }
//
//         protected override void OnStartRunning()
//         {
//             _client = SystemAPI.GetSingleton<DeterministicClientComponent>();
//         }
//
//         protected override void OnUpdate()
//         {
//             if (SceneManager.GetActiveScene().name == "CapsulesGame" && Input.GetKey(KeyCode.C) &&
//                 World.Name == "ClientWorld2") // Simulation of disconnection
//             {
//                 _client.deterministicClientWorkingMode = DeterministicClientWorkingMode.Disconnect;
//
//                 SceneManager.LoadScene("CapsulesGame");
//             }
//
//             if (SceneManager.GetActiveScene().name == "CapsulesLoading" &&
//                 _client.deterministicClientWorkingMode == DeterministicClientWorkingMode.Disconnect)
//             {
//                 SceneManager.LoadScene("CapsulesGame");
//             }
//         }
//     }
// }