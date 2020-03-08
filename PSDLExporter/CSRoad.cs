using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework;
using PSDL;
using PSDL.Elements;
using UnityEngine;

namespace PSDLExporter
{
    class CSRoad
    {
        private List<ushort> segmentIDs = new List<ushort>();
        private List<ushort> nodeIDs = new List<ushort>();
        private Room room; //TODO: use list to allow multiple rooms per road
        private CSIntersection cSstartIntersection;
        private CSIntersection cSendIntersection = null;
        private List<PerimeterPoint> leftPerimeter = new List<PerimeterPoint>();
        private List<PerimeterPoint> rightPerimeter = new List<PerimeterPoint>();
        public List<PerimeterPoint> startPerimeter = new List<PerimeterPoint>();
        public List<PerimeterPoint> endPerimeter = new List<PerimeterPoint>();

        private static NetManager netMan = Singleton<NetManager>.instance;

        public Room Room { get => room; }
        public List<ushort> SegmentIDs { get => segmentIDs; }
        public List<ushort> NodeIDs { get => nodeIDs; }
        internal CSIntersection CSstartIntersection { get => cSstartIntersection; set => cSstartIntersection = value; }
        internal CSIntersection CSendIntersection { get => cSendIntersection; set => cSendIntersection = value; }

        public CSRoad(ushort startIntersectionID, ushort startSegmentID, SortedDictionary<ushort, CSIntersection> intersections)
        {
            cSstartIntersection = intersections[startIntersectionID];
            // traverse road until we find the next intersection
            nodeIDs.Add(startIntersectionID);
            ushort currentNode = RoadUtils.GetNextNode(startIntersectionID, startSegmentID);

            ushort currentSegment = startSegmentID;

            while (RoadUtils.GetAllAdjacentSegments(netMan.m_nodes.m_buffer[currentNode], NetInfo.LaneType.Vehicle).Count == 2)
            {
                segmentIDs.Add(currentSegment);
                nodeIDs.Add(currentNode);
                currentSegment = RoadUtils.GetNextSegment(currentNode, currentSegment);
                currentNode = RoadUtils.GetNextNode(currentNode, currentSegment);
            }

            segmentIDs.Add(currentSegment);
            nodeIDs.Add(currentNode);

            if(intersections.ContainsKey(currentNode))
            {
                cSendIntersection = intersections[currentNode];
            }

        }

