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
        // protected override void OnCreate()
        // {
        //     RateManager = new DeterministicFixedStepRateManager();
        // }
        //
        // public struct DeterministicFixedStepRateManager : IRateManager
        // {
        //     public bool ShouldGroupUpdate(ComponentSystemGroup group)
        //     {
        //         ref var networkTime = ref SystemAPI.GetSingletonRW<NetworkTime>().ValueRW;
        //         if (networkTime.SimulationTime < networkTime.RealTime)
        //         {
        //             networkTime.SimulationTime += fixedStepDeltaTime;
        //             networkTime.NumTimesTickedThisFrame++;
        //             group.World.PushTime(new TimeData());
        //             return true;
        //         }
        //
        //         for (int i = 0; i < networkTime.NumTimesTickedThisFrame; i++)
        //             group.World.PopTime();
        //         networkTime.NumTimesTickedThisFrame = 0;
        //         return false;
        //     }
        //
        //     public float Timestep { get; set; }
        // }
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