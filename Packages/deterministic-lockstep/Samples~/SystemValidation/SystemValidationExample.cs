using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DeterministicLockstep.Samples
{
    /// <summary>
    /// Example MonoBehaviour demonstrating system validation.
    /// </summary>
    public class SystemValidationExample : MonoBehaviour
    {
        [Header("Validation Settings")]
        [SerializeField] private int numberOfRuns = 3;
        [SerializeField] private int numberOfTestEntities = 100;
        
        private World _world;
        
        private void Start()
        {
            _world = World.DefaultGameObjectInjectionWorld;
        }
        
        [ContextMenu("Validate Movement System (Snapshot)")]
        public void ValidateMovementSystemWithSnapshot()
        {
            Debug.Log("--- Validating ExampleMovementSystem with Snapshot ---");
            
            // First, create some test entities
            SetupTestEntities();
            
            // Validate using snapshot approach
            var result = SystemValidator.ValidateWithSnapshot<ExampleMovementSystem>(_world, numberOfRuns);
            SystemValidator.LogResult(result);
        }
        
        [ContextMenu("Validate Movement System (Setup Function)")]
        public void ValidateMovementSystemWithSetup()
        {
            Debug.Log("--- Validating ExampleMovementSystem with Setup Function ---");
            
            var result = SystemValidator.ValidateWithSetup<ExampleMovementSystem>(
                _world,
                setup: SetupTestEntitiesAction,
                cleanup: CleanupTestEntitiesAction,
                numberOfRuns: numberOfRuns
            );
            SystemValidator.LogResult(result);
        }
        
        [ContextMenu("Validate Nondeterministic System (Should Fail)")]
        public void ValidateNondeterministicSystem()
        {
            Debug.Log("--- Validating ExampleNondeterministicSystem (Expected to FAIL) ---");
            
            var result = SystemValidator.ValidateWithSetup<ExampleNondeterministicSystem>(
                _world,
                setup: SetupTestEntitiesAction,
                cleanup: CleanupTestEntitiesAction,
                numberOfRuns: numberOfRuns
            );
            SystemValidator.LogResult(result);
        }
        
        private void SetupTestEntities()
        {
            var em = _world.EntityManager;
            
            for (int i = 0; i < numberOfTestEntities; i++)
            {
                var entity = em.CreateEntity();
                
                em.AddComponentData(entity, new LocalTransform
                {
                    Position = new float3(i, 0, 0),
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                
                em.AddComponentData(entity, new Velocity
                {
                    Value = new float3(1, 0, 0)
                });
                
                em.AddComponentData(entity, new DeterministicEntityID { id = i });
            }
        }
        
        private void SetupTestEntitiesAction(EntityManager em)
        {
            for (int i = 0; i < numberOfTestEntities; i++)
            {
                var entity = em.CreateEntity();
                
                em.AddComponentData(entity, new LocalTransform
                {
                    Position = new float3(i, 0, 0),
                    Rotation = quaternion.identity,
                    Scale = 1f
                });
                
                em.AddComponentData(entity, new Velocity
                {
                    Value = new float3(1, 0, 0)
                });
                
                em.AddComponentData(entity, new DeterministicEntityID { id = i });
            }
        }
        
        private void CleanupTestEntitiesAction(EntityManager em)
        {
            // Destroy all entities with Velocity component
            var query = em.CreateEntityQuery(typeof(Velocity));
            em.DestroyEntity(query);
        }
    }
}

