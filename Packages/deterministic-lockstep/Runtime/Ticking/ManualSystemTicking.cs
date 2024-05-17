using Unity.Collections;
using Unity.Core;
using Unity.Entities;

namespace DeterministicLockstep
{
    /// <summary>
    /// System group that contains all of user defined systems which are not affecting state of the game.
    /// </summary>
    [UpdateAfter(typeof(ConnectionHandleSystemGroup))]
    public partial class UserSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// System group that contains connection handle systems.
    /// </summary>
    public partial class ConnectionHandleSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// System group that contains deterministic simulation systems. Systems that are using it are PlayerUpdateSystem, DeterminismSystemCheck, and PlayerInputGatherAndSendSystem.
    /// </summary>
    [UpdateAfter(typeof(UserSystemGroup))]
    public partial class DeterministicSimulationSystemGroup : ComponentSystemGroup
    {
        protected override void OnCreate()
        {
            RateManager = new DeterministicFixedStepRateManager();
            EntityManager.CreateSingleton(new DeterministicTime
            {
                entries = new NativeQueue<RpcPlayersDataUpdate>(Allocator.Persistent)
            });
            // move to deterministic system group 
        }

        protected override void OnDestroy()
        {
            ref var value = ref SystemAPI.GetSingletonRW<DeterministicTime>().ValueRW;
            value.entries.Dispose();
        }

        protected override void OnUpdate()
        {
            if (RateManager.ShouldGroupUpdate(this))
            {
                base.OnUpdate();
            }
        }

        public struct DeterministicFixedStepRateManager : IRateManager
        {
            private EntityQuery _deterministicTimeQuery;
            private EntityQuery _storedTicksAheadQuery;

            public DeterministicFixedStepRateManager(ComponentSystemGroup group) : this()
            {
                _deterministicTimeQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicTime));
                ;
            }

            public bool ShouldGroupUpdate(ComponentSystemGroup group)
            {
                ref var deterministicTime = ref _deterministicTimeQuery.GetSingletonRW<DeterministicTime>().ValueRW;

                if (deterministicTime.NumTimesTickedThisFrame < 10)
                {
                    var hasInputsForThisTick = deterministicTime.entries.Count > deterministicTime.tickAheadValue;
                    if (hasInputsForThisTick)
                    {
                        if (deterministicTime.DeterministicLockstepElapsedTime <
                            group.World.Time
                                .ElapsedTime) // if it has inputs for the frame. automatically handles question of how many ticks for now
                        {
                            // If we found on we can increment both ticks (current presentation tick and tick we will send to server)
                            tickRateInfo.ValueRW.currentClientTickToSend++;
                            tickRateInfo.ValueRW.currentSimulationTick++;

                            // first update the component data before we will remove the info from the array to make space for more
                            UpdateComponentsData(storedTicksAhead.ValueRW.entries
                                .Dequeue()); // it will remove it so no reason for dispose method for arrays?

                            EntityManager.SetComponentEnabled<PlayerInputDataToSend>(connectionEntity, true);
                            EntityManager.SetComponentEnabled<PlayerInputDataToUse>(connectionEntity, true);
                            
                            
                            
                            //#TODO cap this to 10 times per render frame
                            const float deltaTime = 1.0f / 60.0f;
                            deterministicTime.DeterministicLockstepElapsedTime += deltaTime;
                            deterministicTime.NumTimesTickedThisFrame++;
                            group.World.PushTime(
                                new TimeData(deterministicTime.DeterministicLockstepElapsedTime, deltaTime));
                            return true;
                        }
                    }
                }

                //check if we already pushed time this frame
                for (int i = 0;
                     i < deterministicTime.NumTimesTickedThisFrame;
                     i++)
                    group.World.PopTime();
                deterministicTime.NumTimesTickedThisFrame = 0;
                return false;
            }

            public float Timestep { get; set; }
        }
    }

    public struct DeterministicTime : IComponentData
    {
        public double DeterministicLockstepElapsedTime; //seconds same as ElapsedTime
        public float RealTime;
        public int NumTimesTickedThisFrame;

        public int tickRate;
        public int tickAheadValue;

        public float delayTime;
        public int currentSimulationTick; // Received simulation tick from the server
        public int currentClientTickToSend; // We are sending input for the tick in the future
        public ulong hashForTheTick;

        public NativeQueue<RpcPlayersDataUpdate> entries; // be sure that there is no memory leak
    }


    /// <summary>
    /// System group that is used for any game logic stuff (can be ticked when rolling back or catching up).
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderFirst = true)]
    public partial class GameStateUpdateSystemGroup : ComponentSystemGroup
    {
    }

    public partial struct ManualSystemTicking : ISystem // should run once right?
    {
        public void OnCreate(ref SystemState state)
        {
            var world = World.DefaultGameObjectInjectionWorld;

            var deterministicSimulationSystemGroup = world.GetOrCreateSystem<DeterministicSimulationSystemGroup>();
            var connectionHandleSystemGroup = world.GetOrCreateSystem<ConnectionHandleSystemGroup>();
            var gameStateUpdateSystemGroup = world.GetOrCreateSystem<GameStateUpdateSystemGroup>();
            var userSystemGroup = world.GetOrCreateSystem<UserSystemGroup>();
        }
    }


/*public partial class DeterministicSystemGroup : ComponentSystemGroup
{
    protected override void OnCreate()
    {
        RateManager = new DeterministicFixedStepRateManager();
    }

    public struct DeterministicFixedStepRateManager : IRateManager
    {
        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            ref var networkTime = ref SystemAPI.GetSingletonRW<NetworkTime>().ValueRW;
            if (networkTime.SimulationTime < networkTime.RealTime)
            {
                networkTime.SimulationTime += fixedStepDeltaTime;
                networkTime.NumTimesTickedThisFrame++;
                group.World.PushTime(new TimeData());
                return true;
            }

            for (int i = 0; i < networkTime.NumTimesTickedThisFrame; i++)
                group.World.PopTime();
            networkTime.NumTimesTickedThisFrame = 0;
            return false;
        }

        public float Timestep { get; set; }
    }
}*/
}