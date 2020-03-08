using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PSDLExporter
{
    class UniversalProperties
    {
        public static readonly float CONVERSION_SCALE = 1.0f;
        public static readonly float SIDEWALK_HEIGHT = 0.15f;
        public static readonly float INTERSECTION_OFFSET = 4.0f;

        // temporary until we can retrieve individual values from prefab meshes
        //public static readonly float SIDEWALK_OFFSET = 0.15f; // relative offset
        //public static readonly float HALFROAD_WIDTH = 8.0f;
    }
}
