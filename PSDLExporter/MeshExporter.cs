using ColossalFramework;
using ColossalFramework.UI;
using PSDL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PSDLExporter
{
    class MeshExporter
    {
        public static readonly uint NETNODE_COUNT = 32768u;
        public static readonly uint NETSEGMENT_COUNT = 36864u;

        public static List<string> warnings = new List<string>();

        public void RetrieveData(string style = "f")
        {
            NetManager nm = Singleton<NetManager>.instance;

            // find and mark intersections

            MeshBuilder meshBuilder = new MeshBuilder();
            List<Room> rooms = new List<Room>();

            SortedDictionary<ushort, CSIntersection> intersections = meshBuilder.CreateAllIntersections(style);

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
                        CSRoad road = new CSRoad(intersec.NodeID, segIndex, intersections, style);
                        road.BuildRoadBlock();

                        // test road analyzer

                        /*float roadWidth = RoadAnalyzer.DetermineRoadWidth(segIndex);
                        Debug.Log("Road width: " + roadWidth);

                        float sidewalkWidth = RoadAnalyzer.DetermineSidewalkWidth(segIndex);
                        Debug.Log("Sidewalk width: " + sidewalkWidth);*/

                        // roads are identified by either start or end segment
                        roads.Add(segIndex, road);
                        // need to check this in case road consists of a single segment
                        if(segIndex != road.SegmentIDs.Last()) roads.Add(road.SegmentIDs.Last(), road);

                        rooms.Add(road.Room);
                    }
                }
            }

            // generate ground
            try
            {
                // a ground tile can be identified by a pair of segments adjacent to an intersection that border the ground tile.
                Dictionary<KeyValuePair<ushort, ushort>, CSGround> groundTiles = new Dictionary<KeyValuePair<ushort, ushort>, CSGround>();

                // TODO: allow duplicate keys!
                SortedList<float, CSGround> groundTilesSortedByBoundingBoxArea = new SortedList<float, CSGround>();


                foreach (CSIntersection intersec in intersections.Values)
                {
                    ushort[] adjacentSegments = intersec.AdjacentSegments.Values.ToArray();


                    for (int i = 0; i < adjacentSegments.Length; i++)
                    {
                        KeyValuePair<ushort, ushort> identifier = new KeyValuePair<ushort, ushort>(adjacentSegments[i], adjacentSegments[(i + 1) % adjacentSegments.Length]);

                        if (groundTiles.ContainsKey(identifier)) continue;

                        // now setup ground tile
                        CSGround ground = new CSGround(identifier, intersec);

                        try
                        {
                            ground.FindAdjacentRoadsAndIntersections(roads);

                            foreach (KeyValuePair<ushort, ushort> id in ground.IdentificationSegments.ToArray())
                            {
                                groundTiles.Add(id, ground);
                            }
                            //ground.ConstructRoom();                 
                            groundTilesSortedByBoundingBoxArea.Add(ground.CalculateBoundingBoxArea(), ground);
                        }
                        catch(Exception ex)
                        {
                            warnings.Add("Ground generation failed at traversing boundary! Details: " + ex.Message + " at " + ex.StackTrace);
                        }
                  
                    }
                }

                // ground tile with the largest bounding box is the outer one that encapsulates all roads. For now, we will ignore it.
                groundTilesSortedByBoundingBoxArea.Remove(groundTilesSortedByBoundingBoxArea.ToArray().Last().Key);

                foreach (CSGround groundTile in groundTilesSortedByBoundingBoxArea.Values)
                {
                    try
                    {
                        groundTile.ScanHeight();
                        groundTile.ConstructRoom();
                        rooms.Add(groundTile.Room);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add("Ground generation failed at constructing room! Details: " + ex.Message + " at " + ex.StackTrace);
                    }
                }
            }
            catch(Exception ex)
            {
                warnings.Add("Ground generation failed! Details: " + ex.Message + " at " + ex.StackTrace);
            }

            PSDLFile file = new PSDLFile();

            file.Rooms = rooms;
            file.Version = 0;

            //TODO: filter other illegal characters and generate unique city basename
            string psdlLocation = @"PSDLExporter\Exports\" + Regex.Replace(Singleton<SimulationManager>.instance.m_metaData.m_CityName, @"\s+", "_")
                + "_" + Singleton<SimulationManager>.instance.m_metaData.m_WorkshopPublishedFileId.AsUInt64 + ".psdl";           

            if (File.Exists(psdlLocation)) File.Delete(psdlLocation);
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

            // accumulate warnings
            string warningMessage = "WARNINGS:" + Environment.NewLine;

            if (warnings.Count == 0)
            {
                warningMessage += "(none)" + Environment.NewLine;
            }
            else
            {
                foreach (string w in warnings)
                {
                    warningMessage += w + Environment.NewLine;
                }
            }
            warnings.Clear();

            ExceptionPanel panel2 = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
            panel2.SetMessage("PSDLExporter", "Successfully exported PSDL." + Environment.NewLine + Environment.NewLine + warningMessage, false);
        }
    }
}
