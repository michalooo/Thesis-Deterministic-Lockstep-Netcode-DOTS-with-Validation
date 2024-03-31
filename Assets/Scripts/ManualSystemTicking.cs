using Unity.Entities;

public partial class ConnectionHandleSystemGroup : ComponentSystemGroup { }

[UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderFirst = true)] 
public partial class InputGatherSystemGroup : ComponentSystemGroup { }

[UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
public partial class MovementSystemGroup : ComponentSystemGroup { }

[UpdateAfter(typeof(ConnectionHandleSystemGroup))]
public partial class DeterministicSimulationSystemGroup : ComponentSystemGroup //IRateManager How to implement?
{
    // public bool ShouldGroupUpdate(ComponentSystemGroup group)
    // {
    //     return group. == SimulationSystemGroup;
    // }
    //
    // public float Timestep { get; set; }
}







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
