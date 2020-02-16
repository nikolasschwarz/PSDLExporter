using ColossalFramework;
using ColossalFramework.UI;
using PSDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PSDLExporter
{
    class MeshExporter
    {
        public static readonly uint NETNODE_COUNT = 32768u;
        public static readonly uint NETSEGMENT_COUNT = 36864u;

        public static bool IsPlausible(float x0, float y0, float x1, float y1)
        {
            if ((x1 == x0) && (y1 == y0)) return false;
            return true;
            // calculate distance

            float dist = Mathf.Sqrt((x0 - x1) * (x0 - x1) + (y0 - y1) * (y0 - y1));

            return dist < 200.0f;
        }

        private bool UpdateSegment(ref ushort start, ref ushort end, ushort updateNode)
        {
            if (start == 0)
            {
                start = updateNode;
                if (end != 0) return true; // done
                return false;
            }

            end = updateNode;
            return true; // done

        }

        private void FixSegment(ushort id, ref ushort start, ref ushort end)
        {
            NetManager nm = Singleton<NetManager>.instance;
            bool done = false;

            for (ushort i = 0; i < NETNODE_COUNT; i++)
            {
                if ((nm.m_nodes.m_buffer[i].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
                {
                    //continue;
                }


                if (nm.m_nodes.m_buffer[i].m_segment0 == id)
                {
                    done = UpdateSegment(ref start, ref end, i);
                }
                else if (nm.m_nodes.m_buffer[i].m_segment1 == id)
                {
                    done = UpdateSegment(ref start, ref end, i);
                }
                else if (nm.m_nodes.m_buffer[i].m_segment2 == id)
                {
                    done = UpdateSegment(ref start, ref end, i);
                }
                else if (nm.m_nodes.m_buffer[i].m_segment3 == id)
                {
                    done = UpdateSegment(ref start, ref end, i);
                }
                else if (nm.m_nodes.m_buffer[i].m_segment4 == id)
                {
                    done = UpdateSegment(ref start, ref end, i);
                }
                else if (nm.m_nodes.m_buffer[i].m_segment5 == id)
                {
                    done = UpdateSegment(ref start, ref end, i);
                }
                else if (nm.m_nodes.m_buffer[i].m_segment6 == id)
                {
                    done = UpdateSegment(ref start, ref end, i);
                }
                else if (nm.m_nodes.m_buffer[i].m_segment7 == id)
                {
                    done = UpdateSegment(ref start, ref end, i);
                }

                if (done) return;
            }
        }

        private int FindClosestNode(Vector3 pos, List<uint> netNodes)
        {
            int closestIndex = -1;
            float distance = float.PositiveInfinity;
            NetManager nm = Singleton<NetManager>.instance;

            for (int i = 0; i < netNodes.Count; i++)
            {
                //float tempDist = Vector3.Distance(, nm.m_nodes.m_buffer[netNodes[i]].m_position);
                Vector3 otherPos = nm.m_nodes.m_buffer[netNodes[i]].m_position;
                float tempDist = Mathf.Sqrt((pos.x - otherPos.x) * (pos.x - otherPos.x) + (pos.z - otherPos.z) * (pos.z - otherPos.z));
                if (tempDist < distance && tempDist != 0.0)
                {
                    distance = tempDist;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        bool IsValid(NetSegment segment)
        {
            NetManager nm = Singleton<NetManager>.instance;

            if ((segment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
            {
                return false;
            }

            NetNode start = nm.m_nodes.m_buffer[segment.m_startNode];
            NetNode end = nm.m_nodes.m_buffer[segment.m_endNode];

            if ((start.m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
            {
                return false;
            }

            if ((end.m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
            {
                return false;
            }

            // for now ignore dead ends
            /*if (start.CountSegments() < 2) return false;
            if (end.CountSegments() < 2) return false;*/


            Vector3 pos0 = start.m_position;
            Vector3 pos1 = end.m_position;

            return IsPlausible(pos0.x, pos0.z, pos1.x, pos1.z);
        }

        ushort[] CountNodeReferences()
        {
            NetManager nm = Singleton<NetManager>.instance;
            ushort[] nodeReferences = new ushort[NETNODE_COUNT];
            nodeReferences.Initialize();

            for (ushort i = 0; i < NETSEGMENT_COUNT; i++)
            {
                NetSegment segment = nm.m_segments.m_buffer[i];
                if (!IsValid(segment))
                {
                    continue;
                }
                Vector3 pos0 = nm.m_nodes.m_buffer[segment.m_startNode].m_position;
                Vector3 pos1 = nm.m_nodes.m_buffer[segment.m_endNode].m_position;


                nodeReferences[segment.m_startNode]++;
                nodeReferences[segment.m_endNode]++;
            }

            return nodeReferences;
        }

        private List<NetSegment> CollectAllAttachedSegments(List<uint> brokenEnds)
        {
            List<NetSegment> segments = new List<NetSegment>();
            NetManager nm = Singleton<NetManager>.instance;

            foreach (uint i in brokenEnds)
            {
                NetSegment[] seg = new NetSegment[8];

                seg[0] = (nm.m_segments.m_buffer[nm.m_nodes.m_buffer[i].m_segment0]);
                seg[1] = (nm.m_segments.m_buffer[nm.m_nodes.m_buffer[i].m_segment1]);
                seg[2] = (nm.m_segments.m_buffer[nm.m_nodes.m_buffer[i].m_segment2]);
                seg[3] = (nm.m_segments.m_buffer[nm.m_nodes.m_buffer[i].m_segment3]);
                seg[4] = (nm.m_segments.m_buffer[nm.m_nodes.m_buffer[i].m_segment4]);
                seg[5] = (nm.m_segments.m_buffer[nm.m_nodes.m_buffer[i].m_segment5]);
                seg[6] = (nm.m_segments.m_buffer[nm.m_nodes.m_buffer[i].m_segment6]);
                seg[7] = (nm.m_segments.m_buffer[nm.m_nodes.m_buffer[i].m_segment7]);

                foreach (NetSegment s in seg)
                {
                    if (!IsValid(s)) continue;

                    if (s.m_startNode == i || s.m_endNode == i)
                    {
                        segments.Add(s);

                    }
                }
            }

            return segments;
        }

        /*private List<NetSegment> FixSegments(List<uint> brokenEnds)
        {
            List<NetSegment> segments = new List<NetSegment>();
            NetManager nm = Singleton<NetManager>.instance;

            while (brokenEnds.Count > 0)
            {
                uint current = brokenEnds[0];
                brokenEnds.RemoveAt(0);

                int match = FindClosestNode(nm.m_nodes.m_buffer[current].m_position, brokenEnds);

                if (match == -1) break;

                NetSegment segment = new NetSegment();
                segment.m_flags |= NetSegment.Flags.Created;

                segment.m_startNode = (ushort)current;
                segment.m_endNode = (ushort)brokenEnds[match];

                if (IsValid(segment))
                {
                    brokenEnds.RemoveAt(match);
                    segments.Add(segment);
                }
            }

            return segments;
        }*/

        public void RetrieveData()
        {
            uint brokenSegments = 0u;
            List<uint> deadEnds = new List<uint>();
            List<uint> brokenEnds = new List<uint>();
            NetManager nm = Singleton<NetManager>.instance;
            ushort[] nodeReferences = CountNodeReferences();

            string svgBody = "";

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;

            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            for (ushort i = 0; i < NETSEGMENT_COUNT; i++)
            {

                string color = "rgb(0, 0, 0)";

                ushort nodeIndex0 = nm.m_segments.m_buffer[i].m_startNode;
                ushort nodeIndex1 = nm.m_segments.m_buffer[i].m_endNode;

                if (IsValid(nm.m_segments.m_buffer[i]))
                {
                    Vector3 pos0 = nm.m_nodes.m_buffer[nodeIndex0].m_position;
                    Vector3 pos1 = nm.m_nodes.m_buffer[nodeIndex1].m_position;

                    // detect dead ends
                    if (nm.m_nodes.m_buffer[nodeIndex0].CountSegments() < 2)
                    {
                        deadEnds.Add(nodeIndex0);
                        color = "rgb(255, 0, 0)";
                    }
                    if (nm.m_nodes.m_buffer[nodeIndex1].CountSegments() < 2)
                    {
                        deadEnds.Add(nodeIndex1);
                        color = "rgb(255, 0, 0)";
                    }

                    while (nodeReferences[nodeIndex0] < nm.m_nodes.m_buffer[nodeIndex0].CountSegments())
                    {
                        brokenEnds.Add(nodeIndex0);
                        color = "rgb(0, 255, 0)";
                        nodeReferences[nodeIndex0]++;
                    }

                    while (nodeReferences[nodeIndex1] < nm.m_nodes.m_buffer[nodeIndex1].CountSegments())
                    {
                        brokenEnds.Add(nodeIndex1);
                        color = "rgb(0, 255, 0)";
                        nodeReferences[nodeIndex1]++;
                    }

                    minX = Mathf.Min(minX, pos0.z, pos1.z);
                    maxX = Mathf.Max(maxX, pos0.z, pos1.z);

                    minY = Mathf.Min(minY, pos0.x, pos1.x);
                    maxY = Mathf.Max(maxY, pos0.x, pos1.x);

                    svgBody += "  <line x1=\"" + pos0.z + "\" y1=\"" + pos0.x +
                    "\" x2=\"" + pos1.z + "\" y2=\"" + pos1.x +
                    "\" style=\"stroke:" + color + ";stroke-width:4\" />" + Environment.NewLine;

                }
                else
                {
                    brokenSegments++;
                    //FixSegment(i, ref nodeIndex0, ref nodeIndex1);
                }

            }

            List<NetSegment> attachedSegments = CollectAllAttachedSegments(brokenEnds);

            for (int i = 0; i < attachedSegments.Count; i++)
            {
                NetSegment n = attachedSegments[i];
                Vector3 pos0 = nm.m_nodes.m_buffer[n.m_startNode].m_position;
                Vector3 pos1 = nm.m_nodes.m_buffer[n.m_endNode].m_position;

                svgBody += "  <line x1=\"" + pos0.z + "\" y1=\"" + pos0.x +
                    "\" x2=\"" + pos1.z + "\" y2=\"" + pos1.x +
                    "\" style=\"stroke:" + "rgb(255, 0, 255)" + ";stroke-width:4\" />" + Environment.NewLine;
            }

            // find and mark intersections

            MeshBuilder meshBuilder = new MeshBuilder();
            List<Room> rooms = new List<Room>();

            SortedDictionary<ushort, CSIntersection> intersections = meshBuilder.CreateAllIntersections();

            foreach (CSIntersection intersection in intersections.Values)
            {
                //rooms.Add(intersection.Room);
            }
            //rooms.Clear();

            SortedDictionary<uint, uint> alreadyBuilt = new SortedDictionary<uint, uint>();

            // list all intersections
            foreach (ushort intersec in intersections.Keys)
            {
                Debug.Log("Intersection Node: " + intersec);
            }

            foreach (CSIntersection intersec in intersections.Values)
            {
                foreach (ushort segIndex in intersec.AdjacentSegments.Values)
                {
                    Debug.Log("Building road with start segment " + segIndex);
                    Room road = meshBuilder.BuildRoadBlock(intersec.NodeID, segIndex, intersections, alreadyBuilt);
                    if (road != null) rooms.Add(road);                
                }
            }

            PSDLFile file = new PSDLFile();

            file.Rooms = rooms;
            file.RecalculateBounds();
            file.Version = 0;

            file.SaveAs(@"D:\Games\SteamLibrary\steamapps\common\Cities_Skylines\test.psdl");

            //PolygonMesh roadMeshCombined = PolygonMesh.Merge(roadMeshes);
            //roadMeshCombined.ExportToObj(@"D:\Games\SteamLibrary\steamapps\common\Cities_Skylines\test.psdl", 2.0f);

            ExceptionPanel panel2 = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
            panel2.SetMessage("PSDLExporter", "Wrote to file. There are " + brokenSegments + " broken segments, " + deadEnds.Count
                + " dead ends and " + brokenEnds.Count + " broken ends.", false);
        }
    }
}
