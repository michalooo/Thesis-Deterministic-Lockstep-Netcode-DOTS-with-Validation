using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    public enum DeterminismHashCalculationOption
    {
        PerSystem,
        PerTick,
        None
    }
    
    public struct DeterministicSettings : IComponentData
    {
        public int ticksAhead;
        public int allowedConnectionsPerGame;
        public int simulationTickRate;
        public DeterminismHashCalculationOption hashCalculationOption;

        public bool isInGame;
        public bool isSimulationCatchingUpOrRollingBack; // need to be set
        
        public FixedString32Bytes _serverAddress { get; set; }
        public int _serverPort { get; set; }
    }
    
    public class DeterministicSettingsAuthoring : MonoBehaviour // Will it work both on Client and Server?
    {
        [Header("Game simulation params")]
        public int simulationTickRate = 30;
        public int allowedConnectionsPerGame = 16;
        public int ticksAhead = 4; // Mathf.CeilToInt(0.15f * tickRate); (delay of 0.15s)
        public int serverPort = 7979;
        public string serverAddress = "127.0.0.1";
        public DeterminismHashCalculationOption hashCalculationOption = DeterminismHashCalculationOption.None;

        private bool isInGame = false;
        private bool isSimulationCatchingUpOrRollingBack = false;
        
        class SettingBaker : Baker<DeterministicSettingsAuthoring>
        {
            public override void Bake(DeterministicSettingsAuthoring authoring)
            {
                var component = default(DeterministicSettings);
                component.ticksAhead = authoring.ticksAhead;
                component.allowedConnectionsPerGame = authoring.allowedConnectionsPerGame;
                component.simulationTickRate = authoring.simulationTickRate;
                component.isInGame = authoring.isInGame;
                component.isSimulationCatchingUpOrRollingBack = authoring.isSimulationCatchingUpOrRollingBack;
                component.hashCalculationOption = authoring.hashCalculationOption;
                component._serverPort = authoring.serverPort;
                component._serverAddress = authoring.serverAddress;
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, component);
            }
        }
        
    }
}