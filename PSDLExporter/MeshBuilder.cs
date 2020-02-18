using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using ICities;
using UnityEngine;
using PSDL;
using PSDL.Elements;

namespace PSDLExporter
{
    class MeshBuilder
    {
        NetManager netMan = Singleton<NetManager>.instance;

        public SortedDictionary<ushort, CSIntersection> CreateAllIntersections()
        {
            SortedDictionary<ushort, CSIntersection> intersections = new SortedDictionary<ushort, CSIntersection>();

            ushort intersectionNo = 0;
            for (ushort i = 0; i < MeshExporter.NETNODE_COUNT; i++)
            {
                if ((netMan.m_nodes.m_buffer[i].m_flags & NetNode.Flags.Created) != NetNode.Flags.None
                    && /*.CountSegments()*/ RoadUtils.GetAllAdjacentSegments(netMan.m_nodes.m_buffer[i], NetInfo.LaneType.Vehicle).Count > 2)
                {

                    CSIntersection intersection = new CSIntersection(i, intersectionNo);
                    Debug.Log("Calculate adjacent segments...");
                    intersection.CalculateAdjacentSegments();
                    Debug.Log("Calculate connection points...");
                    intersection.CalculateConnectionPoints();
                    Debug.Log("Build intersection room...");
                    intersection.BuildIntersectionRoom();

                    intersections.Add(i, intersection);
                    intersectionNo++;
                }
            }
            return intersections;
        }

        // for debugging
        static public string DrawPerimeter(List<PerimeterPoint> pps)
        {
            string result = "<circle cx=\"" + pps[0].Vertex.x + "\" cy=\"" + pps[0].Vertex.z + "\" r=\"4\"/>" + Environment.NewLine;

            // show second vertex to indicate direction
            result += "<circle cx=\"" + pps[1].Vertex.x + "\" cy=\"" + pps[1].Vertex.z + "\" r=\"4\" fill=\"red\" />" + Environment.NewLine;

            result += "<polygon points=\"";
            foreach (PerimeterPoint pp in pps)
            {
                result += pp.Vertex.x + "," + pp.Vertex.z + " ";
            }
            result.Remove(result.Length - 1); // remove last space

            result += "\" style=\"fill:lightgrey;stroke:red;stroke-width:1\" />" + Environment.NewLine;
            return result;
        }
    }
}
