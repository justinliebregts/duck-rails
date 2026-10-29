namespace Game.Audio
{
    /// <summary>A generation-checked lease; stale handles cannot stop a reused source.</summary>
    public readonly struct AudioHandle
    {
        readonly AudioManager manager;
        readonly int slot;
        readonly ulong generation;
        internal AudioHandle(AudioManager manager, int slot, ulong generation)
        { this.manager = manager; this.slot = slot; this.generation = generation; }
        public bool IsPlaying => manager != null && manager.IsAlive(slot, generation);
        public void Stop(float fadeOut = 0f)
        {
            if (manager != null) manager.Stop(slot, generation, fadeOut);
        }
    }
}
