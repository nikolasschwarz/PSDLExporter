using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PSDLExporter
{
    class RoadAnalyzer
    {
        private static NetManager netMan = Singleton<NetManager>.instance;
        private static readonly float LANE_WIDTH = 5.0f;
        public static readonly float SIDEWALK_WIDTH = 3.0f;

        public static bool IsCustomStyleValid(string style)
        {
            style = style.ToLower();
            if (style.Equals("l") || style.Equals("f"))
            {
                return false;
            }

            // only permit characters 
            return Regex.IsMatch(style, @"^[a-z]+$");
        }

        public static bool IsOneway(ushort segmentID)
        {
            int l = 0;
            int r = 0;
            netMan.m_segments.m_buffer[segmentID].CountLanes(segmentID, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.All, ref r, ref l);

            return r == 0 || l == 0;
        }

        public static bool IsAsymmetric(ushort segmentID)
        {
            int l = 0;
            int r = 0;
            netMan.m_segments.m_buffer[segmentID].CountLanes(segmentID, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.All, ref r, ref l);

            // we do not consider a oneway to be asymmetric because a symmetric texture can be used
            if (r == 0 || l == 0) return false;

            return r != l;
        }

        public static int CountCarLanes(ushort segmentID)
        {
            int l = 0;
            int r = 0;
            netMan.m_segments.m_buffer[segmentID].CountLanes(segmentID, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.All, ref r, ref l);

            return r + l;
        }

        public static Mesh GetMesh(ushort segmentID)
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

            return meshToAnalyze;
        }

        public static IntervalSet<float> FindPlateaus(ushort segmentID)
        {
            Mesh meshToAnalyze = GetMesh(segmentID);
            IntervalSet<float> plateaus = new IntervalSet<float>();

            for (int i = 0; i < meshToAnalyze.triangles.Length; i += 3)
            {
                // for each edge check if it is on road level and add it to the interval set
                int i0 = meshToAnalyze.triangles[i];
                int i1 = meshToAnalyze.triangles[i + 1];
                int i2 = meshToAnalyze.triangles[i + 2];

                Vector3 v0 = meshToAnalyze.vertices[i0];
                Vector3 v1 = meshToAnalyze.vertices[i1];
                Vector3 v2 = meshToAnalyze.vertices[i2];

                float maxElevation = Mathf.Max(v0.y, v1.y, v2.y);
                float minElevation = Mathf.Min(v0.y, v1.y, v2.y);

                if (maxElevation - minElevation < Mathf.Epsilon)
                {
                    // part of plateau
                    plateaus.UnionInterval(new Interval<float>(minElevation, maxElevation));
                }
            }

            return plateaus;
        }

        public static IntervalSet<float> AnalyzeRoadAtLevel(ushort segmentID, float level)
        {
            Mesh meshToAnalyze = GetMesh(segmentID);

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

                //if ( minElevation > level - Mathf.Epsilon && maxElevation < level + Mathf.Epsilon)
                if (Mathf.Abs(v2.y - level) < Mathf.Epsilon && Mathf.Abs(v1.y - level) < Mathf.Epsilon && Mathf.Abs(v0.y - level) < Mathf.Epsilon)
                {
                    // part of plateau
                    float maxX = Mathf.Max(v0.x, v1.x, v2.x);
                    float minX = Mathf.Min(v0.x, v1.x, v2.x);

                    onRoadLevel.UnionInterval(new Interval<float>(minX, maxX));
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
            List<IntervalSet<float>> plateauProfile = new List<IntervalSet<float>>();

            IntervalSet<float> plateaus = FindPlateaus(segmentID);

            foreach (Interval<float> interval in plateaus.Intervals)
            {
                Debug.Log("Interval: " + interval.lower + ", " + interval.upper);
                IntervalSet<float> plateau = AnalyzeRoadAtLevel(segmentID, (interval.lower + interval.upper) * 0.5f);
                plateauProfile.Add(plateau);
            }

            IntervalSet<float> onRoadLevel = plateauProfile[0];//AnalyzeRoadAtLevel(segmentID, -0.3f);

            IntervalSet<float> onSidewalkLevel = new IntervalSet<float>();

            if (plateauProfile.Count > 1)
            {
                onSidewalkLevel = plateauProfile[1];
            }

            Debug.Log("roadLevel: " + onRoadLevel.Intervals.Count);
            Debug.Log("sidewalkLevel: " + onSidewalkLevel.Intervals.Count);

            float[] profile;

            if (onRoadLevel.Intervals.Count == 1)
            {
                // normal road
                profile = new float[4];

                if (onSidewalkLevel.Intervals.Count > 2)
                {
                    throw new Exception("Road type either not recognized or not supported!");
                }

                if (onSidewalkLevel.Intervals.Count == 0)
                {
                    // road without sidewalk
                    profile[0] = profile[1] = onRoadLevel.Intervals[0].lower;
                    profile[2] = profile[3] = onRoadLevel.Intervals[0].upper;
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

        public static string SidewalkTexture(string style)
        {
            if (style.Equals("l"))
            {
                return "swalk_stone_l";
            }

            return "swalk_" + style;
        }

        public static string SidewalkIntersectionTexture(string style)
        {
            if (style.Equals("l"))
            {
                return "swalk_stone02_l";
            }

            return "swalk_inter_" + style;
        }

        public static string CrosswalkTexture(string style)
        {
            if (style.Equals("l"))
            {
                return "rxwalk02_l";
            }

            return "rxwalk_" + style;
        }

        public static string IntersectionTexture(string style)
        {
            return "rinter_" + style;
        }

        public static string DetermineRoadTexture(int laneCount, bool isOneway, string style, bool lod = false)
        {

            string attributes = "";

            if (isOneway && laneCount % 2 == 0 && !style.Equals("l"))
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

        public enum ElevationType
        {
            Standard,
            Elevated,
            Bridge, // TODO
            Tunnel
        };

        public static ElevationType DetermineElevationType(ushort segmentID)
        {
            NetSegment segment = RoadUtils.GetNetSegment(segmentID);

            if (segment.Info.m_netAI.IsOverground()) return ElevationType.Elevated;
            if (segment.Info.m_netAI.IsUnderground()) return ElevationType.Tunnel;

            return ElevationType.Standard;
        }
    }
}
