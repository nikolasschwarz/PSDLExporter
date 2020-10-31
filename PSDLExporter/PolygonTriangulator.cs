using PSDL;
using PSDL.Elements;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PSDLExporter
{

    public struct Edge
    {
        public Edge(uint v0, uint v1)
        {
            this.v0 = v0;
            this.v1 = v1;
        }

        public uint v0;
        public uint v1;

        public Edge Twin()
        {
            return new Edge(v1, v0);
        }
    }
    /// <summary>
    /// This follows chapter 3: Polygon Triangulation from the Book "Computational Geometry" by
    /// Mark de Berg, Otfried Cheong, Marc van Kreveld and Mark Overmars (ISBN 978-3-540-77973-5)        
    /// </summary>
    public class PolygonTriangulator
    {
        IEnumerable<Vertex> vertices;
        List<Edge> edges;
        Dictionary<Edge, uint> helperVertices = new Dictionary<Edge, uint>();
        List<Edge> leftEdgesOnSweepLine = new List<Edge>(); // TODO: could be optimized by using a binary search tree
        List<Edge>[] outgoing;
        List<Edge>[] incoming;

        enum VertexType
        {
            Start,
            End,
            Regular,
            Split,
            Merge
        }

        public PolygonTriangulator(IEnumerable<Vertex> vertices, List<Edge> edges)
        {
            this.vertices = vertices;
            this.edges = edges;

            outgoing = new List<Edge>[vertices.Count()];
            incoming = new List<Edge>[vertices.Count()];

            for (int i = 0; i < vertices.Count(); i++)
            {
                outgoing[i] = new List<Edge>();
                incoming[i] = new List<Edge>();
            }

            foreach(Edge e in edges)
            {
                outgoing[e.v0].Add(e);
                incoming[e.v1].Add(e);
            }


            // for debugging print all
            /*
            string dataCodeSnippet = "";

            dataCodeSnippet += "Vertex[] vertices = new Vertex[" + vertices.Count() + "];" + Environment.NewLine;

            for (int i = 0; i < vertices.Count(); i++)
            {
                //Debug.Log("vertex " + i + ": " + vertices.ElementAt(i).ToString());
                Vertex v = vertices.ElementAt(i);
                dataCodeSnippet += "vertices[" + i + "] = new Vertex(" + v.x + "f, " + v.y + "f, " + v.z + "f);" + Environment.NewLine;
            }

            dataCodeSnippet += "List<Edge> edges = new List<Edge>" + Environment.NewLine;
            dataCodeSnippet += "{" + Environment.NewLine;

            foreach (Edge e in edges)
            {
                //Debug.Log("edge: (" + e.v0 + ", " + e.v1 + ")");
                dataCodeSnippet += "new Edge(" + e.v0 + ", " + e.v1 + ")," + Environment.NewLine;
            }

            dataCodeSnippet += "};" + Environment.NewLine;

            StreamWriter writer = new StreamWriter(@"D:\Games\SteamLibrary\steamapps\common\Cities_Skylines\codeSnippet.txt");
            writer.Write(dataCodeSnippet);
            writer.Flush();
            writer.Close();*/
        }

        private void MakeMonotone()
        {
            // use this as priority queue for vertices sorted primarily by z-coordinate and secondarily by their x-coordinate
            SortedDictionary<Vertex, int> vertexQueue = new SortedDictionary<Vertex, int>(new VertexComparer());

            IEnumerator<Vertex> enumerator = vertices.GetEnumerator();
            for (int i = 0; i < vertices.Count(); i++)
            {
                vertexQueue.Add(vertices.ElementAt(i), i);
            }

            Console.WriteLine("Set up queue.");

            while(vertexQueue.Count > 0)
            {
                KeyValuePair<Vertex, int> v = vertexQueue.Last();

                // test comparer
                VertexComparer comp = new VertexComparer();

                Console.WriteLine("Processing vertex: " + v.Value);

                VertexType type = DetermineType((uint)v.Value);

                Console.WriteLine("Vertex type: " + type);

                switch (type)
                {
                    case VertexType.Start:
                        HandleStartVertex((uint)v.Value);
                        break;
                    case VertexType.End:
                        HandleEndVertex((uint)v.Value);
                        break;
                    case VertexType.Split:
                        HandleSplitVertex((uint)v.Value);
                        break;
                    case VertexType.Merge:
                        HandleMergeVertex((uint)v.Value);
                        break;
                    case VertexType.Regular:
                        HandleRegularVertex((uint)v.Value);
                        break;
                }

                bool success = vertexQueue.Remove(v.Key);

                Console.WriteLine("Removed vertex: " + success);
                Console.WriteLine();
            }
        }

        private void HandleStartVertex(uint vertexIndex)
        {
            Edge followingEdge = FindOutgoingEdge(vertexIndex);

            leftEdgesOnSweepLine.Add(followingEdge);
            helperVertices.Add(followingEdge, vertexIndex);
        }

        private void HandleEndVertex(uint vertexIndex)
        {
            Edge e = FindIncomingEdge(vertexIndex);

            uint helperVertex = helperVertices[e];
            if(IsMergeVertex(helperVertex))
            {
                /*Edge diagonal;
                diagonal.v0 = vertexIndex;
                diagonal.v1 = helperVertex;
                edges.Add(diagonal);*/
                Edge diagonal = Connect(vertexIndex, helperVertex);

                Console.WriteLine("Added edge: (" + diagonal.v0 + ", " + diagonal.v1 + ")");

                // TODO: maybe also add twin
            }

            leftEdgesOnSweepLine.Remove(e);
        }

        private void HandleSplitVertex(uint vertexIndex)
        {
            Edge justLeft = FindEdgeToTheLeft(vertexIndex);
            uint helperVertex = helperVertices[justLeft];

            /*Edge diagonal;
            diagonal.v0 = vertexIndex;
            diagonal.v1 = helperVertex;
            edges.Add(diagonal);*/
            Edge diagonal = Connect(vertexIndex, helperVertex);

            Console.WriteLine("Added edge: (" + diagonal.v0 + ", " + diagonal.v1 + ")");


            // TODO: maybe also add twin

            helperVertices[justLeft] = vertexIndex;

            Edge followingEdge = FindOutgoingEdge(vertexIndex);

            leftEdgesOnSweepLine.Add(followingEdge);
            helperVertices.Add(followingEdge, vertexIndex);
        }

        private void HandleMergeVertex(uint vertexIndex)
        {
            HandleEndVertex(vertexIndex);

            Edge justLeft = FindEdgeToTheLeft(vertexIndex);
            uint helperVertex = helperVertices[justLeft];

            if(IsMergeVertex(helperVertex))
            {
                /*Edge diagonal;
                diagonal.v0 = vertexIndex;
                diagonal.v1 = helperVertex;
                edges.Add(diagonal);*/
                Edge diagonal = Connect(vertexIndex, helperVertex);

                Console.WriteLine("Added edge: (" + diagonal.v0 + ", " + diagonal.v1 + ")");
            }

            helperVertices[justLeft] = vertexIndex;

        }

        private void HandleRegularVertex(uint vertexIndex)
        {
            if(InteriorToTheRight(vertexIndex))
            {
                Console.WriteLine("Interior is to the right.");
                Edge e = FindIncomingEdge(vertexIndex);

                uint helperVertex = helperVertices[e];

                if (IsMergeVertex(helperVertex))
                {
                    /*Edge diagonal;
                    diagonal.v0 = vertexIndex;
                    diagonal.v1 = helperVertex;
                    edges.Add(diagonal);*/
                    Edge diagonal = Connect(vertexIndex, helperVertex);

                    Console.WriteLine("Added edge: (" + diagonal.v0 + ", " + diagonal.v1 + ")");
                }

                leftEdgesOnSweepLine.Remove(e);

                Edge followingEdge = FindOutgoingEdge(vertexIndex);

                leftEdgesOnSweepLine.Add(followingEdge);
                helperVertices.Add(followingEdge, vertexIndex);
            }
            else
            {
                Console.WriteLine("Interior is to the left.");
                Edge justLeft = FindEdgeToTheLeft(vertexIndex);
                uint helperVertex = helperVertices[justLeft];
                if(IsMergeVertex(helperVertex))
                {
                    /*Edge diagonal;
                    diagonal.v0 = vertexIndex;
                    diagonal.v1 = helperVertex;
                    edges.Add(diagonal);*/
                    Edge diagonal = Connect(vertexIndex, helperVertex);

                    Console.WriteLine("Added edge: (" + diagonal.v0 + ", " + diagonal.v1 + ")");

                }
                helperVertices[justLeft] = vertexIndex;
            }
        }

        private Edge FindEdgeToTheLeft(uint vertexIndex)
        {
            // cast a ray along the z-coordinate of the vertex
            //throw new NotImplementedException();

            float z = vertices.ElementAt((int)vertexIndex).z;
            float x = vertices.ElementAt((int)vertexIndex).x;
            float xToTheLeft = float.NegativeInfinity;
            Edge edgeToTheLeft;
            edgeToTheLeft.v0 = uint.MaxValue;
            edgeToTheLeft.v1 = uint.MaxValue;

            for (int i = 0; i < leftEdgesOnSweepLine.Count; i++)
            {
                if(leftEdgesOnSweepLine[i].v0 == vertexIndex || leftEdgesOnSweepLine[i].v1 == vertexIndex)
                {
                    continue;
                }

                Vertex v0 = vertices.ElementAt((int)leftEdgesOnSweepLine[i].v0);
                Vertex v1 = vertices.ElementAt((int)leftEdgesOnSweepLine[i].v1);

                if (v1.z != v0.z)
                {
                    float t = (z - v0.z) / (v1.z - v0.z);

                    float potXToTheLeft = v0.x + t * (v1.x - v0.x);

                    if(potXToTheLeft < x && potXToTheLeft > xToTheLeft)
                    {
                        xToTheLeft = potXToTheLeft;
                        edgeToTheLeft = leftEdgesOnSweepLine[i];
                    }
                }
                else if(v1.z == z)
                {
                    float potXToTheLeft = Mathf.Max(v0.x, v1.x);

                    if (potXToTheLeft < x && potXToTheLeft > xToTheLeft)
                    {
                        xToTheLeft = potXToTheLeft;
                        edgeToTheLeft = leftEdgesOnSweepLine[i];
                    }
                }
                
            }

            Console.WriteLine("Edge to the left: (" + edgeToTheLeft.v0 + ", " + edgeToTheLeft.v1 + ")");

            return edgeToTheLeft;
        }

        private bool InteriorToTheRight(uint vertexIndex)
        {
            Edge outgoingEdge = FindOutgoingEdge(vertexIndex);
            VertexComparer comp = new VertexComparer();
            return comp.Compare(vertices.ElementAt((int)outgoingEdge.v1), vertices.ElementAt((int)outgoingEdge.v0)) < 0;
        }

        private bool InteriorToTheRight(uint vertexIndex, List<uint> perimeterVertices)
        {
            // TODO: IndexOf() is slow!
            int position = perimeterVertices.IndexOf(vertexIndex);

            Edge outgoingEdge = new Edge(vertexIndex, perimeterVertices[(position + 1) % perimeterVertices.Count]);

            VertexComparer comp = new VertexComparer();
            return comp.Compare(vertices.ElementAt((int)outgoingEdge.v1), vertices.ElementAt((int)outgoingEdge.v0)) < 0;
        }

        private bool IsMergeVertex(uint index)
        {
            return DetermineType(index) == VertexType.Merge;
        }

        private Edge FindIncomingEdge(uint index)
        {
            /*foreach (Edge e in edges)
            {
                if(index == e.v1)
                {
                    return e;
                }
            }*/
            if (incoming[index].Count > 0)
            {
                return incoming[index][0];
            }

            throw new Exception("Vertex is either a source, isolated or does not exist.");
        }

        private Edge FindOutgoingEdge(uint index)
        {
            /*foreach (Edge e in edges)
            {
                if (index == e.v0)
                {
                    return e;
                }
            }*/
            if(outgoing[index].Count > 0)
            {
                return outgoing[index][0];
            }

            throw new Exception("Vertex is either a sink, isolated or does not exist.");
        }

        float DeltaXDivZ(Vertex u, Vertex v)
        {
            if(v.z == u.z)
            {
                if(u.x < v.x)
                {
                    return float.PositiveInfinity;
                }
                else if(u.x > v.x)
                {
                    return float.NegativeInfinity;
                }
                else
                {
                    return float.NaN;
                }
            }

            return (v.x - u.x) / (v.z - u.z);
        }

        private VertexType DetermineType(uint vertexIndex)
        {
            // debug
            //if (vertexIndex == 18) return VertexType.Split;

            Edge incoming = FindIncomingEdge(vertexIndex);
            Edge outgoing = FindOutgoingEdge(vertexIndex);

            Vertex prev = vertices.ElementAt((int)incoming.v0);
            Vertex current = vertices.ElementAt((int)vertexIndex);
            Vertex next = vertices.ElementAt((int)outgoing.v1);

            VertexComparer comp = new VertexComparer();

            int cPrev = comp.Compare(prev, current);
            int cNext = comp.Compare(current, next);

            if(cPrev == cNext)
            {
                return VertexType.Regular;
            }
            else if(cPrev < 0 && cNext > 0)
            {
                // split or start
                // calculate delta x divided by delta z for both edges
                float dxzPrev = DeltaXDivZ(prev, current);//(prev.x - current.x) / (prev.z - current.z);
                float dxzNext = DeltaXDivZ(next, current);// (next.x - current.x) / (next.z - current.z);

                Console.WriteLine("dxzPrev: " + dxzPrev);
                Console.WriteLine("dxzNext: " + dxzNext);

                if (dxzNext > dxzPrev) // start
                {
                    return VertexType.Start;
                }
                else // split
                {
                    return VertexType.Split;
                }
            }
            else // if(cPrev > 0 && cNext < 0)
            {
                // end or merge
                // calculate delta x divided by delta z for both edges
                float dxzPrev = DeltaXDivZ(current, prev);//(prev.x - current.x) / (prev.z - current.z);
                float dxzNext = DeltaXDivZ(current, next);// (next.x - current.x) / (next.z - current.z);

                if (dxzNext > dxzPrev) // end
                {
                    return VertexType.End;
                }
                else // merge
                {
                    return VertexType.Merge;
                }
            }
        }

        private Edge Connect(uint v0, uint v1, bool bidirectional = true)
        {
            Edge e;
            e.v0 = v0;
            e.v1 = v1;

            outgoing[v0].Add(e);
            incoming[v1].Add(e);
            edges.Add(e);

            if(bidirectional)
            {
                Connect(v1, v0, false);
            }

            return e;
        }

        public List<List<uint>> ExtractMonotonePolygons()
        {
            List<List<uint>> polygons = new List<List<uint>>();

            // every half-edge should be in exactly one polygon

            uint currentVertex = 1;
            uint startVertex = 1;
            //int startOfNextPolygon = -1;
            HashSet<Edge> visitedCornerOutgoing = new HashSet<Edge>();
            List<Edge> polygonStartEdges = new List<Edge>();

            Edge currentEdge;
            currentEdge.v0 = 0;
            currentEdge.v1 = 1;
            bool edgeLeft = true;

            while (edgeLeft)
            {

                Console.WriteLine("Traversing polygon with starting edge: (" + currentEdge.v0 + ", " + currentEdge.v1 + ")");
                visitedCornerOutgoing.Add(currentEdge);
                startVertex = currentEdge.v0;

                List<uint> currentPolygon = new List<uint>
                {
                    currentEdge.v0
                };

                do
                {
                    currentPolygon.Add(currentVertex);
                    if(outgoing[currentVertex].Count == 1)
                    {
                        // simple case
                        currentEdge = outgoing[currentVertex][0];
                    }
                    else
                    {
                        // we need to figure out which outgoing edge is the closest to the incoming one in clockwise direction
                        currentEdge = DetermineOutgoing(currentEdge);
                        visitedCornerOutgoing.Add(currentEdge);

                        // add all others to todo list
                        // TODO: This will also add the edge we are traversing next but that shouldn't hurt much. It should be reviewed, though.
                        polygonStartEdges.AddRange(outgoing[currentVertex]);

                    }

                    Console.WriteLine("Current edge: (" + currentEdge.v0 + ", " + currentEdge.v1 + ")");
                    //Console.ReadLine();

                    currentVertex = currentEdge.v1;


                } while (currentVertex != startVertex);
                Console.WriteLine("Polygon completed.");

                polygons.Add(currentPolygon);

                // find next polygon
                do
                {
                    if (polygonStartEdges.Count == 0)
                    {
                        edgeLeft = false;
                        break;
                    }

                    currentEdge = polygonStartEdges[0];
                    currentVertex = currentEdge.v1;
                    polygonStartEdges.RemoveAt(0);

                } while (visitedCornerOutgoing.Contains(currentEdge));
            }

            return polygons;
        }

        private Edge DetermineOutgoing(Edge currentEdge)
        {
            List<Edge> outgoingEdges = outgoing[currentEdge.v1];

            foreach(Edge e in outgoingEdges)
            {
                Console.WriteLine("Outgoing edge of vertex " + currentEdge.v1 + ": (" + e.v0 + ", " + e.v1 + ")");
            }

            // calculate incoming angle

            Vertex inDir = vertices.ElementAt((int)currentEdge.v0) - vertices.ElementAt((int)currentEdge.v1);
            Vector2 normInDir = new Vector2(inDir.x, inDir.z);
            normInDir.Normalize();

            float inAngle;

            if (normInDir.y >= 0)
            {
                inAngle = Mathf.Acos(normInDir.x);
            }
            else
            {
                inAngle = 2 * Mathf.PI - Mathf.Acos(normInDir.x);
            }


            float[] angles = new float[outgoingEdges.Count];

            for(int i = 0; i < outgoingEdges.Count; i++)
            {
                Vertex v0 = vertices.ElementAt((int)outgoingEdges[i].v0);
                Vertex v1 = vertices.ElementAt((int)outgoingEdges[i].v1);
                Vertex dir = v1 - v0;
                Vector2 normDir = new Vector2(dir.x, dir.z);
                normDir.Normalize();

                if (normDir.y >= 0)
                {
                    angles[i] = Mathf.Acos(normDir.x);
                }
                else
                {
                    angles[i] = 2 * Mathf.PI - Mathf.Acos(normDir.x);
                }

                angles[i] -= inAngle;
                if(angles[i] < 0)
                {
                    angles[i] += 2 * Mathf.PI;
                }

                Console.WriteLine("Angle at edge (" + outgoingEdges[i].v0 + ", " + outgoingEdges[i].v1 + "): " + (angles[i] * 180.0f / Mathf.PI));
            }

            Edge clockwiseNeighbor = outgoingEdges[0];
            float maxAngle = angles[0];

            for (int i = 1; i < outgoingEdges.Count; i++)
            {
                if(maxAngle < angles[i])
                {
                    clockwiseNeighbor = outgoingEdges[i];
                    maxAngle = angles[i];
                }
            }

            return clockwiseNeighbor;
        }

        public void TriangulateMonotonePolygon(List<uint> counterClockwiseVertices)
        {
            // use this as priority queue for vertices sorted primarily by z-coordinate and secondarily by their x-coordinate
            SortedDictionary<Vertex, uint> vertexQueue = new SortedDictionary<Vertex, uint>(new VertexComparer());

            // TODO: if we know the maximum we can start traversing from there in both directions and merge the two sequences until we reach the minimum.
            // That would be much more efficient.
            Console.WriteLine("Adding all vertices to the queue....");
            for (int i = 0; i < counterClockwiseVertices.Count(); i++)
            {
                vertexQueue.Add(vertices.ElementAt((int)counterClockwiseVertices[i]), counterClockwiseVertices[i]);
            }

            Console.WriteLine("Added all vertices to the queue.");

            uint[] vertexArray = vertexQueue.Values.Reverse().ToArray();

            List<uint> stack = new List<uint>();
            stack.Add(vertexArray[0]);
            stack.Add(vertexArray[1]);

            for(int j = 2; j < vertexArray.Length - 1; j++)
            {
                // TODO: interior probably only works for the initial polygon but not for a partition
                if (InteriorToTheRight(stack.Last(), counterClockwiseVertices) != InteriorToTheRight(vertexArray[j], counterClockwiseVertices)) // on different sides
                {
                    Console.WriteLine("Different chain.");
                    for (int i = stack.Count - 1; i > 0; i--)
                    {
                        Connect(vertexArray[j], stack[i]);
                        Console.WriteLine("Added edge: (" + vertexArray[j] + ", " + stack[i] + ")");
                    }

                    stack.Clear();

                    stack.Add(vertexArray[j - 1]);
                    stack.Add(vertexArray[j]);
                }
                else
                {
                    Console.WriteLine("Same chain.");
                    while (stack.Count > 1 && DiagonalIsInside(vertexArray[j], stack[stack.Count - 2], stack[stack.Count - 1], counterClockwiseVertices))
                    {
                        stack.RemoveAt(stack.Count - 1);
                        Connect(vertexArray[j], stack.Last());
                        Console.WriteLine("Added edge: (" + vertexArray[j] + ", " + stack.Last() + ")");
                    }

                    stack.Add(vertexArray[j]);
                }
            }

            for(int j = 1; j < stack.Count - 1; j++)
            {
                Connect(vertexArray.Last(), stack[j]);
                Console.WriteLine("Added edge: (" + vertexArray.Last() + ", " + stack[j] + ")");
            }

        }

        private bool DiagonalIsInside(uint v0, uint v1, uint prevPopped, List<uint> counterClockwiseVertices)
        {
            Vertex lastDiagonal = vertices.ElementAt((int)prevPopped) - vertices.ElementAt((int)v0);
            Vertex potentialDiagonal = vertices.ElementAt((int)v1) - vertices.ElementAt((int)v0);
            bool interiorToTheRight = InteriorToTheRight(v0, counterClockwiseVertices);

            float dxzLast = lastDiagonal.x / lastDiagonal.z;
            float dxzPot = potentialDiagonal.x / potentialDiagonal.z;

            if(interiorToTheRight)
            {
                return dxzPot > dxzLast;
            }
            else
            {
                return dxzPot < dxzLast;
            }


        }

        bool EdgeExists(Edge e)
        {
            // amortized constant runtime
            return outgoing[e.v0].Contains(e);
        }

        public void Triangulate()
        {
            MakeMonotone();
            List<List<uint>> monotonePolygons = ExtractMonotonePolygons();

            foreach (List<uint> polygon in monotonePolygons)
            {
                Console.Write("Polygon vertices: ");
                foreach (uint vertexIndex in polygon)
                {
                    Console.Write(vertexIndex + ", ");
                }
                Console.WriteLine();

               TriangulateMonotonePolygon(polygon);
            }
        }

        public List<ISDLElement> MakeTriangleFans()
        {
            List<ISDLElement> fans = new List<ISDLElement>();

            int[] colors = new int[vertices.Count()];
            HashSet<Edge> visited = new HashSet<Edge>();
            List<Edge> queue = new List<Edge>();

            for(int i = 0; i < colors.Length; i++)
            {
                colors[i] = 0;
            }


            queue.Add(edges[0]);

            colors[edges[0].v0] = 1;
            colors[edges[0].v1] = 2;

            while (queue.Count > 0)
            {
                Edge current;

                do
                {
                    current = queue[0];
                    queue.RemoveAt(0);
                } while (visited.Contains(current) && queue.Count > 0);

                if (visited.Contains(current)) break; // queue empty

                visited.Add(current);

                Edge e1 = DetermineOutgoing(current);
                visited.Add(e1);

                if (colors[e1.v1] == 0)
                {
                    // We use a bitmask consisting of 3 bits where each bit represents a color.
                    if (colors[current.v0] + colors[current.v1] < 8)
                    {
                        colors[e1.v1] = 7 - colors[current.v0] - colors[current.v1];
                    }
                    else
                    {
                        // TODO: maybe using color 4 would be better in some cases
                        if(colors[current.v0] == 8)
                        {
                            if(colors[current.v1] == 1)
                            {
                                colors[e1.v1] = 2;
                            }
                            else
                            {
                                colors[e1.v1] = 1;
                            }
                        }
                        else
                        {
                            if (colors[current.v0] == 1)
                            {
                                colors[e1.v1] = 2;
                            }
                            else
                            {
                                colors[e1.v1] = 1;
                            }
                        }
                    }
                }
                // found a circle (only possible in polygon with holes)
                else if (colors[e1.v1] != 7 - colors[current.v0] - colors[current.v1]) 
                {
                    // need to assign 4th color
                    colors[e1.v1] = 8;
                }

                // for now just create a triangle fan consisting of just one triangle
                List<Vertex> fanVertices = new List<Vertex>(3)
                {
                    vertices.ElementAt((int)current.v1),
                    vertices.ElementAt((int)current.v0),
                    vertices.ElementAt((int)e1.v1)
                };

                TriangleFanElement tri = new TriangleFanElement("s_grass", fanVertices);
                fans.Add(tri);

                if(EdgeExists(e1.Twin()))
                {
                    queue.Add(e1.Twin());
                }

                Edge e2 = DetermineOutgoing(e1);
                visited.Add(e2);

                if (EdgeExists(e2.Twin()))
                {
                    queue.Add(e2.Twin());
                }
            }

            // TODO: count each color and use all vertices of the color with the smallest count as fan pivots. Then use the 4th color to find any missing triangles
            return fans;
        }
    }

    class VertexComparer : IComparer<Vertex>
    {
        public int Compare(Vertex v0, Vertex v1)
        {
            if(v0.z == v1.z)
            {
                if (v1.x == v0.x) return 0;

                // WTF Unity? Apparently the sign of 0 is positive.
                return (int)Mathf.Sign(v1.x - v0.x);
            }

            return (int)Mathf.Sign(v0.z - v1.z);
        }
    }
}
