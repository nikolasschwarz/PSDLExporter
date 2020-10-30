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
    /// <summary>
    /// Represents an intersection from Cities Skylines and stores additional generated information
    /// </summary>
    class CSIntersection
    {

        private SortedDictionary<ushort, Vector2[]> crosswalkConnectionPoints;
        private SortedDictionary<ushort, Vector2[]> connectionPoints; // map segment to connection points
        private SortedDictionary<ushort, List<PerimeterPoint>> roadPerimeterPoints; // map segment to its perimeter points
        private Dictionary<KeyValuePair<ushort, ushort>, List<PerimeterPoint>> terrainPerimeterPoints; // map segment to its perimeter points
        private SortedDictionary<float, ushort> adjacentSegments; // map angle to segment (allows to find neighboring roads)
        private Vector2[] meetingPoints;
        private float[][] roadProfiles;

        private Room room;
        private ushort nodeID;
        private ushort intersectionNo;
        public string style;

        static private NetManager netMan = Singleton<NetManager>.instance;

        public CSIntersection(ushort nodeID, ushort intersectionNo, string style)
        {
            this.nodeID = nodeID;
            this.intersectionNo = intersectionNo;
            this.style = style;
        }

        public NetNode GetNetNode()
        {
            return netMan.m_nodes.m_buffer[nodeID];
        }

        public void CalculateAdjacentSegments()
        {
            // segments sorted by angle
            adjacentSegments = new SortedDictionary<float, ushort>();

            if ((netMan.m_segments.m_buffer[GetNetNode().m_segment0].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {

                AddSegment(GetNetNode().m_segment0);
            }

            if ((netMan.m_segments.m_buffer[GetNetNode().m_segment1].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(GetNetNode().m_segment1);
            }

            if ((netMan.m_segments.m_buffer[GetNetNode().m_segment2].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(GetNetNode().m_segment2);
            }

            if ((netMan.m_segments.m_buffer[GetNetNode().m_segment3].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(GetNetNode().m_segment3);
            }

            if ((netMan.m_segments.m_buffer[GetNetNode().m_segment4].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(GetNetNode().m_segment4);
            }

            if ((netMan.m_segments.m_buffer[GetNetNode().m_segment5].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(GetNetNode().m_segment5);
            }

            if ((netMan.m_segments.m_buffer[GetNetNode().m_segment6].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(GetNetNode().m_segment6);
            }

            if ((netMan.m_segments.m_buffer[GetNetNode().m_segment7].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                AddSegment(GetNetNode().m_segment7);
            }
        }

        public void CalculateConnectionPoints()
        {
          
            //Debug.Assert(adjacentSegments.Count == node.CountSegments());

            // sort by angle and intersect each segment with its neighbor
            KeyValuePair<float, ushort>[] adjacentSegmentsArray = adjacentSegments.ToArray();
            meetingPoints = new Vector2[adjacentSegmentsArray.Length];
            connectionPoints = new SortedDictionary<ushort, Vector2[]>();
            roadProfiles = new float[adjacentSegmentsArray.Length][];

            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                Vector2[] connection = new Vector2[2];
                connectionPoints.Add(adjacentSegmentsArray[i].Value, connection);
                roadProfiles[i] = RoadAnalyzer.RoadProfile(adjacentSegmentsArray[i].Value);
            }

            // TODO: probably no longer needed
            Vector2[] roadBounds = new Vector2[2 * adjacentSegmentsArray.Length];
            

            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                if (i != adjacentSegmentsArray.Length - 1)
                {
                    Debug.Assert(adjacentSegmentsArray[i].Key < adjacentSegmentsArray[i + 1].Key);
                }

                // intersect pair
                Vector2 dirA2d = GetStraightDirection(adjacentSegmentsArray[i].Value, nodeID);
                dirA2d.Normalize();
                Vector2 normA = new Vector2(dirA2d.y, -dirA2d.x);

                Vector2 dirB2d = GetStraightDirection(adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value, nodeID);
                dirB2d.Normalize();
                Vector2 normB = new Vector2(dirB2d.y, -dirB2d.x);

                

                Vector2 supportA = new Vector2(GetNetNode().m_position.z, GetNetNode().m_position.x) + (roadProfiles[i][0] * normA);
                Vector2 supportB = new Vector2(GetNetNode().m_position.z, GetNetNode().m_position.x) + (roadProfiles[(i + 1) % adjacentSegmentsArray.Length].Last() * normB);

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

                // make intersections smooth
                connectionPoints[adjacentSegmentsArray[i].Value][1] = meetingPoints[i] + UniversalProperties.INTERSECTION_OFFSET * dirA2d;
                connectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0] = meetingPoints[i] + UniversalProperties.INTERSECTION_OFFSET * dirB2d;

            }

            crosswalkConnectionPoints = new SortedDictionary<ushort, Vector2[]>();

            // adjust connection points so crosswalks cross orthogonally
            foreach (KeyValuePair<ushort, Vector2[]> connectionPoint in connectionPoints)
            {
                Vector2 roadDir = GetStraightDirection(connectionPoint.Key, nodeID);
                roadDir.Normalize();

                float factor0 = Vector2.Dot(roadDir, connectionPoint.Value[0]);
                float factor1 = Vector2.Dot(roadDir, connectionPoint.Value[1]);

                if(factor0 < factor1)
                {
                    connectionPoint.Value[0] += (factor1 - factor0) * roadDir;
                }
                else if (factor0 > factor1)
                {
                    connectionPoint.Value[1] += (factor0 - factor1) * roadDir;
                }

                Vector2[] crosswalkConnectionVertices = new Vector2[2];

                crosswalkConnectionVertices[0] = connectionPoint.Value[0];
                crosswalkConnectionVertices[1] = connectionPoint.Value[1];
                crosswalkConnectionPoints.Add(connectionPoint.Key, crosswalkConnectionVertices);

                connectionPoint.Value[0] = connectionPoint.Value[0] + (UniversalProperties.CROSSWALK_WIDTH / UniversalProperties.CONVERSION_SCALE) * roadDir;
                connectionPoint.Value[1] = connectionPoint.Value[1] + (UniversalProperties.CROSSWALK_WIDTH / UniversalProperties.CONVERSION_SCALE) * roadDir;
            }
        }

        public void BuildIntersectionRoom()
        {
            KeyValuePair<float, ushort>[] adjacentSegmentsArray = adjacentSegments.ToArray();
            List<ISDLElement> intersectionElements = new List<ISDLElement>();
            List<Vertex> innerIntersection = new List<Vertex>();
            List<PerimeterPoint> intersectionPerimeterPoints = new List<PerimeterPoint>();
            roadPerimeterPoints = new SortedDictionary<ushort, List<PerimeterPoint>>();
            terrainPerimeterPoints = new Dictionary<KeyValuePair<ushort, ushort>, List<PerimeterPoint>>();

            // construct sidewalk meshes.
            Debug.Log("Construct sidewalks...");

            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                // use quadratic bezier curve to connect the two points smoothly with the meeting point as support
                Debug.Log("Construct border points...");
                Vector2[] outerSidewalkPoints = new Vector2[7];

                outerSidewalkPoints[0] = new Vector2(connectionPoints[adjacentSegmentsArray[i].Value][1].x, connectionPoints[adjacentSegmentsArray[i].Value][1].y);
                outerSidewalkPoints[1] = new Vector2(crosswalkConnectionPoints[adjacentSegmentsArray[i].Value][1].x, crosswalkConnectionPoints[adjacentSegmentsArray[i].Value][1].y);

                outerSidewalkPoints[5] = new Vector2(crosswalkConnectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0].x,
                    crosswalkConnectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0].y);
                outerSidewalkPoints[6] = new Vector2(connectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0].x,
                    connectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0].y);

                outerSidewalkPoints[2] = QuadraticBezier(crosswalkConnectionPoints[adjacentSegmentsArray[i].Value][1], meetingPoints[i],
                    crosswalkConnectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0], 0.25f);
                outerSidewalkPoints[3] = QuadraticBezier(crosswalkConnectionPoints[adjacentSegmentsArray[i].Value][1], meetingPoints[i],
                    crosswalkConnectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0], 0.5f);
                outerSidewalkPoints[4] = QuadraticBezier(crosswalkConnectionPoints[adjacentSegmentsArray[i].Value][1], meetingPoints[i],
                    crosswalkConnectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0], 0.75f);

                // TODO: are concave shapes permitted?!


                Vector2[] sidewalkRoadBorderPoints = new Vector2[7];

                Vector2 crossingDir0 = connectionPoints[adjacentSegmentsArray[i].Value][0] - connectionPoints[adjacentSegmentsArray[i].Value][1];

                float relativeOffset0 = (roadProfiles[i].Last() - roadProfiles[i][roadProfiles[i].Length - 2]) / (roadProfiles[i].Last() - roadProfiles[i][0]);
                sidewalkRoadBorderPoints[0] = connectionPoints[adjacentSegmentsArray[i].Value][1] + relativeOffset0 * crossingDir0;
                sidewalkRoadBorderPoints[1] = crosswalkConnectionPoints[adjacentSegmentsArray[i].Value][1] + relativeOffset0 * crossingDir0;

                Vector2 crossingDir1 = connectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][1]
                    - connectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0];

                float relativeOffset1 = (roadProfiles[(i + 1) % adjacentSegmentsArray.Length][1] - roadProfiles[(i + 1) % adjacentSegmentsArray.Length][0])
                    / (roadProfiles[(i + 1) % adjacentSegmentsArray.Length].Last() - roadProfiles[(i + 1) % adjacentSegmentsArray.Length][0]);


                sidewalkRoadBorderPoints[5] = crosswalkConnectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0] + relativeOffset1 * crossingDir1;
                sidewalkRoadBorderPoints[6] = connectionPoints[adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value][0] + relativeOffset1 * crossingDir1;

                /*Vector2 supportPoint = meetingPoints[i] + relativeOffset0 * crossingDir0 + relativeOffset1 * crossingDir1;

                // TODO: calculate this differently.
                sidewalkRoadBorderPoints[1] = QuadraticBezier(sidewalkRoadBorderPoints[0], supportPoint, sidewalkRoadBorderPoints[4], 0.25f);
                sidewalkRoadBorderPoints[2] = QuadraticBezier(sidewalkRoadBorderPoints[0], supportPoint, sidewalkRoadBorderPoints[4], 0.5f);
                sidewalkRoadBorderPoints[3] = QuadraticBezier(sidewalkRoadBorderPoints[0], supportPoint, sidewalkRoadBorderPoints[4], 0.75f);*/

                sidewalkRoadBorderPoints[2] = outerSidewalkPoints[2] + relativeOffset0 * crossingDir0 * 0.75f + relativeOffset1 * crossingDir1 * 0.25f;
                sidewalkRoadBorderPoints[3] = outerSidewalkPoints[3] + relativeOffset0 * crossingDir0 * 0.5f + relativeOffset1 * crossingDir1 * 0.5f;
                sidewalkRoadBorderPoints[4] = outerSidewalkPoints[4] + relativeOffset0 * crossingDir0 * 0.25f + relativeOffset1 * crossingDir1 * 0.75f;

                Debug.Log("Add point to center...");
                innerIntersection.Add(new Vertex(sidewalkRoadBorderPoints[3].x, GetNetNode().m_position.y, sidewalkRoadBorderPoints[3].y) * UniversalProperties.CONVERSION_SCALE);


                Debug.Log("Create vertices...");
                List<Vertex> blockVertices = new List<Vertex>();

                for (int j = 6; j >= 0; j--)
                {
                    blockVertices.Add(new Vertex(sidewalkRoadBorderPoints[j].x, GetNetNode().m_position.y, sidewalkRoadBorderPoints[j].y) * UniversalProperties.CONVERSION_SCALE);

                    Vertex outer = new Vertex(outerSidewalkPoints[j].x, GetNetNode().m_position.y, outerSidewalkPoints[j].y) * UniversalProperties.CONVERSION_SCALE;
                    outer.y += UniversalProperties.SIDEWALK_HEIGHT;
                    blockVertices.Add(outer);

                }

                // now construct sidewalk mesh from this
                Debug.Log("Construct sidewalk element...");
                SidewalkStripElement sidewalk = new SidewalkStripElement(RoadAnalyzer.SidewalkIntersectionTexture(style)/*"swalk_inter_f"*/, blockVertices);

                intersectionElements.Add(sidewalk);
            }


            // construct crosswalk meshes
            Debug.Log("Construct crosswalks...");
            int polyOffset = 8 * adjacentSegmentsArray.Length;

            for (int i = 0; i < adjacentSegmentsArray.Length; i++)
            {
                List<Vertex> crosswalkVertices = new List<Vertex>();

                SidewalkStripElement side0 = (SidewalkStripElement)intersectionElements[i];
                SidewalkStripElement side1 = (SidewalkStripElement)intersectionElements[(i + 1) % adjacentSegmentsArray.Length];

                crosswalkVertices.Add(side0.GetVertex(2));
                crosswalkVertices.Add(side0.GetVertex(0));

                crosswalkVertices.Add(side1.GetVertex(10));
                crosswalkVertices.Add(side1.GetVertex(12));

                Debug.Log("Construct perimeters...");

                List<PerimeterPoint> roadPerimeterPoints = new List<PerimeterPoint>();
                List<PerimeterPoint> localTerrainPerimeterPoints = new List<PerimeterPoint>();

                roadPerimeterPoints.Add(new PerimeterPoint(side0.GetVertex(1), null));
                roadPerimeterPoints.Add(new PerimeterPoint(side0.GetVertex(0), null));
                roadPerimeterPoints.Add(new PerimeterPoint(side1.GetVertex(12), null));
                roadPerimeterPoints.Add(new PerimeterPoint(side1.GetVertex(13), null));


                this.roadPerimeterPoints.Add(adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value, roadPerimeterPoints);
                intersectionPerimeterPoints.AddRange(roadPerimeterPoints);

                // points not touching connected road
                localTerrainPerimeterPoints.Add(new PerimeterPoint(side1.GetVertex(13), null));
                localTerrainPerimeterPoints.Add(new PerimeterPoint(side1.GetVertex(11), null));
                localTerrainPerimeterPoints.Add(new PerimeterPoint(side1.GetVertex(9), null));
                localTerrainPerimeterPoints.Add(new PerimeterPoint(side1.GetVertex(7), null));
                localTerrainPerimeterPoints.Add(new PerimeterPoint(side1.GetVertex(5), null));
                localTerrainPerimeterPoints.Add(new PerimeterPoint(side1.GetVertex(3), null));
                localTerrainPerimeterPoints.Add(new PerimeterPoint(side1.GetVertex(1), null));

                terrainPerimeterPoints.Add(new KeyValuePair<ushort, ushort>(adjacentSegmentsArray[(i + 1) % adjacentSegmentsArray.Length].Value,
                        adjacentSegmentsArray[(i + 2) % adjacentSegmentsArray.Length].Value), localTerrainPerimeterPoints);

                intersectionPerimeterPoints.AddRange(localTerrainPerimeterPoints);

                Debug.Log("Make crosswalk element...");
                CrosswalkElement crosswalk = new CrosswalkElement(RoadAnalyzer.CrosswalkTexture(style)/*"rxwalk_f"*/, crosswalkVertices);
                intersectionElements.Add(crosswalk);

                Debug.Log("Construct face between crosswalk and center...");
                List<Vertex> betweenVertices = new List<Vertex>();


                betweenVertices.Add(side1.GetVertex(8));
                betweenVertices.Add(side1.GetVertex(10));
                betweenVertices.Add(side0.GetVertex(2));
                betweenVertices.Add(side0.GetVertex(4));
                betweenVertices.Add(side0.GetVertex(6));
                betweenVertices.Add(side1.GetVertex(6));

                CulledTriangleFanElement between = new CulledTriangleFanElement(RoadAnalyzer.IntersectionTexture(style)/*"rinter_f"*/, betweenVertices);
                intersectionElements.Add(between);
            }

            Debug.Log("Construct interior...");
            innerIntersection.Reverse();
            CulledTriangleFanElement interior = new CulledTriangleFanElement(RoadAnalyzer.IntersectionTexture(style), innerIntersection);

            intersectionElements.Add(interior);

            Debug.Assert(intersectionPerimeterPoints.Count == adjacentSegmentsArray.Length * 2);

            intersectionPerimeterPoints.Reverse();

            Debug.Log("Create room sidewalks...");
            room = new Room(intersectionElements, intersectionPerimeterPoints, 0, RoomFlags.Intersection);
        }



        public ushort IntersectionNo { get => intersectionNo; }
        public Room Room { get => room; }
        public ushort NodeID { get => nodeID; }
        public SortedDictionary<float, ushort> AdjacentSegments { get => adjacentSegments; }
        public SortedDictionary<ushort, Vector2[]> ConnectionPoints { get => connectionPoints; }
        public SortedDictionary<ushort, List<PerimeterPoint>> PerimeterPoints { get => roadPerimeterPoints; }
        public Dictionary<KeyValuePair<ushort, ushort>, List<PerimeterPoint>> TerrainPerimeterPoints { get => terrainPerimeterPoints; }

        // helper methods, maybe put somewhere else
        private void AddSegment(ushort segIndex)
        {
            // Vector3 direction = seg.GetDirection(nodeIndex);
            Vector2 rot = GetStraightDirection(segIndex, nodeID);//new Vector2(direction.z, direction.x);
            rot.Normalize();

            // calculate angle
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

        private Vector2 GetStraightDirection(ushort segIndex, ushort nodeIndex)
        {
            NetSegment seg = netMan.m_segments.m_buffer[segIndex];
            Vector3 dir3d = seg.GetDirection(nodeIndex);

            /*if (nodeIndex == seg.m_startNode)
            {
                dir3d = netMan.m_nodes.m_buffer[seg.m_endNode].m_position - netMan.m_nodes.m_buffer[seg.m_startNode].m_position;
            }
            else
            {
                dir3d = netMan.m_nodes.m_buffer[seg.m_startNode].m_position - netMan.m_nodes.m_buffer[seg.m_endNode].m_position;
            }*/

            return new Vector2(dir3d.z, dir3d.x);
        }

        private Vector2 IntersectLines(Vector2 supportA, Vector2 directionA, Vector2 supportB, Vector2 directionB)
        {

            if (Mathf.Abs(directionB.y * directionA.x - directionB.x * directionA.y) < 0.0001)
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

        private Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float param)
        {
            return (1 - param) * (1 - param) * p0 + 2 * (1 - param) * param * p1 + param * param * p2;
        }

        // should be counter-clockwise but needs to be checked to be safe
        public ushort GetAdjacentRoadByOffset(ushort segment, int offset)
        {
            ushort[] segments = adjacentSegments.Values.ToArray();
            int index = Array.IndexOf(segments, segment);
            // TODO: allow infinite wraparound
            return segments[(index + offset + segments.Length) % segments.Length];
        }
        
    }
}
