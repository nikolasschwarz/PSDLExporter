using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PSDLExporter
{
    // provides several static methods that are useful or convenient for processing cities skylines roads
    class RoadUtils
    {
        private static NetManager netMan = Singleton<NetManager>.instance;

        public static NetSegment GetNetSegment(ushort segmentID)
        {
            return netMan.m_segments.m_buffer[segmentID];
        }

        public static NetNode GetNetNode(ushort nodeID)
        {
            return netMan.m_nodes.m_buffer[nodeID];
        }

        public static NetInfo GetNetInfo(ushort segmentID)
        {
             return GetNetSegment(segmentID).Info;
        }

        public static ushort GetNextNode(ushort nodeIndex, ushort segmentIndex)
        {
            if (netMan.m_segments.m_buffer[segmentIndex].m_startNode == nodeIndex)
                return netMan.m_segments.m_buffer[segmentIndex].m_endNode;

            return netMan.m_segments.m_buffer[segmentIndex].m_startNode;
        }

        public static ushort GetNextSegment(ushort nodeIndex, ushort segmentIndex)
        {
            List<ushort> segments = GetAllAdjacentSegments(netMan.m_nodes.m_buffer[nodeIndex], NetInfo.LaneType.Vehicle);
            Debug.Assert(segments.Count == 2);

            if (segments[0] == segmentIndex)
                return segments[1];

            return segments[0];
        }

        public static List<ushort> GetAllAdjacentSegments(NetNode node, NetInfo.LaneType laneType)
        {
            List<ushort> segmentIDs = new List<ushort>();

            if ((netMan.m_segments.m_buffer[node.m_segment0].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment0].CountLanes(node.m_segment0, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment0);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment1].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment1].CountLanes(node.m_segment1, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment1);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment2].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment2].CountLanes(node.m_segment2, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment2);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment3].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment3].CountLanes(node.m_segment3, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment3);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment4].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment4].CountLanes(node.m_segment4, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment4);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment5].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment5].CountLanes(node.m_segment5, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment5);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment6].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment6].CountLanes(node.m_segment6, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment6);
            }

            if ((netMan.m_segments.m_buffer[node.m_segment7].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                int l = 0;
                int r = 0;
                netMan.m_segments.m_buffer[node.m_segment7].CountLanes(node.m_segment7, laneType, VehicleInfo.VehicleType.All, ref r, ref l);
                if (l + r > 0) segmentIDs.Add(node.m_segment7);
            }

            return segmentIDs;
        }
    }
}
