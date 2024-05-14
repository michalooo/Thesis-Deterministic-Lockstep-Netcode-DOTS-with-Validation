// using DeterministicLockstep;
// using Unity.Entities;
// using UnityEngine;
//
// namespace PongGame
// {
//     /// <summary>
//     /// Behaviour to add the DeterministicSimulation component to an entity.
//     /// </summary>
//     public class DeterministicEntityAuthoring : MonoBehaviour
//     {
//         //public bool UseInDeterministicFastHashCalculation;
//         // public bool UseAutoCommandTarget;
//
//         class Baker : Baker<DeterministicEntityAuthoring>
//         {
//             public override void Bake(DeterministicEntityAuthoring authoring)
//             {
//                 var entity = GetEntity(TransformUsageFlags.Dynamic);
//                 AddComponent<DeterministicSimulation>(entity);
//                 //if(authoring.UseInDeterministicFastHashCalculation) AddComponent<UseInDeterministicFastHashCalculation>(entity);
//             }
//         }
//     }
// }