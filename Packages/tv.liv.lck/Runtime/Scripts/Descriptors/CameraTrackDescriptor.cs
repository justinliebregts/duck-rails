using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Liv.Lck
{
    [Serializable]
    public struct CameraTrackDescriptor
    {
        public CameraResolutionDescriptor CameraResolutionDescriptor;
        public uint Bitrate;
        public uint Framerate;
        public uint AudioBitrate;

        public CameraTrackDescriptor(CameraResolutionDescriptor cameraResolutionDescriptor, uint bitrate = 5 << 20, uint framerate = 30, uint audioBitrate = 192000)
        {
            CameraResolutionDescriptor = cameraResolutionDescriptor;
            this.Bitrate = bitrate;
            this.Framerate = framerate;
            this.AudioBitrate = audioBitrate;
        }
    }
}
