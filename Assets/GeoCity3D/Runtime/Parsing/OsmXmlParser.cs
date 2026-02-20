using System;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;
using GeoCity3D.Data;
using UnityEngine;

namespace GeoCity3D.Parsing
{
    public class OsmXmlParser
    {
        public OsmData Parse(string xmlContent)
        {
            OsmData data = new OsmData();
            
            try
            {
                using (StringReader sr = new StringReader(xmlContent))
                using (XmlReader reader = XmlReader.Create(sr))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "node")
                            {
                                long id = long.Parse(reader.GetAttribute("id"));
                                double lat = double.Parse(reader.GetAttribute("lat"), CultureInfo.InvariantCulture);
                                double lon = double.Parse(reader.GetAttribute("lon"), CultureInfo.InvariantCulture);
                                OsmNode osmNode = new OsmNode(id, lat, lon);
                                
                                if (!reader.IsEmptyElement)
                                {
                                    using (XmlReader inner = reader.ReadSubtree())
                                    {
                                        while (inner.Read()) {} // Drain it
                                    }
                                }
                                data.AddNode(osmNode);
                            }
                            else if (reader.Name == "way")
                            {
                                long id = long.Parse(reader.GetAttribute("id"));
                                OsmWay osmWay = new OsmWay(id);
                                
                                if (!reader.IsEmptyElement)
                                {
                                    using (XmlReader inner = reader.ReadSubtree())
                                    {
                                        while (inner.Read())
                                        {
                                            if (inner.NodeType == XmlNodeType.Element)
                                            {
                                                if (inner.Name == "nd")
                                                {
                                                    long refId = long.Parse(inner.GetAttribute("ref"));
                                                    osmWay.AddNode(refId);
                                                }
                                                else if (inner.Name == "tag")
                                                {
                                                    string k = inner.GetAttribute("k");
                                                    string v = inner.GetAttribute("v");
                                                    osmWay.AddTag(k, v);
                                                }
                                            }
                                        }
                                    }
                                }
                                data.AddWay(osmWay);
                            }
                            else if (reader.Name == "relation")
                            {
                                long id = long.Parse(reader.GetAttribute("id"));
                                OsmRelation rel = new OsmRelation(id);
                                
                                if (!reader.IsEmptyElement)
                                {
                                    using (XmlReader inner = reader.ReadSubtree())
                                    {
                                        while (inner.Read())
                                        {
                                            if (inner.NodeType == XmlNodeType.Element)
                                            {
                                                if (inner.Name == "member")
                                                {
                                                    string type = inner.GetAttribute("type") ?? "";
                                                    long refId = long.Parse(inner.GetAttribute("ref"));
                                                    string role = inner.GetAttribute("role") ?? "";
                                                    rel.AddMember(type, refId, role);
                                                }
                                                else if (inner.Name == "tag")
                                                {
                                                    string k = inner.GetAttribute("k");
                                                    string v = inner.GetAttribute("v");
                                                    rel.AddTag(k, v);
                                                }
                                            }
                                        }
                                    }
                                }
                                data.AddRelation(rel);
                            }
                        }
                    }
                }

                // Assemble multipolygon water relations into synthetic ways
                AssembleWaterRelations(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing OSM XML: {e.Message}");
            }

            return data;
        }

        /// <summary>
        /// For each multipolygon relation that represents water, chain the outer
        /// member ways together into a single synthetic OsmWay and inject it into
        /// the Ways list with the relation's tags so existing detection picks it up.
        /// </summary>
        private void AssembleWaterRelations(OsmData data)
        {
            foreach (var rel in data.Relations)
            {
                string relType = (rel.GetTag("type") ?? "").ToLower();
                // Accept multipolygon water areas and type=waterway river relations
                if (relType != "multipolygon" && relType != "waterway") continue;

                // Check if this relation represents water
                if (!IsWaterRelation(rel)) continue;

                // Collect outer member ways
                List<OsmWay> outerWays = new List<OsmWay>();
                foreach (var member in rel.Members)
                {
                    if (member.Type != "way") continue;
                    // Accept "outer" role or empty role (default = outer)
                    if (member.Role != "outer" && member.Role != "") continue;

                    if (data.WaysById.TryGetValue(member.Ref, out OsmWay memberWay))
                    {
                        outerWays.Add(memberWay);
                    }
                }

                if (outerWays.Count == 0) continue;

                // Chain the outer ways into connected rings
                List<List<long>> rings = ChainWays(outerWays);

                foreach (var ring in rings)
                {
                    if (ring.Count < 3) continue;

                    // Create a synthetic way with a unique negative ID
                    OsmWay syntheticWay = new OsmWay(-rel.Id * 1000 - rings.IndexOf(ring));
                    foreach (long nodeId in ring)
                        syntheticWay.AddNode(nodeId);

                    // Copy the relation's tags onto the synthetic way
                    foreach (var kvp in rel.Tags)
                        syntheticWay.AddTag(kvp.Key, kvp.Value);

                    data.AddWay(syntheticWay);
                }
            }
        }

        private bool IsWaterRelation(OsmRelation rel)
        {
            string natural = (rel.GetTag("natural") ?? "").ToLower();
            string waterway = (rel.GetTag("waterway") ?? "").ToLower();
            string water = (rel.GetTag("water") ?? "").ToLower();
            string landuse = (rel.GetTag("landuse") ?? "").ToLower();
            string relType = (rel.GetTag("type") ?? "").ToLower();

            // type=waterway relations are always water
            if (relType == "waterway") return true;

            return natural == "water" || natural == "bay" || natural == "wetland"
                || natural == "coastline" || natural == "beach"
                || waterway.Length > 0  // Any waterway tag = water
                || water.Length > 0
                || landuse == "reservoir" || landuse == "basin";
        }

        /// <summary>
        /// Chain disconnected ways into connected rings by matching end-node IDs.
        /// </summary>
        private List<List<long>> ChainWays(List<OsmWay> ways)
        {
            List<List<long>> rings = new List<List<long>>();
            List<List<long>> segments = new List<List<long>>();

            // Copy each way's node list as a segment
            foreach (var w in ways)
            {
                if (w.NodeIds.Count < 2) continue;
                segments.Add(new List<long>(w.NodeIds));
            }

            while (segments.Count > 0)
            {
                List<long> chain = segments[0];
                segments.RemoveAt(0);

                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = segments.Count - 1; i >= 0; i--)
                    {
                        var seg = segments[i];
                        long chainEnd = chain[chain.Count - 1];
                        long chainStart = chain[0];

                        if (seg[0] == chainEnd)
                        {
                            // Append seg (skip first node, it's the same)
                            for (int j = 1; j < seg.Count; j++)
                                chain.Add(seg[j]);
                            segments.RemoveAt(i);
                            changed = true;
                        }
                        else if (seg[seg.Count - 1] == chainEnd)
                        {
                            // Append reversed seg
                            for (int j = seg.Count - 2; j >= 0; j--)
                                chain.Add(seg[j]);
                            segments.RemoveAt(i);
                            changed = true;
                        }
                        else if (seg[seg.Count - 1] == chainStart)
                        {
                            // Prepend seg
                            for (int j = seg.Count - 2; j >= 0; j--)
                                chain.Insert(0, seg[j]);
                            segments.RemoveAt(i);
                            changed = true;
                        }
                        else if (seg[0] == chainStart)
                        {
                            // Prepend reversed seg
                            for (int j = 1; j < seg.Count; j++)
                                chain.Insert(0, seg[j]);
                            segments.RemoveAt(i);
                            changed = true;
                        }
                    }
                }

                rings.Add(chain);
            }

            return rings;
        }
    }
}
