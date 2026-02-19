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

    public struct OsmRelationMember
    {
        public string Type;  // "way", "node", "relation"
        public long Ref;
        public string Role;  // "outer", "inner", ""

        public OsmRelationMember(string type, long refId, string role)
        {
            Type = type;
            Ref = refId;
            Role = role;
        }
    }

    public class OsmRelation
    {
        public long Id;
        public List<OsmRelationMember> Members;
        public Dictionary<string, string> Tags;

        public OsmRelation(long id)
        {
            Id = id;
            Members = new List<OsmRelationMember>();
            Tags = new Dictionary<string, string>();
        }

        public void AddMember(string type, long refId, string role)
        {
            Members.Add(new OsmRelationMember(type, refId, role));
        }

        public void AddTag(string key, string value)
        {
            Tags[key] = value;
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
        public Dictionary<long, OsmWay> WaysById;
        public List<OsmRelation> Relations;

        public OsmData()
        {
            Nodes = new Dictionary<long, OsmNode>();
            Ways = new List<OsmWay>();
            WaysById = new Dictionary<long, OsmWay>();
            Relations = new List<OsmRelation>();
        }

        public void AddNode(OsmNode node)
        {
            Nodes[node.Id] = node;
        }

        public void AddWay(OsmWay way)
        {
            Ways.Add(way);
            WaysById[way.Id] = way;
        }

        public void AddRelation(OsmRelation relation)
        {
            Relations.Add(relation);
        }
    }
}
