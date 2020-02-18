using ColossalFramework;
using ColossalFramework.UI;
using PSDL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PSDLExporter
{
    class MeshExporter
    {
        public static readonly uint NETNODE_COUNT = 32768u;
        public static readonly uint NETSEGMENT_COUNT = 36864u;

        public void RetrieveData()
        {
            NetManager nm = Singleton<NetManager>.instance;            

            // find and mark intersections

            MeshBuilder meshBuilder = new MeshBuilder();
            List<Room> rooms = new List<Room>();

            SortedDictionary<ushort, CSIntersection> intersections = meshBuilder.CreateAllIntersections();

            foreach (CSIntersection intersection in intersections.Values)
            {
                rooms.Add(intersection.Room);
            }

            SortedDictionary<uint, uint> alreadyBuilt = new SortedDictionary<uint, uint>();

            foreach (CSIntersection intersec in intersections.Values)
            {
                foreach (ushort segIndex in intersec.AdjacentSegments.Values)
                {
                    if (!alreadyBuilt.ContainsKey(segIndex))
                    {
                        CSRoad road = new CSRoad(intersec.NodeID, segIndex, intersections);                 
                        road.BuildRoadBlock();

                        rooms.Add(road.Room);
                    }
                }
            }

            PSDLFile file = new PSDLFile();

            file.Rooms = rooms;
            file.Version = 0;

            string psdlLocation = @"D:\Games\SteamLibrary\steamapps\common\Cities_Skylines\test.psdl";
            if(File.Exists(psdlLocation)) File.Delete(psdlLocation);
            file.SaveAs(psdlLocation);

            // debug
            Debug.Log("There are " + file.Vertices.Count + " vertices and " + file.Rooms.Count + " rooms.");

            StreamWriter writer = new StreamWriter(@"D:\Games\SteamLibrary\steamapps\common\Cities_Skylines\perimeters.svg");
            writer.WriteLine("<svg height=\"210\" width=\"500\">");

            foreach (Room r in rooms)
            {
                writer.WriteLine(MeshBuilder.DrawPerimeter(r.Perimeter));
                Debug.Log("Perimeter has " + r.Perimeter.Count + " vertices.");
            }

            writer.WriteLine("</ svg >");
            writer.Flush();
            writer.Close();

            ExceptionPanel panel2 = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
            panel2.SetMessage("PSDLExporter", "Successfully exported PSDL.", false);
        }
    }
}
