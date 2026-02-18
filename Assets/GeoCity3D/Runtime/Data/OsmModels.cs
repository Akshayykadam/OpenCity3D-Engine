using System.Collections.Generic;

namespace GeoCity3D.Data
{
    public struct OsmNode
    {
        public long Id;
        public double Latitude;
        public double Longitude;

        public OsmNode(long id, double lat, double lon)
        {
            Id = id;
            Latitude = lat;
            Longitude = lon;
        }
    }

    public class OsmWay
    {
        public long Id;
        public List<long> NodeIds;
        public Dictionary<string, string> Tags;

        public OsmWay(long id)
        {
            Id = id;
            NodeIds = new List<long>();
            Tags = new Dictionary<string, string>();
        }

        public void AddNode(long nodeId)
        {
            NodeIds.Add(nodeId);
        }

        public void AddTag(string key, string value)
        {
            Tags[key] = value;
        }

        public bool HasTag(string key)
        {
            return Tags.ContainsKey(key);
        }

        public string GetTag(string key)
        {
            return Tags.ContainsKey(key) ? Tags[key] : null;
        }
    }

    public class OsmData
    {
        public Dictionary<long, OsmNode> Nodes;
        public List<OsmWay> Ways;
        // In the future, we might add Relations

        public OsmData()
        {
            Nodes = new Dictionary<long, OsmNode>();
            Ways = new List<OsmWay>();
        }

        public void AddNode(OsmNode node)
        {
            Nodes[node.Id] = node;
        }

        public void AddWay(OsmWay way)
        {
            Ways.Add(way);
        }
    }
}
