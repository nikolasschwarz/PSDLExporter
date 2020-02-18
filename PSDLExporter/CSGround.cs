using PSDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PSDLExporter
{
    // used to generate ground. 
    class CSGround
    {
        private List<Vertex> perimeter = new List<Vertex>();
        private SortedDictionary<uint, uint> identificationSegments; // pairs of segments that border the ground at each adjacent intersection
    }
}
