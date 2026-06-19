using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxon;
// Collection of Type defittiions of VxU Development
// These should only be types that apply to Voxon's technology working within Unity

namespace Voxon
{
    [System.Serializable]
    public struct point2DInt

    {
        public int x;
        public int y;

        public point2DInt(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    };

}
