using UnityEngine;

namespace Game.Audio
{
    public sealed class MinMaxRangeAttribute : PropertyAttribute
    {
        public readonly float Min;
        public readonly float Max;
        public MinMaxRangeAttribute(float min, float max) { Min = min; Max = max; }
    }
}
