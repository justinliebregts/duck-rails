using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Liv.Lck
{
    [Serializable]
    public struct CameraResolutionDescriptor
    {
        public uint Width;
        public uint Height;

        public CameraResolutionDescriptor(uint width = 512, uint height = 512)
        {
            this.Width = width;
            this.Height = height;
        }
        
        public bool IsValid()
        {
            return Width > 0 && Height > 0;
        }

        /// <summary>
        /// Gets a <see cref="CameraResolutionDescriptor"/> of the same dimensions in the given <see cref="orientation"/>
        /// </summary>
        /// <param name="orientation">
        /// The <see cref="LckCameraOrientation"/> type to get the <see cref="CameraResolutionDescriptor"/> in
        /// </param>
        /// <returns>
        /// A <see cref="CameraResolutionDescriptor"/> with matching dimensions, but in the given
        /// <see cref="orientation"/>
        /// </returns>
        public CameraResolutionDescriptor GetResolutionInOrientation(LckCameraOrientation orientation)
        {
            return orientation switch
            {
                LckCameraOrientation.Landscape => new CameraResolutionDescriptor(
                    Math.Max(Width, Height),
                    Math.Min(Width, Height)),
                LckCameraOrientation.Portrait => new CameraResolutionDescriptor(
                    Math.Min(Width, Height),
                    Math.Max(Width, Height)),
                _ => this
            };
        }
    }
}