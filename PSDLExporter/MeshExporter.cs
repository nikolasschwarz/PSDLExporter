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

            SortedDictionary<ushort, CSRoad> roads = new SortedDictionary<ushort, CSRoad>();

            foreach (CSIntersection intersec in intersections.Values)
            {
                foreach (ushort segIndex in intersec.AdjacentSegments.Values)
                {
                    // make sure road has not been built already starting from the other side.
                    if (!roads.ContainsKey(segIndex))
                    {
                        CSRoad road = new CSRoad(intersec.NodeID, segIndex, intersections);
                        road.BuildRoadBlock();

                        // roads are identified by either start or end segment
                        roads.Add(segIndex, road);
                        // need to check this in case road consists of a single segment
                        if(segIndex != road.SegmentIDs.Last()) roads.Add(road.SegmentIDs.Last(), road);

                        rooms.Add(road.Room);
                    }
                }
            }

            // a ground tile can be identified by a pair of segments adjacent to an intersection that border the ground tile.
            Dictionary<KeyValuePair<ushort, ushort>, CSGround> groundTiles = new Dictionary<KeyValuePair<ushort, ushort>, CSGround>();

            // TODO: allow duplicate keys!
            SortedList<float, CSGround> groundTilesSortedByBoundingBoxArea = new SortedList<float, CSGround>();

            foreach (CSIntersection intersec in intersections.Values)
            {
                ushort[] adjacentSegments = intersec.AdjacentSegments.Values.ToArray();


                for(int i = 0; i < adjacentSegments.Length; i++)
                {
                    KeyValuePair<ushort, ushort> identifier = new KeyValuePair<ushort, ushort>(adjacentSegments[i], adjacentSegments[(i + 1) % adjacentSegments.Length]);

                    if (groundTiles.ContainsKey(identifier)) continue;

                    // now setup ground tile
                    CSGround ground = new CSGround(identifier, intersec);

                    ground.FindAdjacentRoadsAndIntersections(roads);
                    ground.ConstructRoom();

                    foreach(KeyValuePair<ushort, ushort> id in ground.IdentificationSegments.ToArray())
                    {
                        groundTiles.Add(id, ground);
                    }

                    groundTilesSortedByBoundingBoxArea.Add(ground.CalculateBoundingBoxArea(), ground);


                }
            }

            // ground tile with the largest bounding box is the outer one that encapsulates all roads. For now, we will ignore it.
            groundTilesSortedByBoundingBoxArea.Remove(groundTilesSortedByBoundingBoxArea.ToArray().Last().Key);

            foreach(CSGround groundTile in groundTilesSortedByBoundingBoxArea.Values)
            {
                rooms.Add(groundTile.Room);
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
