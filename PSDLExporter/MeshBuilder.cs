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
        readonly float offset = 5.0f;
        static readonly float SIDEWALK_OFFSET = 0.15f; // relative amount of total width
        static readonly float SIDEWALK_HEIGHT = 0.15f; // absolute height

        static readonly float CONVERSION_SCALE = 2.0f;

        public Vector3[] BuildSegment(ushort segIndex, SortedDictionary<ushort, SortedDictionary<ushort, Vector2[]>> intersections)
        {

            NetSegment seg = netMan.m_segments.m_buffer[segIndex];


            NetNode startNode = netMan.m_nodes.m_buffer[seg.m_startNode];
            NetNode endNode = netMan.m_nodes.m_buffer[seg.m_endNode];
            Vector3[] vertexBuffer = new Vector3[8];

            if (/*startNode.CountSegments()*/ GetAllAdjacentSegments(startNode, NetInfo.LaneType.Vehicle).Count <= 2) // no intersection
            {
                Vector3[] points = BuildNode(seg.m_startNode, segIndex);
                vertexBuffer[0] = points[0];
                vertexBuffer[3] = points[1];              
            }
            else // intersection
            {
                SortedDictionary<ushort, Vector2[]> intersection = intersections[seg.m_startNode];
                Vector2[] points2d = intersection[segIndex];

                vertexBuffer[0] = new Vector3(points2d[0].y, startNode.m_position.y, points2d[0].x);
                vertexBuffer[3] = new Vector3(points2d[1].y, startNode.m_position.y, points2d[1].x);
            }

            vertexBuffer[1] = 0.85f * vertexBuffer[0] + 0.15f * vertexBuffer[3];
            vertexBuffer[2] = 0.15f * vertexBuffer[0] + 0.85f * vertexBuffer[3];
            // sidewalk
            vertexBuffer[0].y += 0.5f;
            vertexBuffer[3].y += 0.5f;

            if (/*endNode.CountSegments()*/ GetAllAdjacentSegments(endNode, NetInfo.LaneType.Vehicle).Count <= 2) // no intersection
            {
                Vector3[] points = BuildNode(seg.m_endNode, segIndex);
                vertexBuffer[4] = points[0];
                vertexBuffer[7] = points[1];
            }
            else // intersection
            {
                SortedDictionary<ushort, Vector2[]> intersection = intersections[seg.m_endNode];
                Vector2[] points2d = intersection[segIndex];

                vertexBuffer[4] = new Vector3(points2d[0].y, endNode.m_position.y, points2d[0].x);
                vertexBuffer[7] = new Vector3(points2d[1].y, endNode.m_position.y, points2d[1].x);
            }

            vertexBuffer[5] = 0.85f * vertexBuffer[4] + 0.15f * vertexBuffer[7];
            vertexBuffer[6] = 0.15f * vertexBuffer[4] + 0.85f * vertexBuffer[7];
            // sidewalk
            vertexBuffer[4].y += 0.5f;
            vertexBuffer[7].y += 0.5f;

            return vertexBuffer;
        }

        public SortedDictionary<ushort, SortedDictionary<ushort, Vector2[]>> CreateAllIntersections(List<PolygonMesh> intersectionMeshes)
        {
            SortedDictionary<ushort, SortedDictionary<ushort, Vector2[]>> intersections = new SortedDictionary<ushort, SortedDictionary<ushort, Vector2[]>>();

            int intersectionNo = 0;
            for(ushort i = 0; i < MeshExporter.NETNODE_COUNT; i++)
            {
                if((netMan.m_nodes.m_buffer[i].m_flags & NetNode.Flags.Created) != NetNode.Flags.None
                    && /*.CountSegments()*/ GetAllAdjacentSegments(netMan.m_nodes.m_buffer[i], NetInfo.LaneType.Vehicle).Count > 2)
                {
                    SortedDictionary<ushort, Vector2[]> intersection = BuildIntersection(i, out Vector2[] unused, intersectionNo, out PolygonMesh SingleIntersectionMesh);
                    intersectionMeshes.Add(SingleIntersectionMesh);
                    intersections.Add(i, intersection);
                    intersectionNo++;
                }
            }
            return intersections;
        }

        public SortedDictionary<ushort, CSIntersection> CreateAllIntersections()
        {
            SortedDictionary<ushort, CSIntersection> intersections = new SortedDictionary<ushort, CSIntersection>();

            ushort intersectionNo = 0;
            for (ushort i = 0; i < MeshExporter.NETNODE_COUNT; i++)
            {
                if ((netMan.m_nodes.m_buffer[i].m_flags & NetNode.Flags.Created) != NetNode.Flags.None
                    && /*.CountSegments()*/ GetAllAdjacentSegments(netMan.m_nodes.m_buffer[i], NetInfo.LaneType.Vehicle).Count > 2)
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

        public Vector3[] BuildNode(ushort nodeIndex, ushort segIndex)
        {
            NetNode node = netMan.m_nodes.m_buffer[nodeIndex];
            List<ushort> segments = GetAllAdjacentSegments(node, NetInfo.LaneType.Vehicle);

            Vector3 averageDir = new Vector3(0.0f, 0.0f, 0.0f);

            if (segments.Count > 1)
            {
                // make sure orientation is always the same
                if (segIndex == segments[1])
                {
                    segments[1] = segments[0];
                    segments[0] = segIndex;
                }

                Vector3 dir0 = netMan.m_segments.m_buffer[segments[0]].GetDirection(nodeIndex);
                dir0.Normalize();
                averageDir += dir0;

                Vector3 dir1 = netMan.m_segments.m_buffer[segments[1]].GetDirection(nodeIndex);
                dir1.Normalize();
                averageDir -= dir1;
            }
            else
            {
                Vector3 dir0 = netMan.m_segments.m_buffer[segments[0]].GetDirection(nodeIndex);
                dir0.Normalize();
                averageDir += dir0;
            }

            Vector3 normalInPlane = new Vector3(-averageDir.z, 0.0f, averageDir.x);
            normalInPlane.Normalize();

            Vector3[] points = new Vector3[2];
            points[0] = netMan.m_nodes.m_buffer[nodeIndex].m_position + offset * normalInPlane;
            points[1] = netMan.m_nodes.m_buffer[nodeIndex].m_position - offset * normalInPlane;

            return points;
        }

        private Vector2 GetStraightDirection(ushort segIndex, ushort nodeIndex)
        {
            NetSegment seg = netMan.m_segments.m_buffer[segIndex];
            Vector3 dir3d;

            if(nodeIndex == seg.m_startNode)
            {
                dir3d = netMan.m_nodes.m_buffer[seg.m_endNode].m_position - netMan.m_nodes.m_buffer[seg.m_startNode].m_position;
            }
            else
            {
                dir3d = netMan.m_nodes.m_buffer[seg.m_startNode].m_position - netMan.m_nodes.m_buffer[seg.m_endNode].m_position;
            }

            return new Vector2(dir3d.z, dir3d.x);
        }

        private List<ushort> GetAllAdjacentSegments(NetNode node, NetInfo.LaneType laneType)
        {
            List<ushort> segmentIDs = new List<ushort>();

            if ((netMan.m_segments.m_buffer[node.m_segment0].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment0].CountLanes(node.m_segment0, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if(l + r > 0) segmentIDs.Add(node.m_segment0);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment1].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment1].CountLanes(node.m_segment1, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment1);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment2].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment2].CountLanes(node.m_segment2, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment2);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment3].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment3].CountLanes(node.m_segment3, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment3);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment4].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment4].CountLanes(node.m_segment4, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment4);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment5].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment5].CountLanes(node.m_segment5, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment5);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment6].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment6].CountLanes(node.m_segment6, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment6);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment7].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment7].CountLanes(node.m_segment7, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment7);
            }

            return segmentIDs;
        }

        private void AddSegment(ushort segIndex, ushort nodeIndex, SortedDictionary<float, ushort> adjacentSegments)
        {
            // Vector3 direction = seg.GetDirection(nodeIndex);
            Vector2 rot = GetStraightDirection(segIndex, nodeIndex);//new Vector2(direction.z, direction.x);
            rot.Normalize();

            // approximate angle
            float angle;

            if (rot.y >= 0)
            {
                angle = Mathf.Acos(rot.x);
            }
            else
            {
                angle = 2 * Mathf.PI - Mathf.Acos(rot.x);
            }

            if (adjacentSegments.ContainsKey(angle))
            {
                if (adjacentSegments[angle] != segIndex)
                {
                    Debug.LogWarning("Found two segments with identical direction! The second segment is added anyway but might cause issues.");

                    do
                    {
                        angle += 0.001f;
                    } while (adjacentSegments.ContainsKey(angle));

                    adjacentSegments.Add(angle, segIndex);
                }
                else
                {
                    Debug.Log("Redundant segment is ignored.");
                }
            }
            else
            {
                adjacentSegments.Add(angle, segIndex);
            }
        }

        private ushort GetNextNode(ushort nodeIndex, ushort segmentIndex)
        {
            if (netMan.m_segments.m_buffer[segmentIndex].m_startNode == nodeIndex)
                return netMan.m_segments.m_buffer[segmentIndex].m_endNode;

            return netMan.m_segments.m_buffer[segmentIndex].m_startNode;
        }

        private ushort GetNextSegment(ushort nodeIndex, ushort segmentIndex)
        {
            List<ushort> segments = GetAllAdjacentSegments(netMan.m_nodes.m_buffer[nodeIndex], NetInfo.LaneType.Vehicle);
            Debug.Assert(segments.Count == 2);

            if (segments[0] == segmentIndex)
                return segments[1];

            return segments[0];
        }

        public Room BuildRoadBlock(ushort intersectionIndex, ushort firstSegIndex,
            SortedDictionary<ushort, CSIntersection> intersections, SortedDictionary<uint, uint> alreadyBuilt)
        {
            if (alreadyBuilt.ContainsKey(firstSegIndex)) return null;

            ushort currentNode = GetNextNode(intersectionIndex, firstSegIndex);

            ushort currentSegment = firstSegIndex;
            List<Vector3[]> vertexList = new List<Vector3[]>();

            // get vertices from start intersection
            Debug.Log("Getting start intersection for node " + intersectionIndex + " and segment " + firstSegIndex + "...");
            Debug.Log("Second node: " + currentNode);
            SortedDictionary<ushort, Vector2[]> intersection = intersections[intersectionIndex].ConnectionPoints;
            Vector2[] points2d = intersection[firstSegIndex];
            Vector3[] startIntersection = new Vector3[2];
            startIntersection[1] = new Vector3(points2d[0].y, netMan.m_nodes.m_buffer[intersectionIndex].m_position.y, points2d[0].x);
            startIntersection[0] = new Vector3(points2d[1].y, netMan.m_nodes.m_buffer[intersectionIndex].m_position.y, points2d[1].x);
            vertexList.Add(startIntersection);

            while (/*.CountSegments()*/ GetAllAdjacentSegments(netMan.m_nodes.m_buffer[currentNode], NetInfo.LaneType.Vehicle).Count == 2)
            {
                Vector3[] vertices = BuildNode(currentNode, currentSegment);
                vertexList.Add(vertices);

                currentSegment = GetNextSegment(currentNode, currentSegment);
                currentNode = GetNextNode(currentNode, currentSegment);

                Debug.Assert((netMan.m_nodes.m_buffer[currentNode].m_flags & NetNode.Flags.Created) != NetNode.Flags.None);
                Debug.Log("Current segment: " + currentSegment);
                Debug.Log("Current node: " + currentNode);

            }

            if (/*netMan.m_nodes.m_buffer[currentNode].CountSegments()*/ GetAllAdjacentSegments(netMan.m_nodes.m_buffer[currentNode], NetInfo.LaneType.Vehicle).Count < 2)
            {
                Vector3[] vertices = BuildNode(currentNode, currentSegment);
                vertexList.Add(vertices);
            }
            else
            {

                // get vertices from end intersection
                Debug.Log("Getting end intersection...");
                SortedDictionary<ushort, Vector2[]> intersectionEnd = intersections[currentNode].ConnectionPoints;
                Vector2[] pointsEnd2d = intersectionEnd[currentSegment];
                Vector3[] endIntersection = new Vector3[2];
                endIntersection[0] = new Vector3(pointsEnd2d[0].y, netMan.m_nodes.m_buffer[currentNode].m_position.y, pointsEnd2d[0].x);
                endIntersection[1] = new Vector3(pointsEnd2d[1].y, netMan.m_nodes.m_buffer[currentNode].m_position.y, pointsEnd2d[1].x);
                vertexList.Add(endIntersection);

                alreadyBuilt.Add(currentSegment, currentSegment);
            }

            // now build block
            Debug.Log("Creating block vertices");
            Vertex[] blockVertices = new Vertex[vertexList.Count * 4];
            List<PerimeterPoint> perimeterPoints = new List<PerimeterPoint>();

            // fill vertex buffer
            for (int i = 0; i < vertexList.Count; i++)
            {
                // TODO: might need to adjust orientation. For the old OBJ -> Blender -> 3ds -> MM2CT way x and z had to be swapped.
                Debug.Log("Filling vertex buffer..");
                blockVertices[4 * i] = new Vertex(vertexList[i][1].z, vertexList[i][1].y, vertexList[i][1].x) * CONVERSION_SCALE;

                blockVertices[4 * i + 3] = new Vertex(vertexList[i][0].z, vertexList[i][0].y,  vertexList[i][0].x) * CONVERSION_SCALE;
                blockVertices[4 * i] = new Vertex(vertexList[i][0].z, vertexList[i][0].y,  vertexList[i][0].x) * CONVERSION_SCALE;

                blockVertices[4 * i + 1] = blockVertices[4 * i] * (1.0f - SIDEWALK_OFFSET) + blockVertices[4 * i + 3] * SIDEWALK_OFFSET;
                blockVertices[4 * i + 2] = blockVertices[4 * i] * SIDEWALK_OFFSET + blockVertices[4 * i + 3] * (1.0f - SIDEWALK_OFFSET);
                

                blockVertices[4 * i].y += SIDEWALK_HEIGHT;
                blockVertices[4 * i + 3].y += SIDEWALK_HEIGHT;

                Debug.Log("Setting up perimeter points...");

                Room neighbor = null;

                // TODO: perimeters for ground
                if (i == 0)
                {
                    neighbor = intersections[intersectionIndex].Room;
                }
                else if(i == vertexList.Count - 1)
                {
                    neighbor = intersections[currentNode].Room;
                }

 
                PerimeterPoint leftPoint = new PerimeterPoint(blockVertices[4 * i], neighbor); // TODO: add intersection or ground or whatever.
                PerimeterPoint rightPoint = new PerimeterPoint(blockVertices[4 * i + 3], neighbor); // TODO: add intersection or ground or whatever.

                // Insert left points in opposite order
                perimeterPoints.Add(rightPoint);
                perimeterPoints.Insert(0, leftPoint);


            }

            Debug.Log("Creating road element...");
            RoadElement[] roadArray = new RoadElement[1];
            roadArray[0] = new RoadElement("r2_f", "swalk_f", "r2_lo_f", blockVertices);

            Debug.Log("Creating room...");
            Room room = new Room(roadArray, perimeterPoints, 0, RoomFlags.Road);

            // setup intersection perimeters
            List<PerimeterPoint> startPerimeters = intersections[intersectionIndex].PerimeterPoints[firstSegIndex];
            List<PerimeterPoint> endPerimeters = intersections[currentNode].PerimeterPoints[currentSegment];

            foreach (PerimeterPoint pp in startPerimeters)
            {
                pp.ConnectedRoom = room;
            }

            foreach (PerimeterPoint pp in endPerimeters)
            {
                pp.ConnectedRoom = room;
            }

            return room;
        }

        public PolygonMesh BuildRoad(ushort intersectionIndex, ushort firstSegIndex,
            SortedDictionary<ushort, SortedDictionary<ushort, Vector2[]>> intersections, SortedDictionary<uint, uint> alreadyBuilt)
        {
            if (alreadyBuilt.ContainsKey(firstSegIndex)) return null;

            ushort currentNode = GetNextNode(intersectionIndex, firstSegIndex);

            ushort currentSegment = firstSegIndex;
            List<Vector3[]> vertexList = new List<Vector3[]>();

            // get vertices from start intersection
            Debug.Log("Getting start intersection for node " + intersectionIndex + " and segment " + firstSegIndex + "...");
            Debug.Log("Second node: " + currentNode);
            SortedDictionary<ushort, Vector2[]> intersection = intersections[intersectionIndex];
            Vector2[] points2d = intersection[firstSegIndex];
            Vector3[] startIntersection = new Vector3[2];
            startIntersection[1] = new Vector3(points2d[0].y, netMan.m_nodes.m_buffer[intersectionIndex].m_position.y, points2d[0].x);
            startIntersection[0] = new Vector3(points2d[1].y, netMan.m_nodes.m_buffer[intersectionIndex].m_position.y, points2d[1].x);
            vertexList.Add(startIntersection);

            while (/*.CountSegments()*/ GetAllAdjacentSegments(netMan.m_nodes.m_buffer[currentNode], NetInfo.LaneType.Vehicle).Count == 2)
            {
                Vector3[] vertices = BuildNode(currentNode, currentSegment);
                vertexList.Add(vertices);

                currentSegment = GetNextSegment(currentNode, currentSegment);
                currentNode = GetNextNode(currentNode, currentSegment);

                Debug.Assert((netMan.m_nodes.m_buffer[currentNode].m_flags & NetNode.Flags.Created) != NetNode.Flags.None);
                Debug.Log("Current segment: " + currentSegment);
                Debug.Log("Current node: " + currentNode);

            }
            // TODO: handle dead end
            if (/*netMan.m_nodes.m_buffer[currentNode].CountSegments()*/ GetAllAdjacentSegments(netMan.m_nodes.m_buffer[currentNode], NetInfo.LaneType.Vehicle).Count < 2)
            {
                Vector3[] vertices = BuildNode(currentNode, currentSegment);
                vertexList.Add(vertices);
            }
            else
            {

                // get vertices from end intersection
                Debug.Log("Getting end intersection...");
                SortedDictionary<ushort, Vector2[]> intersectionEnd = intersections[currentNode];
                Vector2[] pointsEnd2d = intersectionEnd[currentSegment];
                Vector3[] endIntersection = new Vector3[2];
                endIntersection[0] = new Vector3(pointsEnd2d[0].y, netMan.m_nodes.m_buffer[currentNode].m_position.y, pointsEnd2d[0].x);
                endIntersection[1] = new Vector3(pointsEnd2d[1].y, netMan.m_nodes.m_buffer[currentNode].m_position.y, pointsEnd2d[1].x);
                vertexList.Add(endIntersection);

                alreadyBuilt.Add(currentSegment, currentSegment);
            }

            // now build mesh
            PolygonMesh roadMesh = new PolygonMesh();
            roadMesh.vertices = new Vector3[vertexList.Count * 4];
            roadMesh.polygons = new int[(vertexList.Count - 1) * 3][];

            // fill vertex buffer
            for (int i = 0; i < vertexList.Count; i++)
            {
                roadMesh.vertices[4 * i] = vertexList[i][0];
                roadMesh.vertices[4 * i].y += SIDEWALK_HEIGHT;
                roadMesh.vertices[4 * i + 1] = (1.0f - SIDEWALK_OFFSET) * vertexList[i][0] + SIDEWALK_OFFSET * vertexList[i][1];
                roadMesh.vertices[4 * i + 2] = SIDEWALK_OFFSET * vertexList[i][0] + (1.0f - SIDEWALK_OFFSET) * vertexList[i][1];
                roadMesh.vertices[4 * i + 3] = vertexList[i][1];
                roadMesh.vertices[4 * i + 3].y += SIDEWALK_HEIGHT;
            }

            // fill polygon buffer
            for (int i = 0; i < vertexList.Count - 1; i++)
            {
                int[] poly0 = { 4 * i + 1, 4 * i + 0, 4 * i + 4, 4 * i + 5 };
                int[] poly1 = { 4 * i + 2, 4 * i + 1, 4 * i + 5, 4 * i + 6 };
                int[] poly2 = { 4 * i + 3, 4 * i + 2, 4 * i + 6, 4 * i + 7 };

                roadMesh.polygons[3 * i] = poly0;
                roadMesh.polygons[3 * i + 1] = poly1;
                roadMesh.polygons[3 * i + 2] = poly2;
            }

            roadMesh.groups.Add(0, "ROADS_" + firstSegIndex + Environment.NewLine + "usemtl rmain"); // mm2ct road with sidewalk tag

            return roadMesh;
        }

        public SortedDictionary<ushort, Vector2[]> BuildIntersectionRoom(ushort nodeIndex, out Vector2[] roadBounds, int intersectionNo, out Room intersectionRoom, out SortedDictionary<ushort, List<PerimeterPoint> > toBeConnected)
        {
            toBeConnected = new SortedDictionary<ushort, List<PerimeterPoint>>();
            NetNode node = netMan.m_nodes.m_buffer[nodeIndex];

            // segments sorted by angle
            SortedDictionary<float, ushort> adjacentSegments = new SortedDictionary<float, ushort>();

            if ((netMan.m_segments.m_buffer[node.m_segment0].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {

                AddSegment(node.m_segment0, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment1].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment1, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment2].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment2, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment3].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment3, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment4].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment4, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment5].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment5, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment6].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment6, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment7].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment7, nodeIndex, adjacentSegments);
            }

            //Debug.Assert(adjacentSegments.Count == node.CountSegments());

            // sort by angle and intersect each segment with its neighbor
            KeyValuePair<float, ushort>[] adjacentSegmentsArray = adjacentSegments.ToArray();
            Vector2[] meetingPoints = new Vector2[adjacentSegmentsArray.Length];
            SortedDictionary<ushort, Vector2[]> connections = new SortedDictionary<ushort, Vector2[]>();
            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                Vector2[] connection = new Vector2[2];
                connections.Add(adjacentSegmentsArray[i].Value, connection);
            }

            roadBounds = new Vector2[2 * adjacentSegmentsArray.Length];

            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                if (i != adjacentSegmentsArray.Length - 1)
                {
                    Debug.Assert(adjacentSegmentsArray[i].Key < adjacentSegmentsArray[i + 1].Key);
                }

                // intersect pair
                Vector2 dirA2d = GetStraightDirection(adjacentSegmentsArray[i].Value, nodeIndex);
                dirA2d.Normalize();
                Vector2 normA = new Vector2(dirA2d.y, -dirA2d.x);

                Vector2 dirB2d = GetStraightDirection(adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value, nodeIndex);
                dirB2d.Normalize();
                Vector2 normB = new Vector2(dirB2d.y, -dirB2d.x);

                Vector2 supportA = new Vector2(node.m_position.z, node.m_position.x) - (offset * normA); // make intersections smooth
                Vector2 supportB = new Vector2(node.m_position.z, node.m_position.x) + (offset * normB);

                roadBounds[2 * i] = supportA;
                roadBounds[2 * i + 1] = supportB;

                if (supportA == supportB)
                {
                    meetingPoints[i] = supportA;
                }
                else
                {
                    meetingPoints[i] = IntersectLines(supportA, dirA2d, supportB, dirB2d);

                    Debug.Assert(meetingPoints[i] == IntersectLines(supportB, dirB2d, supportA, dirA2d));
                }

                connections[adjacentSegmentsArray[i].Value][1] = meetingPoints[i] + 4.0f * dirA2d;
                connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0] = meetingPoints[i] + 4.0f * dirB2d;

            }  

            List<ISDLElement> intersectionElements = new List<ISDLElement>();
            List<Vertex> innerIntersection = new List<Vertex>();
            List<PerimeterPoint> perimeterPoints = new List<PerimeterPoint>();

            // construct sidewalk meshes.
            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                // use quadratic bezier curve to connect the two points smoothly with the meeting point as support
                Vector2[] outerSidewalkPoints = new Vector2[5];

                outerSidewalkPoints[0] = new Vector2(connections[adjacentSegmentsArray[i].Value][1].x, connections[adjacentSegmentsArray[i].Value][1].y);
                outerSidewalkPoints[4] = new Vector2(connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0].x,
                    connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0].y);

                outerSidewalkPoints[1] = QuadraticBezier(connections[adjacentSegmentsArray[i].Value][1], meetingPoints[i],
                    connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0], 0.25f);
                outerSidewalkPoints[2] = QuadraticBezier(connections[adjacentSegmentsArray[i].Value][1], meetingPoints[i],
                    connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0], 0.5f);
                outerSidewalkPoints[3] = QuadraticBezier(connections[adjacentSegmentsArray[i].Value][1], meetingPoints[i],
                    connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0], 0.75f);

                /*for (int j = 0; j < 5; j++)
                {
                    // TODO: set connections (should be roads for first and last and terrain for all.
                    // TODO: are concave shapes permitted?!
                    perimeterPoints.Add(new PerimeterPoint(new Vertex(outerSidewalkPoints[j].y, node.m_position.y + SIDEWALK_HEIGHT, outerSidewalkPoints[j].x) * CONVERSION_SCALE, null));
                }*/

                Vector2[] sidewalkRoadBorderPoints = new Vector2[5];

                Vector2 crossingDir0 = connections[adjacentSegmentsArray[i].Value][0] - connections[adjacentSegmentsArray[i].Value][1];
                sidewalkRoadBorderPoints[0] = connections[adjacentSegmentsArray[i].Value][1] + SIDEWALK_OFFSET * crossingDir0;

                Vector2 crossingDir1 = connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][1]
                    - connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0];
                sidewalkRoadBorderPoints[4] = connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0] + SIDEWALK_OFFSET * crossingDir1;

                Vector2 supportPoint = meetingPoints[i] + SIDEWALK_OFFSET * crossingDir0 + SIDEWALK_OFFSET * crossingDir1;

                // TODO: calculate this differently.
                sidewalkRoadBorderPoints[1] = QuadraticBezier(sidewalkRoadBorderPoints[0], supportPoint, sidewalkRoadBorderPoints[4], 0.25f);
                sidewalkRoadBorderPoints[2] = QuadraticBezier(sidewalkRoadBorderPoints[0], supportPoint, sidewalkRoadBorderPoints[4], 0.5f);
                sidewalkRoadBorderPoints[3] = QuadraticBezier(sidewalkRoadBorderPoints[0], supportPoint, sidewalkRoadBorderPoints[4], 0.75f);


                innerIntersection.Add(new Vertex(sidewalkRoadBorderPoints[2].x, node.m_position.y, sidewalkRoadBorderPoints[2].y) * CONVERSION_SCALE);

                

                List<Vertex> blockVertices = new List<Vertex>();

                for(int j = 0; j < 5; j++)
                {
                    blockVertices.Add(new Vertex(outerSidewalkPoints[j].x, node.m_position.y + SIDEWALK_HEIGHT, outerSidewalkPoints[j].y) * CONVERSION_SCALE);
                    blockVertices.Add(new Vertex(sidewalkRoadBorderPoints[j].x, node.m_position.y, sidewalkRoadBorderPoints[j].y) * CONVERSION_SCALE);                   
                }

                /*for (int j = 4; j >= 0; j--)
                {
                    
                }*/

                // now construct sidewalk mesh from this
                SidewalkStripElement sidewalk = new SidewalkStripElement("swalk_inter_f", blockVertices);

                intersectionElements.Add(sidewalk);
            }

 

            // construct crosswalk meshes
            int polyOffset = 8 * adjacentSegmentsArray.Length;

            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                List<Vertex> crosswalkVertices = new List<Vertex>();              

                SidewalkStripElement side0 = (SidewalkStripElement)intersectionElements[i];
                SidewalkStripElement side1 = (SidewalkStripElement)intersectionElements[(i+1)% adjacentSegmentsArray.Length];

                crosswalkVertices.Add(side0.GetVertex(7));
                crosswalkVertices.Add(side0.GetVertex(9));

                crosswalkVertices.Add(side1.GetVertex(3));
                crosswalkVertices.Add(side1.GetVertex(1));

                // TODO: need to be connected to road.
                perimeterPoints.Add(new PerimeterPoint(side0.GetVertex(8), null));
                perimeterPoints.Add(new PerimeterPoint(side1.GetVertex(0), null));

                CrosswalkElement crosswalk = new CrosswalkElement("rxwalk_f", crosswalkVertices);
                intersectionElements.Add(crosswalk);

                List<Vertex> betweenVertices = new List<Vertex>();

                
                betweenVertices.Add(side1.GetVertex(5));
                betweenVertices.Add(side1.GetVertex(3));
                betweenVertices.Add(side0.GetVertex(7));
                betweenVertices.Add(side0.GetVertex(5));

                CulledTriangleFanElement between = new CulledTriangleFanElement("rinter_f", betweenVertices);
                intersectionElements.Add(between);
            }

            innerIntersection.Reverse();
            CulledTriangleFanElement interior = new CulledTriangleFanElement("rinter_f", innerIntersection);

            intersectionElements.Add(interior);

            Debug.Assert(perimeterPoints.Count == adjacentSegmentsArray.Length * 2);

            perimeterPoints.Reverse();

            intersectionRoom = new Room(intersectionElements, perimeterPoints, 0, RoomFlags.Intersection);

            return connections;
        }
    

        public SortedDictionary<ushort, Vector2[]> BuildIntersection(ushort nodeIndex, out Vector2[] roadBounds, int intersectionNo, out PolygonMesh intersection)
        {
            NetNode node = netMan.m_nodes.m_buffer[nodeIndex];

            // segments sorted by angle
            SortedDictionary<float, ushort> adjacentSegments = new SortedDictionary<float, ushort>();

            if((netMan.m_segments.m_buffer[node.m_segment0].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None){

                AddSegment(node.m_segment0, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment1].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment1, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment2].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment2, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment3].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment3, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment4].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment4, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment5].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment5, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment6].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment6, nodeIndex, adjacentSegments);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment7].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(node.m_segment7, nodeIndex, adjacentSegments);
            }

            //Debug.Assert(adjacentSegments.Count == node.CountSegments());

            // sort by angle and intersect each segment with its neighbor
            KeyValuePair<float, ushort>[] adjacentSegmentsArray = adjacentSegments.ToArray();
            Vector2[] meetingPoints = new Vector2[adjacentSegmentsArray.Length];
            SortedDictionary<ushort, Vector2[]> connections = new SortedDictionary<ushort, Vector2[]>();
            for(int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                Vector2[] connection = new Vector2[2];
                connections.Add(adjacentSegmentsArray[i].Value, connection);
            }

            roadBounds = new Vector2[ 2 * adjacentSegmentsArray.Length];

            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                if(i != adjacentSegmentsArray.Length - 1)
                {
                    Debug.Assert(adjacentSegmentsArray[i].Key < adjacentSegmentsArray[i + 1].Key);
                }

                // intersect pair
                Vector2 dirA2d = GetStraightDirection(adjacentSegmentsArray[i].Value, nodeIndex);
                dirA2d.Normalize();
                Vector2 normA = new Vector2(dirA2d.y, -dirA2d.x);

                Vector2 dirB2d = GetStraightDirection(adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value, nodeIndex);
                dirB2d.Normalize();
                Vector2 normB = new Vector2(dirB2d.y, -dirB2d.x);

                Vector2 supportA = new Vector2(node.m_position.z, node.m_position.x) - (offset * normA); // make intersections smooth
                Vector2 supportB = new Vector2(node.m_position.z, node.m_position.x) + (offset * normB);

                roadBounds[2 * i] = supportA;
                roadBounds[2 * i + 1] = supportB;

                if (supportA == supportB)
                {
                    meetingPoints[i] = supportA;
                }
                else
                {
                    meetingPoints[i] = IntersectLines(supportA, dirA2d, supportB, dirB2d);

                    Debug.Assert(meetingPoints[i] == IntersectLines(supportB, dirB2d, supportA, dirA2d));
                }

                connections[adjacentSegmentsArray[i].Value][1] = meetingPoints[i] + 4.0f * dirA2d;
                connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0] = meetingPoints[i] + 4.0f * dirB2d;

            }

            intersection = new PolygonMesh();

            Vector3[] vertices = new Vector3[10 * adjacentSegmentsArray.Length];
            // sidewalks + crosswalks + intersection center
            int[][] polys = new int[8 * adjacentSegmentsArray.Length + adjacentSegmentsArray.Length + 1][];

            // construct sidewalk meshes.
            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                // use quadratic bezier curve to connect the two points smoothly with the meeting point as support
                Vector2[] borderPoints = new Vector2[5];

                borderPoints[0] = connections[adjacentSegmentsArray[i].Value][1];
                borderPoints[4] = connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0];

                borderPoints[1] = QuadraticBezier(connections[adjacentSegmentsArray[i].Value][1], meetingPoints[i],
                    connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0], 0.25f);
                borderPoints[2] = QuadraticBezier(connections[adjacentSegmentsArray[i].Value][1], meetingPoints[i],
                    connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0], 0.5f);
                borderPoints[3] = QuadraticBezier(connections[adjacentSegmentsArray[i].Value][1], meetingPoints[i],
                    connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0], 0.75f);

                Vector2[] sidewalkBorderPoints = new Vector2[5];

                Vector2 crossingDir0 = connections[adjacentSegmentsArray[i].Value][0] - connections[adjacentSegmentsArray[i].Value][1];
                sidewalkBorderPoints[0] = connections[adjacentSegmentsArray[i].Value][1] + SIDEWALK_OFFSET * crossingDir0;

                Vector2 crossingDir1 = connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][1]
                    - connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0];
                sidewalkBorderPoints[4] = connections[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0] + SIDEWALK_OFFSET * crossingDir1;

                Vector2 supportPoint = meetingPoints[i] + SIDEWALK_OFFSET * crossingDir0 + SIDEWALK_OFFSET * crossingDir1;

                // TODO: calculate this differently.
                sidewalkBorderPoints[1] = QuadraticBezier(sidewalkBorderPoints[0], supportPoint, sidewalkBorderPoints[4], 0.25f);
                sidewalkBorderPoints[2] = QuadraticBezier(sidewalkBorderPoints[0], supportPoint, sidewalkBorderPoints[4], 0.5f);
                sidewalkBorderPoints[3] = QuadraticBezier(sidewalkBorderPoints[0], supportPoint, sidewalkBorderPoints[4], 0.75f);

                // now construct sidewalk mesh from this
                

                for(int j = 0; j < 5; j++)
                {
                    vertices[10 * i + j] = new Vector3(borderPoints[j].y, node.m_position.y + SIDEWALK_HEIGHT, borderPoints[j].x);
                    vertices[10 * i + j + 5] = new Vector3(sidewalkBorderPoints[j].y, node.m_position.y, sidewalkBorderPoints[j].x);
                }

                for(int j = 0; j < 4; j++)
                {
                    int[] tri0 = { 10 * i + j, 10 * i + j + 5, 10 * i + j + 1 };
                    polys[8 * i + 2 * j] = tri0;

                    int[] tri1 = { 10 * i + j + 6, 10 * i + j + 1, 10 * i + j + 5 };
                    polys[8 * i + 2 * j + 1] = tri1;
                }

                
                intersection.groups.Add(8 * i + 0, "SW" + intersectionNo + "_" + i + Environment.NewLine + "usemtl iwalk"); // mm2ct sidewalk tag

                
            }

            intersection.vertices = vertices;
            intersection.polygons = polys;

            // construct crosswalk meshes
            int polyOffset = 8 * adjacentSegmentsArray.Length;

            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                // all vertices lie in the same plane so we can use a quad here
                int[] quad = { 10 * i + 9, 10 * i + 8, 10 * ((i + 1) % adjacentSegmentsArray.Length) + 6, 10 * ((i + 1) % adjacentSegmentsArray.Length) + 5 };
                intersection.polygons[polyOffset + i] = quad;
                intersection.groups.Add(polyOffset + i, "CW" + intersectionNo + "_" + i + Environment.NewLine + "usemtl icross"); // mm2ct crosswalk tag

            }


            int[] poly = new int[3 * adjacentSegmentsArray.Length];

            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                int iRev = adjacentSegmentsArray.Length - i - 1;
                poly[3 * i] = 10 * iRev + 8;
                poly[3 * i + 1] = 10 * iRev + 7;
                poly[3 * i + 2] = 10 * iRev + 6;
            }

            intersection.polygons[intersection.polygons.Length - 1] = poly;

            intersection.groups.Add(intersection.polygons.Length - 1, "IS" + intersectionNo + "_" + Environment.NewLine + "usemtl isurf"); // mm2ct intersection tag

            return connections;
        }



        Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float param)
        {
            return (1 - param) * (1 - param) * p0 + 2 * (1 - param) * param * p1 + param * param * p2;
        }

        public Vector2 IntersectLines(Vector2 supportA, Vector2 directionA, Vector2 supportB, Vector2 directionB)
        {

            if(Mathf.Abs(directionB.y * directionA.x - directionB.x * directionA.y) < 0.0001)
            { // almost parallel
                return (supportA + supportB) / 2.0f;
            }
            float paramB;
            Vector2 supBMinusSupA = supportB - supportA;

            if (directionA.x == 0.0f && directionB.x != 0.0f)
            {
                paramB = -supBMinusSupA.x / directionB.x;
            }
            else if (directionA.y == 0.0f && directionB.y != 0.0f)
            {
                paramB = -supBMinusSupA.y / directionB.y;
            }
            else if (directionB.x == 0.0f && directionA.x != 0.0f)
            {
                float paramA = supBMinusSupA.x / directionA.x;
                return supportA + paramA * directionA;
            }
            else if (directionB.y == 0.0f && directionA.y != 0.0f)
            {
                float paramA = supBMinusSupA.y / directionA.y;
                return supportA + paramA * directionA;
            }
            else
            {
                paramB = (directionA.y * supBMinusSupA.x - directionA.x * supBMinusSupA.y)
                    / (directionB.y * directionA.x - directionB.x * directionA.y);
            }

            return supportB + paramB * directionB;
        }
    }
}
