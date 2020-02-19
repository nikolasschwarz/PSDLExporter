using PSDL;
using PSDL.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PSDLExporter
{
    // used to generate ground. 
    class CSGround
    {
        private List<PerimeterPoint> perimeter = new List<PerimeterPoint>();
        private SortedDictionary<ushort, ushort> identificationSegments = new SortedDictionary<ushort, ushort>(); // pairs of segments that border the ground at each adjacent intersection
        private Room room;

        private float maxX = float.NegativeInfinity;
        private float minX = float.PositiveInfinity;

        private float maxZ = float.NegativeInfinity;
        private float minZ = float.PositiveInfinity;

        // whether the surface is closed, i.e. surrounded by roads
        private bool? isClosed = null;

        public Room Room { get => room; }
        public SortedDictionary<ushort, ushort> IdentificationSegments { get => identificationSegments; }

        public CSGround(KeyValuePair<ushort, ushort> identifier)
        {
            identificationSegments.Add(identifier.Key, identifier.Value);
        }

        public void FindAdjacentRoadsAndIntersections(SortedDictionary<ushort, CSRoad> roads)
        {
            ushort firstRoad = identificationSegments.Values.Last();
            ushort nextRoad = identificationSegments.Values.Last();
            room = new Room();

            // traverse boundary of ground tile
            do
            {
                CSRoad currentRoad = roads[nextRoad];

                List<Vertex> boundaryVertices = currentRoad.TraverseBoundary(nextRoad);

                foreach (Vertex v in boundaryVertices)
                {
                    perimeter.Add(new PerimeterPoint(v, currentRoad.Room));

                    if (v.x > maxX) maxX = v.x;
                    if (v.x < minX) minX = v.x;

                    if (v.z > maxZ) maxZ = v.z;
                    if (v.z < minZ) minZ = v.z;
                    // TODO: update corresponding perimeter points of road
                }

                CSIntersection followingIntersection;
                ushort endOfThisRoad;
                if(currentRoad.SegmentIDs[0] == nextRoad)
                {
                    followingIntersection = currentRoad.CSendIntersection;
                    endOfThisRoad = currentRoad.SegmentIDs.Last();
                    nextRoad = followingIntersection.GetAdjacentRoadByOffset(currentRoad.SegmentIDs.Last(), 1); // is clockwise

                }
                else
                {
                    followingIntersection = currentRoad.CSstartIntersection;
                    endOfThisRoad = currentRoad.SegmentIDs[0];
                    
                }

                nextRoad = followingIntersection.GetAdjacentRoadByOffset(endOfThisRoad, 1); // is clockwise

                // add vertices that border the intersection
                KeyValuePair<ushort, ushort> identifier = new KeyValuePair<ushort, ushort>(endOfThisRoad, nextRoad);
                if(!identificationSegments.Contains(identifier))
                {
                    identificationSegments.Add(endOfThisRoad, nextRoad);
                }

                List<PerimeterPoint> intersectionPerimeter;

                if (followingIntersection.TerrainPerimeterPoints.ContainsKey(identifier))
                {
                    intersectionPerimeter = followingIntersection.TerrainPerimeterPoints[identifier];
                }
                else
                {
                    Debug.LogWarning("identifier " + endOfThisRoad + ", " + nextRoad + " is either not present or inverted. Attempting to use inversion.");
                    intersectionPerimeter = followingIntersection.TerrainPerimeterPoints[new KeyValuePair<ushort, ushort>(nextRoad, endOfThisRoad)];
                }
                // points must be in opposite order
                for(int i = 0; i < intersectionPerimeter.Count; i++)
                {
                    // TODO: create room beforehand or update later.
                    intersectionPerimeter[i].ConnectedRoom = room;
                    perimeter.Add(new PerimeterPoint(intersectionPerimeter[i].Vertex, followingIntersection.Room));

                    if (intersectionPerimeter[i].Vertex.x > maxX) maxX = intersectionPerimeter[i].Vertex.x;
                    if (intersectionPerimeter[i].Vertex.x < minX) minX = intersectionPerimeter[i].Vertex.x;

                    if (intersectionPerimeter[i].Vertex.z > maxZ) maxZ = intersectionPerimeter[i].Vertex.z;
                    if (intersectionPerimeter[i].Vertex.z < minZ) minZ = intersectionPerimeter[i].Vertex.z;
                }

            } while (nextRoad != firstRoad);
        }

        public void ConstructRoom()
        {
            Vertex[] vertices = new Vertex[perimeter.Count + 1];

            vertices[0] = CalculateBarycenter();

            // TODO: skip duplicates
            for(int i = 0; i < perimeter.Count; i++)
            {
                vertices[i + 1] = perimeter[i].Vertex;
            }

            List<ISDLElement> grass = new List<ISDLElement>();
            grass.Add(new CulledTriangleFanElement("s_grass", vertices));

            room.Elements = grass;
            room.Perimeter = perimeter;
        }

        public float CalculateBoundingBoxArea()
        {
            return (maxX - minX) * (maxZ - minZ);
        }

        public Vertex CalculateBarycenter()
        {
            Vertex barycenter = new Vertex(0.0f, 0.0f, 0.0f);

            foreach(PerimeterPoint pp in perimeter)
            {
                barycenter += pp.Vertex;
            }

            barycenter /= perimeter.Count;

            return barycenter;
        }
    }
}
