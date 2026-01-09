using Unity.Networking.Transport;

namespace DeterministicLockstep
{
    /// <summary>
    /// Interface for player input data that can be serialized and used in deterministic simulation.
    /// Implement this interface to define custom input structures for your game.
    /// </summary>
    public interface IPlayerInputs
    {
        /// <summary>
        /// Serialize the input data to a DataStreamWriter for network transmission.
        /// </summary>
        /// <param name="writer">The writer to serialize to.</param>
        void SerializeInputs(ref DataStreamWriter writer);
        
        /// <summary>
        /// Deserialize the input data from a DataStreamReader.
        /// </summary>
        /// <param name="reader">The reader to deserialize from.</param>
        void DeserializeInputs(ref DataStreamReader reader);
    }
}

