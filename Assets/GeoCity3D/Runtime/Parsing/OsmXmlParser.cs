using System;
using System.Xml;
using System.Globalization;
using GeoCity3D.Data;
using UnityEngine;

namespace GeoCity3D.Parsing
{
    public class OsmXmlParser
    {
        public OsmData Parse(string xmlContent)
        {
            OsmData data = new OsmData();
            XmlDocument doc = new XmlDocument();
            
            try
            {
                doc.LoadXml(xmlContent);

                // Parse Nodes
                XmlNodeList nodes = doc.SelectNodes("//node");
                foreach (XmlNode node in nodes)
                {
                    long id = long.Parse(node.Attributes["id"].Value);
                    double lat = double.Parse(node.Attributes["lat"].Value, CultureInfo.InvariantCulture);
                    double lon = double.Parse(node.Attributes["lon"].Value, CultureInfo.InvariantCulture);

                    OsmNode osmNode = new OsmNode(id, lat, lon);
                    data.AddNode(osmNode);
                }

                // Parse Ways
                XmlNodeList ways = doc.SelectNodes("//way");
                foreach (XmlNode wayNode in ways)
                {
                    long id = long.Parse(wayNode.Attributes["id"].Value);
                    OsmWay osmWay = new OsmWay(id);

                    // Parse nd refs
                    foreach (XmlNode child in wayNode.ChildNodes)
                    {
                        if (child.Name == "nd")
                        {
                            long refId = long.Parse(child.Attributes["ref"].Value);
                            osmWay.AddNode(refId);
                        }
                        else if (child.Name == "tag")
                        {
                            string k = child.Attributes["k"].Value;
                            string v = child.Attributes["v"].Value;
                            osmWay.AddTag(k, v);
                        }
                    }

                    data.AddWay(osmWay);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing OSM XML: {e.Message}");
            }

            return data;
        }
    }
}