        public void BuildRoadBlock()
        {
            List<Vector3[]> vertexList = new List<Vector3[]>();

            // get vertices from start intersection
            Debug.Log("Retrieving start intersection...");
            SortedDictionary<ushort, Vector2[]> intersection = cSstartIntersection.ConnectionPoints;
            Vector2[] points2d = intersection[segmentIDs[0]];
            Vector3[] startIntersection = new Vector3[2];
            startIntersection[1] = new Vector3(points2d[0].y, netMan.m_nodes.m_buffer[cSstartIntersection.NodeID].m_position.y, points2d[0].x);
            startIntersection[0] = new Vector3(points2d[1].y, netMan.m_nodes.m_buffer[cSstartIntersection.NodeID].m_position.y, points2d[1].x);
            vertexList.Add(startIntersection);

            Debug.Log("Following road...");
            for (int i = 1; i < nodeIDs.Count - 1; i++)
            {
                Vector3[] vertices = BuildNode(nodeIDs[i], segmentIDs[i-1]);
                vertexList.Add(vertices);
            }

            if (cSendIntersection == null) // dead end
            {
                Vector3[] vertices = BuildNode(nodeIDs.Last(), segmentIDs.Last());
                vertexList.Add(vertices);
            }
            else
            {

                // get vertices from end intersection
                Debug.Log("Retrieving end intersection...");
                SortedDictionary<ushort, Vector2[]> intersectionEnd = cSendIntersection.ConnectionPoints;
                Vector2[] pointsEnd2d = intersectionEnd[segmentIDs.Last()];
                Vector3[] endIntersection = new Vector3[2];
                endIntersection[0] = new Vector3(pointsEnd2d[0].y, netMan.m_nodes.m_buffer[nodeIDs.Last()].m_position.y, pointsEnd2d[0].x);
                endIntersection[1] = new Vector3(pointsEnd2d[1].y, netMan.m_nodes.m_buffer[nodeIDs.Last()].m_position.y, pointsEnd2d[1].x);
                vertexList.Add(endIntersection);
            }

            Room[] segmentRooms = new Room[1];

            // now build block
            Debug.Log("Creating block vertices");
            Vertex[] blockVertices = new Vertex[vertexList.Count * 4];
            List<PerimeterPoint> perimeterPoints = new List<PerimeterPoint>();

            // fill vertex buffer
            for (int i = 0; i < vertexList.Count; i++)
            {
                Debug.Log("Filling vertex buffer..");

                blockVertices[4 * i] = new Vertex(vertexList[i][1].z, vertexList[i][1].y, vertexList[i][1].x) * UniversalProperties.CONVERSION_SCALE;
                blockVertices[4 * i + 3] = new Vertex(vertexList[i][0].z, vertexList[i][0].y, vertexList[i][0].x) * UniversalProperties.CONVERSION_SCALE;

                blockVertices[4 * i + 1] = blockVertices[4 * i] * (1.0f - UniversalProperties.SIDEWALK_OFFSET) + blockVertices[4 * i + 3] * UniversalProperties.SIDEWALK_OFFSET;
                blockVertices[4 * i + 2] = blockVertices[4 * i] * UniversalProperties.SIDEWALK_OFFSET + blockVertices[4 * i + 3] * (1.0f - UniversalProperties.SIDEWALK_OFFSET);


                blockVertices[4 * i].y += UniversalProperties.SIDEWALK_HEIGHT;
                blockVertices[4 * i + 3].y += UniversalProperties.SIDEWALK_HEIGHT;

                Debug.Log("Setting up perimeter points...");

                Room neighbor = null;

                if (i == 0)
                {
                    neighbor = cSstartIntersection.Room;
                    PerimeterPoint innerLeftPoint = new PerimeterPoint(blockVertices[4 * i + 1], neighbor);
                    PerimeterPoint innerRightPoint = new PerimeterPoint(blockVertices[4 * i + 2], neighbor);

                    // we need duplicates for multiple connections
                    PerimeterPoint outerLeftPoint = new PerimeterPoint(blockVertices[4 * i], neighbor);
                    PerimeterPoint outerRightPoint = new PerimeterPoint(blockVertices[4 * i + 3], neighbor);

                    perimeterPoints.Add(innerRightPoint);
                    perimeterPoints.Insert(0, innerLeftPoint);
                    perimeterPoints.Add(outerRightPoint);
                    perimeterPoints.Insert(0, outerLeftPoint);

                    startPerimeter.Add(innerLeftPoint);
                    startPerimeter.Insert(0, innerRightPoint);
                    startPerimeter.Add(outerLeftPoint);
                    startPerimeter.Insert(0, outerRightPoint);
                }
                else if (i == vertexList.Count - 1 && cSendIntersection != null)
                {
                    // last segment and no dead end
                    neighbor = cSendIntersection.Room;
                }


                PerimeterPoint leftPoint = new PerimeterPoint(blockVertices[4 * i], null);
                PerimeterPoint rightPoint = new PerimeterPoint(blockVertices[4 * i + 3], null);

                // Insert left points in opposite order
                perimeterPoints.Add(rightPoint);
                perimeterPoints.Insert(0, leftPoint);

                // opposite direction so we can traverse them in correct order for adjacent rooms
                leftPerimeter.Add(leftPoint);
                rightPerimeter.Insert(0, rightPoint);

                if (i == vertexList.Count - 1)
                {
                    PerimeterPoint outerLeftPoint = new PerimeterPoint(blockVertices[4 * i], neighbor);
                    PerimeterPoint outerRightPoint = new PerimeterPoint(blockVertices[4 * i + 3], neighbor);
                    PerimeterPoint innerLeftPoint = new PerimeterPoint(blockVertices[4 * i + 1], neighbor);
                    PerimeterPoint innerRightPoint = new PerimeterPoint(blockVertices[4 * i + 2], neighbor);

                    perimeterPoints.Add(outerRightPoint);
                    perimeterPoints.Insert(0, outerLeftPoint);
                    perimeterPoints.Add(innerRightPoint);
                    perimeterPoints.Insert(0, innerLeftPoint);

                    endPerimeter.Add(outerLeftPoint);
                    endPerimeter.Add(innerLeftPoint);
                    endPerimeter.Add(innerRightPoint);
                    endPerimeter.Add(outerRightPoint);
                }

            }

            Debug.Log("Creating road element...");
            RoadElement[] roadArray = new RoadElement[1];
            roadArray[0] = new RoadElement("r2_f", "swalk_f", "r2_lo_f", blockVertices);

            Debug.Log("Creating room...");
            Room room = new Room(roadArray, perimeterPoints, 0, RoomFlags.Road);

            // setup intersection perimeters
            List<PerimeterPoint> startPerimeters = cSstartIntersection.PerimeterPoints[segmentIDs[0]];


            foreach (PerimeterPoint pp in startPerimeters)
            {
                pp.ConnectedRoom = room;
            }

            if (cSendIntersection != null)
            {
                List<PerimeterPoint> endPerimeters = cSendIntersection.PerimeterPoints[segmentIDs.Last()];
                foreach (PerimeterPoint pp in endPerimeters)
                {
                    pp.ConnectedRoom = room;
                }
            }

            this.room = room;
        }

