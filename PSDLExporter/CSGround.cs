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
        private List<CSIntersection> adjacentIntersections = new List<CSIntersection>();
        private Room room;

        private float maxX = float.NegativeInfinity;
        private float minX = float.PositiveInfinity;

        private float maxZ = float.NegativeInfinity;
        private float minZ = float.PositiveInfinity;

        // whether the surface is closed, i.e. surrounded by roads
        private bool? isClosed = null;

        public Room Room { get => room; }
        public SortedDictionary<ushort, ushort> IdentificationSegments { get => identificationSegments; }

        public CSGround(KeyValuePair<ushort, ushort> identifier, CSIntersection startIntersection)
        {
            identificationSegments.Add(identifier.Key, identifier.Value);
            adjacentIntersections.Add(startIntersection);
        }

        public void FindAdjacentRoadsAndIntersections(SortedDictionary<ushort, CSRoad> roads)
        {
            ushort firstRoad = identificationSegments.Values.Last();
            ushort nextRoad = identificationSegments.Values.Last();
            CSIntersection currentIntersection = adjacentIntersections.Last();
            room = new Room();

            // traverse boundary of ground tile
            do
            {
                CSRoad currentRoad = roads[nextRoad];

                List<Vertex> boundaryVertices = currentRoad.TraverseBoundary(currentIntersection.NodeID);

                foreach (Vertex v in boundaryVertices)
                {
                    perimeter.Add(new PerimeterPoint(v, currentRoad.Room));

                    if (v.x > maxX) maxX = v.x;
                    if (v.x < minX) minX = v.x;

                    if (v.z > maxZ) maxZ = v.z;
                    if (v.z < minZ) minZ = v.z;
                    // TODO: update corresponding perimeter points of road
                }

                List<PerimeterPoint> perimeterPointsToUpdate = currentRoad.GetLeftPerimeter(currentIntersection.NodeID);

                foreach( PerimeterPoint pp in perimeterPointsToUpdate)
                {
                    pp.ConnectedRoom = room;
                }


                ushort endOfThisRoad;
                if(currentRoad.CSstartIntersection == currentIntersection)
                {
                    currentIntersection = currentRoad.CSendIntersection;
                    endOfThisRoad = currentRoad.SegmentIDs.Last();
                }
                else if(currentRoad.CSendIntersection == currentIntersection)
                {
                    currentIntersection = currentRoad.CSstartIntersection;
                    endOfThisRoad = currentRoad.SegmentIDs[0];                   
                }
                else
                {
                    throw new Exception("Next intersection could not be found!");
                }

                nextRoad = currentIntersection.GetAdjacentRoadByOffset(endOfThisRoad, 1); // is clockwise

                // add vertices that border the intersection
                KeyValuePair<ushort, ushort> identifier = new KeyValuePair<ushort, ushort>(endOfThisRoad, nextRoad);
                if(!identificationSegments.Contains(identifier))
                {
                    identificationSegments.Add(endOfThisRoad, nextRoad);
                }

                List<PerimeterPoint> intersectionPerimeter;

                if (currentIntersection.TerrainPerimeterPoints.ContainsKey(identifier))
                {
                    intersectionPerimeter = currentIntersection.TerrainPerimeterPoints[identifier];
                }
                else
                {
                    Debug.LogWarning("identifier " + endOfThisRoad + ", " + nextRoad + " is either not present or inverted. Attempting to use inversion.");
                    intersectionPerimeter = currentIntersection.TerrainPerimeterPoints[new KeyValuePair<ushort, ushort>(nextRoad, endOfThisRoad)];
                }
                // points must be in opposite order
                for(int i = 0; i < intersectionPerimeter.Count; i++)
                {
                    // TODO: create room beforehand or update later.
                    intersectionPerimeter[i].ConnectedRoom = room;
                    perimeter.Add(new PerimeterPoint(intersectionPerimeter[i].Vertex, currentIntersection.Room));

                    if (intersectionPerimeter[i].Vertex.x > maxX) maxX = intersectionPerimeter[i].Vertex.x;
                    if (intersectionPerimeter[i].Vertex.x < minX) minX = intersectionPerimeter[i].Vertex.x;

                    if (intersectionPerimeter[i].Vertex.z > maxZ) maxZ = intersectionPerimeter[i].Vertex.z;
                    if (intersectionPerimeter[i].Vertex.z < minZ) minZ = intersectionPerimeter[i].Vertex.z;
                }

            } while (nextRoad != firstRoad);
        }

        public void ConstructRoom()
        {
            List<ISDLElement> grass = new List<ISDLElement>();
            Vertex barycenter = CalculateBarycenter();
            // TODO: skip duplicates
            for (int i = 0; i < perimeter.Count; i++)
            {
                Vertex[] vertices = new Vertex[3];

                vertices[0] = barycenter;
                vertices[1] = perimeter[i].Vertex;
                vertices[2] = perimeter[(i + 1) % perimeter.Count].Vertex;

                grass.Add(new TriangleFanElement("s_grass", vertices));
            }

            room.Elements = grass;
            room.Perimeter = perimeter;
            room.PropRule = 0;
        }

        public void ScanHeight()
        {

        }

        /// <summary>
        /// Essentially a 2D ray cast parallel to the x-axis
        /// </summary>
        /// <param name="z"></param>
        /// <returns></returns>
        public List<float> Intersect(float z)
        {
            List<float> intersections = new List<float>();

            bool onLine = false;
            bool enteredFromTop = false;

            // TODO: what if we start on the line?

            for (int i = 0; i < perimeter.Count; i++)
            {
                Vertex v0 = perimeter[i].Vertex;
                Vertex v1 = perimeter[(i + 1) % perimeter.Count].Vertex;

                if((v0.z < z && v1.z > z) || (v0.z > z && v1.z < z))
                {
                    float t = (z - v0.z) / (v1.z - v0.z);

                    float x = v0.x + t * (v1.x - v0.x);

                    intersections.Add(x);
                }
                else if (v1.z == z && !onLine)
                {
                    intersections.Add(v1.x);
                    onLine = true;
                    enteredFromTop = v0.z > z;
                }
                else if(onLine) // leaving line
                {
                    // in case we leave in the other direction than we came from we add the hit twice
                    intersections.Add(v0.x);
                    if ((enteredFromTop && v1.z < z) || (!enteredFromTop && v1.z > z))
                    {
                        intersections.Add(v0.x);
                    }
                    onLine = false;
                }
            }

            return intersections;
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
            Debug.Log("Barycenter height is: " + barycenter.y);
            barycenter.y = RoadUtils.QueryHeight(barycenter.x, barycenter.z);

            return barycenter;
        }
    }
}
