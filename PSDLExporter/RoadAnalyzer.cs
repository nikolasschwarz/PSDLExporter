using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PSDLExporter
{
    class RoadAnalyzer
    {
        private static NetManager netMan = Singleton<NetManager>.instance;
        private static readonly float LANE_WIDTH = 5.0f;
        public static readonly float SIDEWALK_WIDTH = 3.0f;

        public static bool IsOneway(ushort segmentID)
        {
            int l = 0;
            int r = 0;
            netMan.m_segments.m_buffer[segmentID].CountLanes(segmentID, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.All, ref r, ref l);

            return r == 0 || l == 0;
        }

        public static int CountCarLanes(ushort segmentID)
        {
            int l = 0;
            int r = 0;
            netMan.m_segments.m_buffer[segmentID].CountLanes(segmentID, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.All, ref r, ref l);

            return r + l;
        }

        public static IntervalSet<float> AnalyzeRoadAtLevel(ushort segmentID, float level)
        {
            NetSegment segment = RoadUtils.GetNetSegment(segmentID);

            // only analyze first segment model; that should suffice
            NetInfo.Segment segmentInfo = segment.Info.m_segments[0];

            Mesh meshToAnalyze = segmentInfo.m_mesh;

            // analogous to ModTools
            if (!meshToAnalyze.isReadable)
            {
                Debug.Log($"Mesh \"{meshToAnalyze.name}\" is marked as non-readable, running workaround..");

                try
                {
                    // copy the relevant data to the temporary mesh
                    meshToAnalyze = new Mesh
                    {
                        vertices = segmentInfo.m_mesh.vertices,
                        colors = segmentInfo.m_mesh.colors,
                        triangles = segmentInfo.m_mesh.triangles,
                        normals = segmentInfo.m_mesh.normals,
                        tangents = segmentInfo.m_mesh.tangents,
                    };
                    meshToAnalyze.RecalculateBounds();
                }
                catch (Exception ex)
                {
                    Debug.Log($"Workaround failed with error - {ex.Message}");
                }
            }

            IntervalSet<float> onRoadLevel = new IntervalSet<float>();

            // now loop through triangles

            for (int i = 0; i < meshToAnalyze.triangles.Length; i += 3)
            {
                // for each edge check if it is on road level and add it to the interval set
                int i0 = meshToAnalyze.triangles[i];
                int i1 = meshToAnalyze.triangles[i + 1];
                int i2 = meshToAnalyze.triangles[i + 2];

                Vector3 v0 = meshToAnalyze.vertices[i0];
                Vector3 v1 = meshToAnalyze.vertices[i1];
                Vector3 v2 = meshToAnalyze.vertices[i2];

                if (Mathf.Abs(v0.y - level) < Mathf.Epsilon && Mathf.Abs(v1.y - level) < Mathf.Epsilon)
                {
                    onRoadLevel.UnionInterval(new Interval<float>(v0.x, v1.x));
                }

                if (Mathf.Abs(v1.y - level) < Mathf.Epsilon && Mathf.Abs(v2.y - level) < Mathf.Epsilon)
                {
                    onRoadLevel.UnionInterval(new Interval<float>(v1.x, v2.x));
                }

                if (Mathf.Abs(v2.y - level) < Mathf.Epsilon && Mathf.Abs(v0.y - level) < Mathf.Epsilon)
                {
                    onRoadLevel.UnionInterval(new Interval<float>(v2.x, v0.x));
                }
            }

            return onRoadLevel;
        }

        public static float DetermineRoadWidth(ushort segmentID)
        {
            IntervalSet<float> onRoadLevel = AnalyzeRoadAtLevel(segmentID, -0.3f);

            Debug.Log("Road consists of " + onRoadLevel.Intervals.Count + " intervals with road geometry");

            return onRoadLevel.Intervals.Last().upper - onRoadLevel.Intervals[0].lower;
        }

        public static float DetermineSidewalkWidth(ushort segmentID)
        {
            IntervalSet<float> onSidewalkLevel = AnalyzeRoadAtLevel(segmentID, 0.0f);

            Debug.Log("Road consists of " + onSidewalkLevel.Intervals.Count + " intervals with sidewalk or median geometry");

            return onSidewalkLevel.Intervals[0].upper - onSidewalkLevel.Intervals[0].lower;
        }

        public static float[] RoadProfile(ushort segmentID)
        {
            // TODO: A much safer method would be to search for plateaus and sort them by altitude to identify components
            IntervalSet<float> onRoadLevel = AnalyzeRoadAtLevel(segmentID, -0.3f);

            IntervalSet<float> onSidewalkLevel = AnalyzeRoadAtLevel(segmentID, 0.0f);

            float[] profile;

            if(onRoadLevel.Intervals.Count == 1)
            {
                // normal road
                profile = new float[4];

                if(onSidewalkLevel.Intervals.Count > 2)
                {
                    throw new Exception("Road type either not recognized or not supported!");
                }
                
                if(onSidewalkLevel.Intervals.Count == 0)
                {
                    // road without sidewalk
                    profile[0] = profile[1] = onRoadLevel.Intervals[0].lower;
                    profile[2] = profile[3] = onRoadLevel.Intervals[0].upper;
                }
                else
                {
                    // left side
                    if(onSidewalkLevel.Intervals[0].lower < onRoadLevel.Intervals[0].lower)
                    {
                        // left sidewalk exists
                        profile[0] = onSidewalkLevel.Intervals[0].lower;
                        profile[1] = onRoadLevel.Intervals[0].lower;
                    }
                    else
                    {
                        profile[0] = profile[1] = onRoadLevel.Intervals[0].lower;
                    }

                    // right side
                    if (onSidewalkLevel.Intervals.Last().upper > onRoadLevel.Intervals.Last().upper)
                    {
                        // right sidewalk exists
                        profile[2] = onRoadLevel.Intervals.Last().upper;
                        profile[3] = onSidewalkLevel.Intervals.Last().upper;
                    }
                    else
                    {
                        profile[2] = profile[3] = onRoadLevel.Intervals[0].upper;
                    }
                }

                Debug.Log("Profile: " + profile[0] + ", " + profile[1] + ", " + profile[2] + ", " + profile[3]);
            }
            else if (onRoadLevel.Intervals.Count == 2)
            {
                // divided road
                profile = new float[6];

                if (onSidewalkLevel.Intervals.Count > 3)
                {
                    throw new Exception("Road type either not recognized or not supported!");
                }

                if (onSidewalkLevel.Intervals.Count == 1)
                {
                    // road without sidewalk
                    profile[0] = profile[1] = onRoadLevel.Intervals[0].lower;
                    profile[4] = profile[5] = onRoadLevel.Intervals[0].upper;
                }
                else
                {
                    // left side
                    if (onSidewalkLevel.Intervals[0].lower < onRoadLevel.Intervals[0].lower)
                    {
                        // left sidewalk exists
                        profile[0] = onSidewalkLevel.Intervals[0].lower;
                        profile[1] = onRoadLevel.Intervals[0].lower;
                        
                    }
                    else
                    {
                        profile[0] = profile[1] = onRoadLevel.Intervals[0].lower;
                    }

                    // right side
                    if (onSidewalkLevel.Intervals.Last().upper > onRoadLevel.Intervals.Last().upper)
                    {
                        // right sidewalk exists
                        profile[4] = onRoadLevel.Intervals.Last().upper;
                        profile[5] = onSidewalkLevel.Intervals.Last().upper;
                    }
                    else
                    {
                        profile[4] = profile[5] = onRoadLevel.Intervals[0].upper;
                    }
                }

                profile[2] = onRoadLevel.Intervals[0].upper;
                profile[3] = onRoadLevel.Intervals.Last().lower;
            }
            else
            {
                throw new Exception("Road type either not recognized or not supported!");
            }

            return profile;
        }

        public static string DetermineRoadTexture(int laneCount, bool isOneway, string style, bool lod = false)
        {

            string attributes = "";

            if(isOneway && laneCount % 2 == 0 && !style.Equals("l"))
            {
                attributes += "_oneway";
            }

            string textureName = "r" + laneCount + attributes + "_" + (lod ? "lo_" : "") + style;

            if (
                laneCount > 8 ||
                laneCount == 7 ||
                laneCount == 5 ||
                (laneCount == 8 && (lod || style.Equals("l"))) ||
                (laneCount == 6 && isOneway && style.Equals("f")) ||
                (laneCount == 3 && style.Equals("f")) ||
                (!style.Equals("f") && !style.Equals("l"))
            )
            {
                MeshExporter.warnings.Add("No appropriate default " + (lod ? "LOD " : "") + "texture exists for a " + laneCount + "-lane "
                    + (isOneway ? "oneway" : "twoway") + " road! Make sure to provide the texture yourself and name it " + textureName);
            }

            return textureName;
        }
    }
}
