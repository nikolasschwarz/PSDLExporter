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
        public readonly float[] profile;
        public string style;
        private string roadTextureName;
        private string roadTextureNameLod;

        private static NetManager netMan = Singleton<NetManager>.instance;

        public Room Room { get => room; }
        public List<ushort> SegmentIDs { get => segmentIDs; }
        public List<ushort> NodeIDs { get => nodeIDs; }
        internal CSIntersection CSstartIntersection { get => cSstartIntersection; set => cSstartIntersection = value; }
        internal CSIntersection CSendIntersection { get => cSendIntersection; set => cSendIntersection = value; }

        public CSRoad(ushort startIntersectionID, ushort startSegmentID, SortedDictionary<ushort, CSIntersection> intersections, string style)
        {
            cSstartIntersection = intersections[startIntersectionID];
            this.style = style;
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

            // TODO: divide road into sections depending on the profile
            profile = RoadAnalyzer.RoadProfile(startSegmentID);
            int laneCount = RoadAnalyzer.CountCarLanes(startSegmentID);
            bool isOneway = RoadAnalyzer.IsOneway(startSegmentID);

            if (RoadAnalyzer.IsAsymmetric(startSegmentID))
            {
                roadTextureName = "transparent";

                // LOD not correct but let's see if is noticeable in-game
                roadTextureNameLod = RoadAnalyzer.DetermineRoadTexture((laneCount / 2) * 2, isOneway, style, true);
            }
            else
            {
                roadTextureName = RoadAnalyzer.DetermineRoadTexture(laneCount, isOneway, style);
                roadTextureNameLod = RoadAnalyzer.DetermineRoadTexture(laneCount, isOneway, style, true);
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
            int verticesPerNode = profile.Length;
            Vertex[] blockVertices = new Vertex[vertexList.Count * verticesPerNode];
            List<PerimeterPoint> perimeterPoints = new List<PerimeterPoint>();

            // fill vertex buffer
            for (int i = 0; i < vertexList.Count; i++)
            {
                Debug.Log("Filling vertex buffer..");

                blockVertices[verticesPerNode * i] =
                    new Vertex(vertexList[i][1].z, vertexList[i][1].y, vertexList[i][1].x) * UniversalProperties.CONVERSION_SCALE;
                blockVertices[verticesPerNode * i + verticesPerNode - 1] =
                    new Vertex(vertexList[i][0].z, vertexList[i][0].y, vertexList[i][0].x) * UniversalProperties.CONVERSION_SCALE;

                Vertex leftToRightVector = blockVertices[verticesPerNode * i + verticesPerNode - 1] - blockVertices[verticesPerNode * i];

                // calculate all points from profile

                for (int j = 1; j < verticesPerNode - 1; j++)
                {
                    float relativeOffset = (profile[j] - profile[0]) / (profile.Last() - profile[0]);

                    blockVertices[verticesPerNode * i + j] =
                        blockVertices[verticesPerNode * i] + leftToRightVector * relativeOffset;
                }

                // add sidewalk height if sidewalk exists
                if (profile[0] != profile[1]) blockVertices[verticesPerNode * i].y += UniversalProperties.SIDEWALK_HEIGHT;
                if (profile[verticesPerNode - 1] != profile[verticesPerNode - 2]) blockVertices[verticesPerNode * i + verticesPerNode - 1].y += UniversalProperties.SIDEWALK_HEIGHT;

                Debug.Log("Setting up perimeter points...");

                Room neighbor = null;

                if (i == 0)
                {
                    neighbor = cSstartIntersection.Room;
                    PerimeterPoint innerLeftPoint = new PerimeterPoint(blockVertices[verticesPerNode * i + 1], neighbor);
                    PerimeterPoint innerRightPoint = new PerimeterPoint(blockVertices[verticesPerNode * i + verticesPerNode - 2], neighbor);

                    // we need duplicates for multiple connections
                    PerimeterPoint outerLeftPoint = new PerimeterPoint(blockVertices[verticesPerNode * i], neighbor);
                    PerimeterPoint outerRightPoint = new PerimeterPoint(blockVertices[verticesPerNode * i + verticesPerNode - 1], neighbor);

                    PerimeterPoint medianRight = null;
                    PerimeterPoint medianLeft = null;

                    if (verticesPerNode == 6)
                    {
                        medianRight = new PerimeterPoint(blockVertices[verticesPerNode * i + verticesPerNode - 3], neighbor);
                        medianLeft = new PerimeterPoint(blockVertices[verticesPerNode * i + 2], neighbor);
                    }

                    if (verticesPerNode == 6)
                    {
                        perimeterPoints.Add(medianRight);
                        perimeterPoints.Insert(0, medianLeft);
                    }

                    perimeterPoints.Add(innerRightPoint);
                    perimeterPoints.Insert(0, innerLeftPoint);
                    perimeterPoints.Add(outerRightPoint);
                    perimeterPoints.Insert(0, outerLeftPoint);

                    if (verticesPerNode == 6)
                    {
                        startPerimeter.Add(medianLeft);
                        startPerimeter.Insert(0, medianRight);
                    }

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


                PerimeterPoint leftPoint = new PerimeterPoint(blockVertices[verticesPerNode * i], null);
                PerimeterPoint rightPoint = new PerimeterPoint(blockVertices[verticesPerNode * i + verticesPerNode - 1], null);

                // Insert left points in opposite order
                perimeterPoints.Add(rightPoint);
                perimeterPoints.Insert(0, leftPoint);

                // opposite direction so we can traverse them in correct order for adjacent rooms
                leftPerimeter.Add(leftPoint);
                rightPerimeter.Insert(0, rightPoint);

                if (i == vertexList.Count - 1)
                {
                    PerimeterPoint outerLeftPoint = new PerimeterPoint(blockVertices[verticesPerNode * i], neighbor);
                    PerimeterPoint outerRightPoint = new PerimeterPoint(blockVertices[verticesPerNode * i + verticesPerNode - 1], neighbor);
                    PerimeterPoint innerLeftPoint = new PerimeterPoint(blockVertices[verticesPerNode * i + 1], neighbor);
                    PerimeterPoint innerRightPoint = new PerimeterPoint(blockVertices[verticesPerNode * i + verticesPerNode - 2], neighbor);

                    PerimeterPoint medianRight = null;
                    PerimeterPoint medianLeft = null;

                    if(verticesPerNode == 6)
                    {
                        medianRight = new PerimeterPoint(blockVertices[verticesPerNode * i + verticesPerNode - 3], neighbor);
                        medianLeft = new PerimeterPoint(blockVertices[verticesPerNode * i + 2], neighbor);
                    }

                    perimeterPoints.Add(outerRightPoint);
                    perimeterPoints.Insert(0, outerLeftPoint);
                    perimeterPoints.Add(innerRightPoint);
                    perimeterPoints.Insert(0, innerLeftPoint);

                    if (verticesPerNode == 6)
                    {
                        perimeterPoints.Add(medianRight);
                        perimeterPoints.Insert(0, medianLeft);
                    }

                    endPerimeter.Add(outerLeftPoint);
                    endPerimeter.Add(innerLeftPoint);

                    if (verticesPerNode == 6)
                    {
                        endPerimeter.Add(medianLeft);
                        endPerimeter.Add(medianRight);                      
                    }

                    endPerimeter.Add(innerRightPoint);
                    endPerimeter.Add(outerRightPoint);
                }

            }

            Debug.Log("Creating road element...");
            ISDLElement[] roadArray = new ISDLElement[1];

            if (profile.Length == 4)
            {
                roadArray[0] = new RoadElement(roadTextureName,
                    RoadAnalyzer.SidewalkTexture(style)/*"swalk_f"*/, roadTextureNameLod, blockVertices);
            }
            else
            {
                DividedRoadElement dividedRoad = new DividedRoadElement();
                dividedRoad.Textures = new string[3];

                Debug.Log("Set vertices...");
                dividedRoad.Vertices = blockVertices.ToList();
                Debug.Log("Set Surface...");
                dividedRoad.SetTexture(RoadTextureType.Surface, roadTextureName);
                Debug.Log("Set LOD...");
                dividedRoad.SetTexture(RoadTextureType.LOD, roadTextureNameLod);
                Debug.Log("Set Sidewalk...");
                dividedRoad.SetTexture(RoadTextureType.Sidewalk, RoadAnalyzer.SidewalkTexture(style)); // TODO: adjust style
                //Debug.Log("Set Cap...");
                //dividedRoad.SetDividerTexture(DividerTextureType.Cap, "rwalk");
                Debug.Log("Set Side...");
                dividedRoad.SetDividerTexture(DividerTextureType.Side, "rwalk");
                Debug.Log("Set SideStrips...");
                dividedRoad.SetDividerTexture(DividerTextureType.SideStrips, "rwalk");
                Debug.Log("Set Top...");
                dividedRoad.SetDividerTexture(DividerTextureType.Top, "rwalk");
                Debug.Log("Set Elevated...");
                dividedRoad.DividerType = DividerType.Elevated;
                Debug.Log("Set DividerFlags...");
                dividedRoad.DividerFlags = DividerFlags.ClosedStart | DividerFlags.ClosedEnd; // TODO: change when we create multi-segments
                Debug.Log("Set all.");

                dividedRoad.Value = (ushort)Mathf.RoundToInt(UniversalProperties.SIDEWALK_HEIGHT * 256);

                roadArray[0] = dividedRoad;
            }

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

            if (RoadAnalyzer.IsAsymmetric(segmentIDs[0]))
            {
                OverlayAsymmetricRoad();
            }
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

        public List<PerimeterPoint> TraversePerimeter(ushort startIntersection, ushort startSegment, bool leftside = true)
        {
            int index;
            int step;
            bool hasDeadEnd;
            List<PerimeterPoint> relativeLeftPerimeter;
            List<PerimeterPoint> relativeRightPerimeter;
            List<PerimeterPoint> perimeterTraversal = new List<PerimeterPoint>();

            if (startIntersection == nodeIDs[0] && startSegment == segmentIDs[0])
            {
                index = leftside ? 0 : rightPerimeter.Count - 1;
                step = leftside ? 1 : -1; // adapted for divided roads
                hasDeadEnd = cSendIntersection == null;

                relativeLeftPerimeter = leftPerimeter;
                relativeRightPerimeter = rightPerimeter;
            }
            else if (startIntersection == nodeIDs.Last() && startSegment == segmentIDs.Last())
            {
                index = leftside ? 0 : rightPerimeter.Count - 1;
                step = leftside ? 1 : -1; // adapted for divided roads
                hasDeadEnd = cSstartIntersection == null;

                relativeLeftPerimeter = rightPerimeter;
                relativeRightPerimeter = leftPerimeter;
            }
            else
            {
                throw new Exception("startSegment is not located at either end of the road!");
            }


            return perimeterTraversal;

        }

        public bool TraverseRoadVertices(ref int index, int step, List<Vertex> roadVertices, List<Vertex> collectedVertices, bool turnAroundOnBridgeOrTunnel = true)
        {
            RoadAnalyzer.ElevationType elevation;

            while (index < roadVertices.Count && index >= 0)
            {
                collectedVertices.Add(roadVertices[index]);

                if (turnAroundOnBridgeOrTunnel)
                {
                    int nodeIndex = index / profile.Length;
                    int followingSegmentIndex = step > 0 ? nodeIndex : (nodeIndex - 1);

                    if (followingSegmentIndex >= 0 && followingSegmentIndex < segmentIDs.Count)
                    {

                        // account for tunnels and bridges
                        elevation = RoadAnalyzer.DetermineElevationType(segmentIDs[followingSegmentIndex]);
                        if (elevation != RoadAnalyzer.ElevationType.Standard)
                        {
                            index += step; // index must be a step ahead of the last valid one
                            return true;
                        }

                    } // else we have reached the very last node with no following segment
                }

                
                index += step;         
            }

            return false;
        }

        public List<Vertex> TraverseBoundary(ushort startIntersection, ushort startSegment, bool leftside = true, bool turnAroundOnBridgeOrTunnel = true)
        {
            List<Vertex> leftBoundaryVertices = new List<Vertex>();
            int index;
            int step;
            List<Vertex> roadVertices;

            if (profile.Length == 4)
            {
                RoadElement road = (RoadElement)room.FindElementOfType<RoadElement>();
                roadVertices = road.Vertices;
            }
            else
            {
                DividedRoadElement road = (DividedRoadElement)room.FindElementOfType<DividedRoadElement>();
                roadVertices = road.Vertices;
            }
            bool hasDeadEnd;
            ushort endIntersection;
            ushort endSegment;

            if (startIntersection == nodeIDs[0] && startSegment == segmentIDs[0])
            {
                index = leftside ? 0 : profile.Length - 1;
                step = profile.Length; // adapted for divided roads
                hasDeadEnd = cSendIntersection == null;
                endIntersection = nodeIDs.Last();
                endSegment = segmentIDs.Last();
            }
            else if(startIntersection == nodeIDs.Last() && startSegment == segmentIDs.Last())
            {
                index = leftside ? roadVertices.Count - 1 : roadVertices.Count - profile.Length;
                step = -profile.Length; // adapted for divided roads
                hasDeadEnd = cSstartIntersection == null;
                endIntersection = nodeIDs[0];
                endSegment = segmentIDs[0];
            }
            else
            {
                throw new Exception("startSegment is not located at either end of the road!");
            }

            /*RoadAnalyzer.ElevationType elevation;
            bool turnAround = false;

            while (index < roadVertices.Count && index >= 0)
            {
                leftBoundaryVertices.Add(roadVertices[index]);
                index += step;

                if (turnAroundOnBridgeOrTunnel)
                {
                    int nodeIndex = index / profile.Length;
                    int followingSegmentIndex = step > 0 ? nodeIndex : (nodeIndex - 1);

                    // account for tunnels and bridges
                    elevation = RoadAnalyzer.DetermineElevationType(segmentIDs[followingSegmentIndex]);
                    if (elevation != RoadAnalyzer.ElevationType.Standard)
                    {
                        turnAround = true;
                        break;
                    }
                }
            }*/
            bool turnAround = TraverseRoadVertices(ref index, step, roadVertices, leftBoundaryVertices, turnAroundOnBridgeOrTunnel);

            if(hasDeadEnd || turnAround)
            {
                // restore last valid index
                index -= step;
                // add the vertices inbetween, TODO: adjust for bridges and tunnels to construct entrance or boundary between bridge and grass
                if(index % Math.Abs(step) == 0)
                {
                    // need to add to index
                    leftBoundaryVertices.Add(roadVertices[index + 1]);
                    leftBoundaryVertices.Add(roadVertices[index + 2]);

                    if(profile.Length == 6)
                    {
                        leftBoundaryVertices.Add(roadVertices[index + 3]);
                        leftBoundaryVertices.Add(roadVertices[index + 4]);

                        index += 5;
                    }
                    else
                    {
                        index += 3;
                    }
                }
                else
                {
                    leftBoundaryVertices.Add(roadVertices[index - 1]);
                    leftBoundaryVertices.Add(roadVertices[index - 2]);

                    if (profile.Length == 6)
                    {
                        leftBoundaryVertices.Add(roadVertices[index - 3]);
                        leftBoundaryVertices.Add(roadVertices[index - 4]);

                        index -= 5;
                    }
                    else
                    {
                        index -= 3;
                    }
                }

                /*List<Vertex> oppositeDirection = TraverseBoundary(endIntersection, endSegment, leftside, false);
                leftBoundaryVertices.AddRange(oppositeDirection);*/
                TraverseRoadVertices(ref index, -step, roadVertices, leftBoundaryVertices);
            }

            return leftBoundaryVertices;
        }

        private void CountLanes(ref int r, ref int l)
        {
            // figure out orientation of first segment
            int orientation = 1;
            NetSegment startSegment = RoadUtils.GetNetSegment(segmentIDs[0]);

            if (startSegment.m_startNode != nodeIDs[0])
            {
                orientation *= -1;
            }

            /*if ((startSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
            {
                orientation *= -1;
            }*/

            // TODO: is this correct for left-hand traffic?
            if (orientation == 1)
            {
                startSegment.CountLanes(segmentIDs[0], NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.All, ref r, ref l);
            }
            else
            {
                startSegment.CountLanes(segmentIDs[0], NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.All, ref l, ref r);
            }
        }

        private void OverlayAsymmetricRoad()
        {
            if(profile.Length == 6)
            {
                OverlayAsymmetricDividedRoad();
                return;
            }

            int l = 0;
            int r = 0;

            CountLanes(ref r, ref l);

            float centerFrac = l / (float)(l + r);

            RoadElement road = (RoadElement)room.FindElementOfType<RoadElement>();

            Vertex[] rightAlleyVertices = new Vertex[road.Vertices.Count / 2];

            for (int i = 0; i < road.Vertices.Count / 4; i++)
            {
                rightAlleyVertices[2 * i] = road.Vertices[4 * i + 1] * (1 - centerFrac) + road.Vertices[4 * i + 2] * centerFrac;
                rightAlleyVertices[2 * i + 1] = road.Vertices[4 * i + 2];
            }

            WalkwayElement rightSide = new WalkwayElement(RoadAnalyzer.DetermineRoadTexture(2 * r, false, style), rightAlleyVertices);

            Vertex[] leftAlleyVertices = new Vertex[road.Vertices.Count / 2];

            for (int i = 0; i < road.Vertices.Count / 4; i++)
            {
                leftAlleyVertices[2 * i] = road.Vertices[road.Vertices.Count - 4 * i - 2] * centerFrac + road.Vertices[road.Vertices.Count - 4 * i - 3] * (1 - centerFrac);
                leftAlleyVertices[2 * i + 1] = road.Vertices[road.Vertices.Count - 4 * i - 3];
            }

            WalkwayElement leftSide = new WalkwayElement(RoadAnalyzer.DetermineRoadTexture(2 * l, false, style), leftAlleyVertices);

            room.Elements.Add(rightSide);
            room.Elements.Add(leftSide);
        }

        private void OverlayAsymmetricDividedRoad()
        {
            int l = 0;
            int r = 0;

            CountLanes(ref r, ref l);

            DividedRoadElement dividedRoad = (DividedRoadElement)room.FindElementOfType<DividedRoadElement>();

            Vertex[] rightAlleyVertices = new Vertex[dividedRoad.Vertices.Count / 3];

            for(int i = 0; i < dividedRoad.Vertices.Count / 6; i++)
            {
                rightAlleyVertices[2 * i] = dividedRoad.Vertices[6 * i + 3];
                rightAlleyVertices[2 * i + 1] = dividedRoad.Vertices[6 * i + 4];
            }

            WalkwayElement rightSide = new WalkwayElement(RoadAnalyzer.DetermineRoadTexture(2 * r, false, style), rightAlleyVertices);

            Vertex[] leftAlleyVertices = new Vertex[dividedRoad.Vertices.Count / 3];

            for (int i = 0; i < dividedRoad.Vertices.Count / 6; i++)
            {
                leftAlleyVertices[2 * i] = dividedRoad.Vertices[dividedRoad.Vertices.Count - 6 * i - 4];
                leftAlleyVertices[2 * i + 1] = dividedRoad.Vertices[dividedRoad.Vertices.Count - 6 * i - 5];
            }

            WalkwayElement leftSide = new WalkwayElement(RoadAnalyzer.DetermineRoadTexture(2 * l, false, style), leftAlleyVertices);

            room.Elements.Add(rightSide);
            room.Elements.Add(leftSide);

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
            points[0] = netMan.m_nodes.m_buffer[nodeIndex].m_position + profile.Last() * cornerFactor * normalInPlane;
            points[1] = netMan.m_nodes.m_buffer[nodeIndex].m_position + profile[0] * cornerFactor * normalInPlane;

            return points;
        }

        public bool HasDeadEnd()
        {
            return cSstartIntersection == null || cSendIntersection == null;
        }
    }
}
