using Unity.Entities;

public partial class ConnectionHandleSystemGroup : ComponentSystemGroup { }

[UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderFirst = true)] 
public partial class InputGatherSystemGroup : ComponentSystemGroup { }

[UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
public partial class MovementSystemGroup : ComponentSystemGroup { }

[UpdateAfter(typeof(ConnectionHandleSystemGroup))]
public partial class DeterministicSimulationSystemGroup : ComponentSystemGroup 
{ }

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






public partial struct ManualSystemTicking : ISystem
{

    public void OnCreate(ref SystemState state)
    {
        var world = World.DefaultGameObjectInjectionWorld;

        world.GetOrCreateSystem<InputGatherSystemGroup>();
        world.GetOrCreateSystem<MovementSystemGroup>();
        world.GetOrCreateSystem<DeterministicSimulationSystemGroup>(); 
        world.GetOrCreateSystem<ConnectionHandleSystemGroup>();

    }
}

// public partial struct ManualSystemTicking : ISystem
// {
//     private InputGatherSystemGroup inputSystemGroup;
//     private MovementSystemGroup movementSystemGroup;
//     private DeterminismSystemGroup determinismSystemGroup;
//
//     public void OnCreate(ref SystemState state)
//     {
//         inputSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<InputGatherSystemGroup>();
//         movementSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<MovementSystemGroup>();
//         determinismSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DeterminismSystemGroup>();
//     }
//
//     public void OnUpdate(ref SystemState state)
//     {
//         for(int i=0; i<100; i++) // simulate a 100 frames forward
//         {
//             inputSystemGroup.Update(); 
//             movementSystemGroup.Update();
//             determinismSystemGroup.Update();
//         }
//     }
// }
