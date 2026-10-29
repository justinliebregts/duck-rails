namespace Liv.Lck.Audio
{
    internal abstract class LckAudioSystemOptions
    {
        /// <summary>
        /// Whether LCK should capture audio from the audio system described by the <see cref="LckAudioSystemOptions"/>
        /// </summary>
        public bool ShouldCapture { get; set; }
        
        public override string ToString()
        {
            return string.Join(", ", GetToStringDisplayFields());
        }

        protected virtual string[] GetToStringDisplayFields()
        {
            return new string[]
            {
                $"{nameof(ShouldCapture)}={ShouldCapture}"
            };
        }
    }
}
