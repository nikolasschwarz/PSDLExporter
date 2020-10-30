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
        private Vertex[,] vertices;
        private Room room;

        private float maxX = float.NegativeInfinity;
        private float minX = float.PositiveInfinity;

        private float maxZ = float.NegativeInfinity;
        private float minZ = float.PositiveInfinity;
        private static readonly float CELL_SIZE = 20.0f;

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

                List<Vertex> boundaryVertices = currentRoad.TraverseBoundary(currentIntersection.NodeID, nextRoad);

                foreach (Vertex v in boundaryVertices)
                {
                    perimeter.Add(new PerimeterPoint(v, currentRoad.Room));

                    if (v.x > maxX) maxX = v.x;
                    if (v.x < minX) minX = v.x;

                    if (v.z > maxZ) maxZ = v.z;
                    if (v.z < minZ) minZ = v.z;
                }

                // TODO: not true for tunnels or bridges etc.
                List<PerimeterPoint> perimeterPointsToUpdate = new List<PerimeterPoint>();
                //perimeterPointsToUpdate.AddRange( currentRoad.GetLeftPerimeter(currentIntersection.NodeID, nextRoad));
                perimeterPointsToUpdate.AddRange(currentRoad.TraversePerimeter(currentIntersection.NodeID, nextRoad));

                ushort endOfThisRoad;

                if (currentRoad.HasDeadEnd())
                {
                    // add perimeters to come back on the other side
                    if (currentRoad.CSstartIntersection == currentIntersection)
                    {
                        // dead end at endIntersection
                        perimeterPointsToUpdate.AddRange(currentRoad.endPerimeter);
                    }
                    else if (currentRoad.CSendIntersection == currentIntersection)
                    {
                        // dead end at startIntersection
                        perimeterPointsToUpdate.AddRange(currentRoad.startPerimeter);
                    }
                    else
                    {
                        throw new Exception("Next intersection could not be found!");
                    }

                    perimeterPointsToUpdate.AddRange(currentRoad.GetRightPerimeter(currentIntersection.NodeID, nextRoad));

                    // the end is just the same as the start
                    endOfThisRoad = nextRoad;
                }
                else
                {
                    
                    // go to next intersection
                    if (currentRoad.CSstartIntersection == currentIntersection)
                    {
                        currentIntersection = currentRoad.CSendIntersection;
                        endOfThisRoad = currentRoad.SegmentIDs.Last();
                    }
                    else if (currentRoad.CSendIntersection == currentIntersection)
                    {
                        currentIntersection = currentRoad.CSstartIntersection;
                        endOfThisRoad = currentRoad.SegmentIDs[0];
                    }
                    else
                    {
                        throw new Exception("Next intersection could not be found!");
                    }
                }

                foreach( PerimeterPoint pp in perimeterPointsToUpdate)
                {
                    pp.ConnectedRoom = room;
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
            /*Vertex barycenter = CalculateBarycenter();
            // TODO: skip duplicates
            for (int i = 0; i < perimeter.Count; i++)
            {
                Vertex[] vertices = new Vertex[3];

                vertices[0] = barycenter;
                vertices[1] = perimeter[i].Vertex;
                vertices[2] = perimeter[(i + 1) % perimeter.Count].Vertex;

                grass.Add(new TriangleFanElement("s_grass", vertices));
            }*/

            // iterate over vertices and connect them

            for(uint i = 0; i < vertices.GetLength(0) - 1; i++)
            {
                for (uint k = 0; k < vertices.GetLength(1) - 1; k++)
                {
                    if(vertices[i, k] != null && vertices[i, k + 1] != null && vertices[i + 1, k] != null && vertices[i + 1, k + 1] != null)
                    {
                        Vertex[] fanVertices = new Vertex[4];

                        fanVertices[0] = vertices[i, k + 1];
                        fanVertices[1] = vertices[i, k];
                        fanVertices[2] = vertices[i + 1, k];
                        fanVertices[3] = vertices[i + 1, k + 1];

                        TriangleFanElement fan = new TriangleFanElement("s_grass", fanVertices);

                        grass.Add(fan);
                    }
                }
            }

            // now fill the spaces between the grid tiles and the roads
            List<List<Vertex>> polygon = TraverseHoles();
            polygon.Insert(0, GetPerimeterVertexList());
            List<Vertex> polyVertices = new List<Vertex>();
            List<Edge> polyEdges = new List<Edge>();

            // turn into vertex and edge list
            uint index = 0;

            foreach (List<Vertex> vertexList in polygon)
            {
                // skip degenerate cases
                if (vertexList.Count < 3)
                {
                    Debug.Log("Skipping degenerate case with " + vertexList.Count + " vertices.");
                    continue;
                }

                vertexList.Reverse();

                uint firstIndex = index;

                for(int i = 0; i < vertexList.Count - 1; i++)
                {
                    polyVertices.Add(vertexList[i]);

                    polyEdges.Add(new Edge(index, ++index));
                }

                // add last edge
                polyVertices.Add(vertexList.Last());
                polyEdges.Add(new Edge(index, firstIndex));
                index++;
            }

            PolygonTriangulator triangulator = new PolygonTriangulator(polyVertices, polyEdges);

            triangulator.MakeMonotone();
            triangulator.Triangulate();

            List<ISDLElement> fans = triangulator.MakeTriangleFans();

            grass.AddRange(fans);

            room.Elements = grass;
            room.Perimeter = perimeter;
            room.PropRule = 0;
        }

        private List<Vertex> GetPerimeterVertexList()
        {
            Debug.Log("Obtaining perimeter vertices...");
            List<Vertex> perimeterVertices = new List<Vertex>();

            for (int i = 0; i < perimeter.Count; i++)
            {
                // skip duplicates
                if (perimeterVertices.Count == 0 ||
                    (perimeter[i].Vertex.x != perimeterVertices.Last().x || perimeter[i].Vertex.z != perimeterVertices.Last().z))
                {
                    perimeterVertices.Add(perimeter[i].Vertex);
                }
                else if(perimeterVertices.Count > 0 && perimeter[i].Vertex.y != perimeterVertices.Last().y)
                {
                    MeshExporter.warnings.Add("Height is not aligned correctly. Discrepancy: " + (perimeter[i].Vertex.y - perimeterVertices.Last().y));
                }
            }

            // remove duplicates at end
            while(perimeterVertices.Count > 0 &&
                (perimeter[0].Vertex.x == perimeterVertices.Last().x && perimeter[0].Vertex.z == perimeterVertices.Last().z))
            {
                if (perimeter[0].Vertex.y != perimeterVertices.Last().y)
                {
                    MeshExporter.warnings.Add("Height is not aligned correctly. Discrepancy: " + (perimeter[0].Vertex.y - perimeterVertices.Last().y));
                }

                Debug.Log("Removing duplicate...");
                perimeterVertices.RemoveAt(perimeterVertices.Count - 1);
                Debug.Log("Removed duplicate.");
            }
            Debug.Log(perimeterVertices.Count + " perimeter vertices have been obtained.");

            return perimeterVertices;
        }

        private List<List<Vertex>> TraverseHoles()
        {
            bool[,] visited = new bool[vertices.GetLength(0), vertices.GetLength(1)];
            for(int i = 0; i < vertices.GetLength(0); i++)
            {
                for (int k = 0; k < vertices.GetLength(1); k++)
                {
                    visited[i, k] = false;
                }
            }

            List<List<Vertex>> polygonHoles = new List<List<Vertex>>();

            for (uint i = 0; i < vertices.GetLength(0); i++)
            {
                for (uint k = 0; k < vertices.GetLength(1); k++)
                {
                    if(!visited[i, k] && vertices[i, k] != null && !VertexIsInterior((int)i, (int)k))
                    {
                        Debug.Log("Traverse hole...");
                        List<Vertex> holeVertices = TraceHoleBoundary(i, k, visited);

                        if(holeVertices.Count > 0)
                        {
                            polygonHoles.Add(holeVertices);
                        }
                    }
                }
            }

            return polygonHoles;
        }

        private bool VertexExists(int i, int j)
        {
            if(i < 0 || i >= vertices.GetLength(0))
            {
                //Debug.Log("Vertex (" + i + ", " + j + ") is out of bounds");
                return false;
            }

            if (j < 0 || j >= vertices.GetLength(1))
            {
                //Debug.Log("Vertex (" + i + ", " + j + ") is out of bounds");
                return false;
            }

           // Debug.Log("Vertex (" + i + ", " + j + ") is within bounds");

            return vertices[i, j] != null;
        }

        private bool VertexIsInterior(int i, int j)
        {
            if (!VertexExists(i, j)) return false;

            // vertex needs to have at least two adjacent vertices to be valid

            uint adjacencyCount = 0;

            if (VertexExists(i, j + 1))
            {
                adjacencyCount++;
            }

            if (VertexExists(i, j - 1))
            {
                adjacencyCount++;
            }

            if (VertexExists(i + 1, j))
            {
                adjacencyCount++;
            }

            if (VertexExists(i - 1, j))
            {
                adjacencyCount++;
            }

            if (VertexExists(i + 1, j + 1))
            {
                adjacencyCount++;
            }

            if (VertexExists(i - 1, j - 1))
            {
                adjacencyCount++;
            }

            if (VertexExists(i + 1, j - 1))
            {
                adjacencyCount++;
            }

            if (VertexExists(i - 1, j + 1))
            {
                adjacencyCount++;
            }

            return adjacencyCount == 8;
        }

        private bool VertexIsValid(int i, int j)
        {
            if (!VertexExists(i, j)) return false;

            // vertex needs to have at least two adjacent vertices to be valid

            uint adjacencyCount = 0;

            if(VertexExists(i, j + 1))
            {
                adjacencyCount++;
            }

            if (VertexExists(i, j - 1))
            {
                adjacencyCount++;
            }

            if (VertexExists(i + 1, j))
            {
                adjacencyCount++;
            }

            if (VertexExists(i - 1, j))
            {
                adjacencyCount++;
            }

            return adjacencyCount >= 2;
        }

        public List<Vertex> TraceHoleBoundary(uint iStart, uint kStart, bool[,] visited)
        {
            uint i = iStart;
            uint k = kStart;
            // find boundary
            List<Vertex> holeVertices = new List<Vertex>();

            if(!VertexIsValid((int)iStart, (int)kStart))
            {
                // TODO: consider using the vertex for triangulation
                return holeVertices;
            }


            Vertex startVertex = vertices[i,k];
        


            // traverse boundary
            Vertex currentVertex = startVertex;
            int idir = 0, kdir = 1;

            // TODO: what if two holes touch each other? Right now they are treated as separate but that will probably lead to some degenerate
            // triangles when triangulating
            do
            {
                holeVertices.Add(currentVertex);

                if(holeVertices.Count > vertices.Length)
                {
                    throw new Exception("Someting went wrong, added more vertices than there are in total!");
                }

                Debug.Log("i: " + i + "; k: " + k);
                visited[i, k] = true;
                Vertex nextVertex;
                

                if(!VertexIsValid((int)i + idir /*+ kdir*/, (int)k + kdir /*- idir*/))
                {
                    Debug.Log("Vertex straight ahead does not exist, turning right.");
                    int tmp = kdir;
                    kdir = -idir;
                    idir = tmp;
                }
                else if(VertexIsValid((int)i/* + idir*/ - kdir, (int)k /*+ kdir*/ + idir))
                {
                    Debug.Log("Vertex to the left exists, turning left.");
                    int tmp = kdir;
                    kdir = idir;
                    idir = -tmp;
                }
                else
                {
                    Debug.Log("Vertex straight ahead exists, keeping direction.");
                }

                // TODO: this is not necessarily true if two neighboring vertices are not supposed to be connected
                // but for now we will ignore this case as it is rare.

                Debug.Log("idir: " + idir + "; kdir: " + kdir);
                nextVertex = vertices[i + idir, k + kdir];
                i = (uint)(i + idir);
                k = (uint)(k + kdir);

                // now connect the two properly to the boundary

                currentVertex = nextVertex;

            } while (i != iStart || k != kStart);
            // TODO: in the future there could be holes from road segments that also have to be connected

            return holeVertices;
        }

        public void ScanHeight()
        {
            uint width = (uint)((maxX - minX) / CELL_SIZE);
            uint depth = (uint)((maxZ - minZ) / CELL_SIZE);

            vertices = new Vertex[depth, width];

            for(uint i = 0; i < depth; i++)
            {
                float ifrac = (float)(i + 1) / (float)(depth + 1);
                float zValue = minZ + (maxZ - minZ) * ifrac;

                List<float> intersections = Intersect(zValue);
                intersections.Sort(); // ascending (?!)
                bool inside = false;

                for (uint k = 0; k < width; k++)
                {
                    float kfrac = (float)(k + 1) / (float)(width + 1);
                    float xValue = minX + (maxX - minX) * kfrac;

                    // determine if inside or outside
                    while(intersections.Count > 0 && intersections[0] < xValue)
                    {
                        intersections.RemoveAt(0);
                        inside = !inside;
                    }

                    if (inside)
                    {
                        vertices[i, k] = new Vertex(xValue, 0, zValue);
                        vertices[i, k].y = RoadUtils.QueryHeight(vertices[i, k].x, vertices[i, k].z);
                    }
                    // else should be null
                    // TODO: if two neighboring vertices are inside but there are intersections inbetween, they will be connected nonetheless
                }
            }
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
