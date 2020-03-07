using PSDL;
using PSDL.Elements;
using PSDLExporter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Testing
{
    class TriangulationTest
    {
        public TriangulationTest() { }

        public void Run()
        {
            //TestRegularPolygon();
            TestPolygonWithHoles();
        }

        public void TestRegularPolygon()
        {
            Vertex[] vertices = new Vertex[8];

            vertices[0] = new Vertex(3.5f, 0.0f, 0.0f);
            vertices[1] = new Vertex(1.0f, 0.0f, 2.0f);
            vertices[2] = new Vertex(0.0f, 0.0f, 1.5f);
            vertices[3] = new Vertex(-2.0f, 0.0f, 3.5f);
            vertices[4] = new Vertex(-2.0f, 0.0f, 0.0f);
            vertices[5] = new Vertex(-2.0f, 0.0f, -2.0f);
            vertices[6] = new Vertex(0.0f, 0.0f, -1.0f);
            vertices[7] = new Vertex(2.0f, 0.0f, -2.0f);

            List<Edge> edges = new List<Edge>
            {
                new Edge(0, 1),
                new Edge(1, 2),
                new Edge(2, 3),
                new Edge(3, 4),
                new Edge(4, 5),
                new Edge(5, 6),
                new Edge(6, 7),
                new Edge(7, 0)
            };

            PolygonTriangulator pt = new PolygonTriangulator(vertices, edges);

            pt.MakeMonotone();

            List<List<uint>> monotonePolygons = pt.ExtractMonotonePolygons();

        }

        public void TestPolygonWithHoles()
        {
            /*Vertex[] vertices = new Vertex[12];

            vertices[0] = new Vertex(3.5f, 0.0f, 0.0f);
            vertices[1] = new Vertex(1.0f, 0.0f, 2.0f);
            vertices[2] = new Vertex(0.0f, 0.0f, 1.5f);
            vertices[3] = new Vertex(-2.0f, 0.0f, 3.5f);
            vertices[4] = new Vertex(-2.0f, 0.0f, 0.0f);
            vertices[5] = new Vertex(-2.0f, 0.0f, -2.0f);
            vertices[6] = new Vertex(0.0f, 0.0f, -1.0f);
            vertices[7] = new Vertex(2.0f, 0.0f, -2.0f);

            // hole
            vertices[8] = new Vertex(1.0f, 0.0f, 0.0f);
            vertices[9] = new Vertex(0.0f, 0.0f, -0.5f);
            vertices[10] = new Vertex(-1.0f, 0.0f, 0.0f);
            vertices[11] = new Vertex(0.0f, 0.0f, 0.5f);

            List<Edge> edges = new List<Edge>
            {
                new Edge(0, 1),
                new Edge(1, 2),
                new Edge(2, 3),
                new Edge(3, 4),
                new Edge(4, 5),
                new Edge(5, 6),
                new Edge(6, 7),
                new Edge(7, 0),

                new Edge(8, 9),
                new Edge(9, 10),
                new Edge(10, 11),
                new Edge(11, 8),
            };*/

            Vertex[] vertices = new Vertex[29];
            vertices[0] = new Vertex(60.24692f, 124.5327f, -743.5205f);
            vertices[1] = new Vertex(59.65104f, 124.5327f, -742.2241f);
            vertices[2] = new Vertex(58.49573f, 124.5327f, -741.3867f);
            vertices[3] = new Vertex(56.78098f, 124.5327f, -741.0084f);
            vertices[4] = new Vertex(13.29119f, 123.6594f, -737.7663f);
            vertices[5] = new Vertex(-26.85354f, 124.6713f, -734.7735f);
            vertices[6] = new Vertex(-27.07785f, 126.3314f, -790.1898f);
            vertices[7] = new Vertex(-27.30984f, 127.9909f, -846.1664f);
            vertices[8] = new Vertex(34.54819f, 125.613f, -846.4199f);
            vertices[9] = new Vertex(36.36377f, 125.613f, -846.1858f);
            vertices[10] = new Vertex(37.81052f, 125.613f, -845.4672f);
            vertices[11] = new Vertex(38.88844f, 125.613f, -844.264f);
            vertices[12] = new Vertex(39.59754f, 125.613f, -842.5763f);
            vertices[13] = new Vertex(55.54837f, 124.1303f, -784.02f);
            vertices[14] = new Vertex(60.28337f, 124.5327f, -745.2761f);

            vertices[15] = new Vertex(-9.791203f, 125.9688f, -809.2044f);
            vertices[16] = new Vertex(-9.791203f, 125.625f, -790.5967f);
            vertices[17] = new Vertex(-9.791203f, 125.25f, -771.989f);
            vertices[18] = new Vertex(-9.791203f, 124.875f, -753.3812f);
            vertices[19] = new Vertex(7.72744f, 124f, -753.3812f);
            vertices[20] = new Vertex(25.24608f, 123.7813f, -753.3812f);
            vertices[21] = new Vertex(42.76472f, 123.6719f, -753.3812f);
            vertices[22] = new Vertex(42.76472f, 123.7969f, -771.989f);
            vertices[23] = new Vertex(42.76472f, 123.9375f, -790.5967f);
            vertices[24] = new Vertex(42.76472f, 124.2188f, -809.2044f);
            vertices[25] = new Vertex(42.76472f, 124.9219f, -827.8121f);
            vertices[26] = new Vertex(25.24608f, 124.9844f, -827.8121f);
            vertices[27] = new Vertex(7.72744f, 125.3125f, -827.8121f);
            vertices[28] = new Vertex(-9.791203f, 126.6406f, -827.8121f);

            List<Edge> edges = new List<Edge>
            {
                new Edge(0, 1),
                new Edge(1, 2),
                new Edge(2, 3),
                new Edge(3, 4),
                new Edge(4, 5),
                new Edge(5, 6),
                new Edge(6, 7),
                new Edge(7, 8),
                new Edge(8, 9),
                new Edge(9, 10),
                new Edge(10, 11),
                new Edge(11, 12),
                new Edge(12, 13),
                new Edge(13, 14),
                new Edge(14, 0),
                new Edge(15, 16),
                new Edge(16, 17),
                new Edge(17, 18),
                new Edge(18, 19),
                new Edge(19, 20),
                new Edge(20, 21),
                new Edge(21, 22),
                new Edge(22, 23),
                new Edge(23, 24),
                new Edge(24, 25),
                new Edge(25, 26),
                new Edge(26, 27),
                new Edge(27, 28),
                new Edge(28, 15),
            };

            PolygonTriangulator pt = new PolygonTriangulator(vertices, edges);

            pt.MakeMonotone();
            pt.Triangulate();

            List<ISDLElement> fans = pt.MakeTriangleFans();

        }
    }
}
