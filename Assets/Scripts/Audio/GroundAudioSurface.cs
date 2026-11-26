using UnityEngine;

namespace Game.Audio
{
    // Row spans describe the visible material over a single continuous ground collider.
    public sealed class GroundAudioSurface : AudioSurface
    {
        public Vector2 origin;
        public int samplesPerMetre, width;
        public SurfaceType[] palette;
        public int[] rowOffsets, spans;
        public override SurfaceType AtPoint(Vector3 point)
        {
            var p=transform.InverseTransformPoint(point);
            int x=Mathf.FloorToInt((p.x-origin.x)*samplesPerMetre);
            int z=Mathf.FloorToInt((p.z-origin.y)*samplesPerMetre);
            if(x<0 || x>=width || z<0 || rowOffsets==null || z>=rowOffsets.Length-1) return type;
            int material=0;
            for(int i=rowOffsets[z];i<rowOffsets[z+1];i++)
            {
                if((spans[i]>>8)>x) break;
                material=spans[i]&255;
            }
            return palette[material];
        }
    }
}
