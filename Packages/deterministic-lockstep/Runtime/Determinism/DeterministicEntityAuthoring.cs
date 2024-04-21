using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Tag component to mark an entity as part of the deterministic simulation checks.
    /// </summary>
    public struct DeterministicSimulation : IComponentData
    {
    }
    
//public struct UseInDeterministicFastHashCalculation : IComponentData //comment for now. Maybe in performance, environment flag
//{ // full hash per system for user experience
//}
}