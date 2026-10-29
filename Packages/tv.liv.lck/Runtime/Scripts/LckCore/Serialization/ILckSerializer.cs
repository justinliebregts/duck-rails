namespace Liv.Lck.Core.Serialization
{
    /// <summary>
    /// Interface providing serialization/deserialization capability
    /// </summary>
    public interface ILckSerializer
    {
        /// <summary>
        /// The <see cref="SerializationType"/> used by the <see cref="ILckSerializer"/>
        /// </summary>
        SerializationType SerializationType { get; }
        
        /// <summary>
        /// Serialize the given <see cref="data"/> into a <see cref="byte"/> array
        /// </summary>
        /// <param name="data">The data to serialize</param>
        /// <returns>A <see cref="byte"/> array of serialized data</returns>
        byte[] Serialize(object data);
        
        /// <summary>
        /// Deserialize the given <see cref="byte"/> array into an object of type <see cref="T"/>
        /// </summary>
        /// <param name="data">The data to deserialize</param>
        /// <typeparam name="T">The type to deserialize into</typeparam>
        /// <returns>
        /// An object of type <see cref="T"/> if deserialization was successful, or <c>null</c> otherwise
        /// </returns>
        T Deserialize<T>(byte[] data);
    }
}

