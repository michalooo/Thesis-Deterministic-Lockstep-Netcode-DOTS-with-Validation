using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(UserSystemGroup))]
public partial class GameLogicClientSystem : SystemBase
{
    private Entity _settings;
    protected override void OnCreate()
    {
        RequireForUpdate<DeterministicSettings>();
    }

    protected override void OnStartRunning()
    {
        _settings = SystemAPI.GetSingletonEntity<DeterministicSettings>();
    }

    protected override void OnUpdate()
    {
        if (SceneManager.GetActiveScene().name == "CapsulesGame" && Input.GetKey(KeyCode.C) &&
            World.Name == "ClientWorld2") // Simulation of disconnection
        {
            SystemAPI.SetComponentEnabled<DeterministicClientDisconnect>(_settings, true);
        
            SceneManager.LoadScene("CapsulesGame");
        }

        if (SceneManager.GetActiveScene().name != "CapsulesGame" &&
            SystemAPI.IsComponentEnabled<DeterministicClientSendData>(_settings))
        {
            SceneManager.LoadScene("CapsulesGame");
        }
    }
}