using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    public struct DeterministicServerListen : IComponentData, IEnableableComponent { }
    public struct DeterministicServerRunSimulation : IComponentData, IEnableableComponent { }
    public struct DeterministicClientConnect : IComponentData, IEnableableComponent { }
    public struct DeterministicClientDisconnect : IComponentData, IEnableableComponent { }
    public struct DeterministicClientSendData : IComponentData, IEnableableComponent { }
    
    public struct DeterministicSettings : IComponentData
    {
        public int ticksAhead;
        public ushort allowedConnectionsPerGame;
        public ushort simulationTickRate;
        public int serverPort;

        public bool isInGame;
        public bool isSimulationCatchingUpOrRollingBack;

        public int packetDelayMs;
        public int packetJitterMs;
        public int packetDropInterval;
        public int packetDropPercentage;
        public int packetDuplicationPercentage;
    }
    
    public class DeterministicSettingsAuthoring : MonoBehaviour // Will it work both on Client and Server?
    {
        [Header("Game simulation params")]
        public ushort simulationTickRate = 30;
        public ushort allowedConnectionsPerGame = 16;
        public int ticksAhead = 4; // Mathf.CeilToInt(0.15f * tickRate); (delay of 0.15s)
        public int serverPort = 7979;

        private bool isInGame = false;
        private bool isSimulationCatchingUpOrRollingBack = false;
        
        [Header("Simulation Pipeline params")]
        [Tooltip("Fixed delay in milliseconds to apply to all packets which pass through.")]
        public int packetDelayMs = 0;
        
        [Tooltip(
            "Variance of the delay that gets added to all packets that pass through. For example, setting this value to 5 will result in the delay being a random value within 5 milliseconds of the value set with PacketDelayMs.")]
        public int packetJitterMs = 0;
        
        [Tooltip(
            "Fixed interval to drop packets on. This is most suitable for tests where predictable behaviour is desired, as every X-th packet will be dropped. For example, if the value is 5 every fifth packet is dropped.")]
        public int packetDropInterval = 0;
        
        [Tooltip("Percentage of packets that will be dropped.")]
        public int packetDropPercentage = 0;
        
        [Tooltip(
            "Percentage of packets that will be duplicated. Packets are duplicated at most once and will not be duplicated if they were first deemed to be dropped.")]
        public int packetDuplicationPercentage = 0;
        
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
                component.packetDelayMs = authoring.packetDelayMs;
                component.packetJitterMs = authoring.packetJitterMs;
                component.packetDropInterval = authoring.packetDropInterval;
                component.packetDropPercentage = authoring.packetDropPercentage;
                component.packetDuplicationPercentage = authoring.packetDuplicationPercentage;
                component.serverPort = authoring.serverPort;
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, component);
            }
        }
        
    }
}