        public List<PerimeterPoint> GetLeftPerimeter(ushort startIntersection, ushort startSegment)
        {
            if (startIntersection == nodeIDs[0] && startSegment == segmentIDs[0])
            {
                return leftPerimeter;
            }
            else if (startIntersection == nodeIDs.Last() && startSegment == segmentIDs.Last())
            {
                return rightPerimeter;
            }

            throw new Exception("startSegment is not located at either end of the road!");
        }

        public List<PerimeterPoint> GetRightPerimeter(ushort startIntersection, ushort startSegment)
        {
            if (startIntersection == nodeIDs[0] && startSegment == segmentIDs[0])
            {
                return rightPerimeter;
            }
            else if (startIntersection == nodeIDs.Last() && startSegment == segmentIDs.Last())
            {
                return leftPerimeter;
            }

            throw new Exception("startSegment is not located at either end of the road!");
        }

        public List<Vertex> TraverseBoundary(ushort startIntersection, ushort startSegment, bool leftside = true, bool turnAroundOnBridgeOrTunnel = true)
        {
            List<Vertex> leftBoundaryVertices = new List<Vertex>();
            int index;
            int step;
            RoadElement road = (RoadElement)room.FindElementOfType<RoadElement>();
            bool hasDeadEnd;
            ushort endIntersection;
            ushort endSegment;

            if (startIntersection == nodeIDs[0] && startSegment == segmentIDs[0])
            {
                index = leftside ? 0 : 3;
                step = 4; // TODO: needs to be adapted for divided roads
                hasDeadEnd = cSendIntersection == null;
                endIntersection = nodeIDs.Last();
                endSegment = segmentIDs.Last();
            }
            else if(startIntersection == nodeIDs.Last() && startSegment == segmentIDs.Last())
            {
                index = leftside ? road.GetVertexCount() - 1 : road.GetVertexCount() - 4;
                step = -4; // TODO: needs to be adapted for divided roads
                hasDeadEnd = cSstartIntersection == null;
                endIntersection = nodeIDs[0];
                endSegment = segmentIDs[0];
            }
            else
            {
                throw new Exception("startSegment is not located at either end of the road!");
            }

            while(index < road.GetVertexCount() && index >= 0)
            {
                leftBoundaryVertices.Add(road.GetVertex(index));
                index += step;
            }

            if(hasDeadEnd)
            {
                // restore last valid index
                index -= step;
                // add the two vertices inbetween
                if(index % Math.Abs(step) == 0)
                {
                    // need to add to index
                    leftBoundaryVertices.Add(road.GetVertex(index + 1));
                    leftBoundaryVertices.Add(road.GetVertex(index + 2));
                }
                else
                {
                    leftBoundaryVertices.Add(road.GetVertex(index - 1));
                    leftBoundaryVertices.Add(road.GetVertex(index - 2));
                }

                List<Vertex> oppositeDirection = TraverseBoundary(endIntersection, endSegment, leftside, false);
                leftBoundaryVertices.AddRange(oppositeDirection);
            }

            return leftBoundaryVertices;
        }

        public Vector3[] BuildNode(ushort nodeIndex, ushort segIndex)
        {
            NetNode node = netMan.m_nodes.m_buffer[nodeIndex];
            List<ushort> segments = RoadUtils.GetAllAdjacentSegments(node, NetInfo.LaneType.Vehicle);

            Vector3 averageDir = new Vector3(0.0f, 0.0f, 0.0f);

            float cornerFactor = 1.0f;

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
                averageDir.y = 0.0f;
                averageDir.Normalize();

                cornerFactor = 1.0f / Vector3.Dot(dir0, averageDir);
            }
            else
            {
                Vector3 dir0 = netMan.m_segments.m_buffer[segments[0]].GetDirection(nodeIndex);
                averageDir += dir0;
                averageDir.y = 0.0f;
                averageDir.Normalize();
            }

            Vector3 normalInPlane = new Vector3(-averageDir.z, 0.0f, averageDir.x);

            Vector3[] points = new Vector3[2];
            points[0] = netMan.m_nodes.m_buffer[nodeIndex].m_position + UniversalProperties.HALFROAD_WIDTH * cornerFactor * normalInPlane;
            points[1] = netMan.m_nodes.m_buffer[nodeIndex].m_position - UniversalProperties.HALFROAD_WIDTH * cornerFactor * normalInPlane;

            return points;
        }

        public bool HasDeadEnd()
        {
            return cSstartIntersection == null || cSendIntersection == null;
        }
    }
}
