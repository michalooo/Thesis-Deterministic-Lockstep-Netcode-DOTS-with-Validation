using Unity.Entities;


[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class InputGatherSystemGroup : ComponentSystemGroup { }

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateAfter(typeof(InputGatherSystemGroup))]
public partial class MovementSystemGroup : ComponentSystemGroup { }

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
[UpdateAfter(typeof(MovementSystemGroup))]
public partial class DeterminismSystemGroup : ComponentSystemGroup //IRateManager How to implement?
{
    // public bool ShouldGroupUpdate(ComponentSystemGroup group)
    // {
    //     return group. == SimulationSystemGroup;
    // }
    //
    // public float Timestep { get; set; }
}

[UpdateBefore(typeof(InputGatherSystemGroup))]
public partial class ConnectionHandleSystemGroup : ComponentSystemGroup { }

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
