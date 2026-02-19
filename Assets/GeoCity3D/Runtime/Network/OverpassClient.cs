using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoCity3D.Network
{
    public class OverpassClient
    {
        private const string OverpassApiUrl = "https://overpass-api.de/api/interpreter";

        public IEnumerator GetMapData(double lat, double lon, double radius, Action<string> onSuccess, Action<string> onError)
        {
            string query = BuildQuery(lat, lon, radius);
            string url = $"{OverpassApiUrl}?data={UnityWebRequest.EscapeURL(query)}";

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                webRequest.timeout = 60; // Increased timeout for larger areas
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    // Retry once with a smaller, focused query
                    Debug.Log("First attempt failed, retrying with focused query...");
                    string retryQuery = BuildFocusedQuery(lat, lon, radius);
                    string retryUrl = $"{OverpassApiUrl}?data={UnityWebRequest.EscapeURL(retryQuery)}";

                    using (UnityWebRequest retry = UnityWebRequest.Get(retryUrl))
                    {
                        retry.timeout = 90;
                        yield return retry.SendWebRequest();

                        if (retry.result == UnityWebRequest.Result.ConnectionError || retry.result == UnityWebRequest.Result.ProtocolError)
                        {
                            onError?.Invoke(retry.error);
                        }
                        else
                        {
                            onSuccess?.Invoke(retry.downloadHandler.text);
                        }
                    }
                }
                else
                {
                    onSuccess?.Invoke(webRequest.downloadHandler.text);
                }
            }
        }

        /// <summary>
        /// Targeted query — fetches buildings, roads, parks, water areas, waterways, and water relations.
        /// </summary>
        private string BuildQuery(double lat, double lon, double radius)
        {
            return $"[out:xml][timeout:55];" +
                   $"(" +
                   // Buildings & roads
                   $"way[\"building\"](around:{radius},{lat},{lon});" +
                   $"way[\"highway\"](around:{radius},{lat},{lon});" +
                   // Parks & green areas
                   $"way[\"landuse\"~\"park|grass|forest|meadow|reservoir|basin\"](around:{radius},{lat},{lon});" +
                   $"way[\"leisure\"~\"park|garden\"](around:{radius},{lat},{lon});" +
                   // Water areas (closed polygons)
                   $"way[\"natural\"~\"water|bay|wetland|coastline|beach\"](around:{radius},{lat},{lon});" +
                   $"way[\"waterway\"~\"riverbank|dock|boatyard\"](around:{radius},{lat},{lon});" +
                   $"way[\"water\"](around:{radius},{lat},{lon});" +
                   $"way[\"landuse\"~\"reservoir|basin\"](around:{radius},{lat},{lon});" +
                   // Linear waterways (rivers, streams, canals)
                   $"way[\"waterway\"~\"river|stream|canal|drain|ditch\"](around:{radius},{lat},{lon});" +
                   // Beach/sand areas
                   $"way[\"natural\"~\"beach|sand\"](around:{radius},{lat},{lon});" +
                   // Water relations (multipolygon lakes, seas, wide rivers)
                   $"relation[\"natural\"=\"water\"](around:{radius},{lat},{lon});" +
                   $"relation[\"natural\"~\"bay|wetland\"](around:{radius},{lat},{lon});" +
                   $"relation[\"waterway\"](around:{radius},{lat},{lon});" +
                   $"relation[\"water\"](around:{radius},{lat},{lon});" +
                   $"relation[\"landuse\"~\"reservoir|basin\"](around:{radius},{lat},{lon});" +
                   $"relation[\"type\"=\"waterway\"](around:{radius},{lat},{lon});" +
                   $");" +
                   $"out body;>;out skel qt;";
        }


        /// <summary>
        /// Fallback: even more focused query (buildings + roads only) if the full query times out.
        /// </summary>
        private string BuildFocusedQuery(double lat, double lon, double radius)
        {
            return $"[out:xml][timeout:90];" +
                   $"(" +
                   $"way[\"building\"](around:{radius},{lat},{lon});" +
                   $"way[\"highway\"](around:{radius},{lat},{lon});" +
                   $");" +
                   $"out body;>;out skel qt;";
        }
    }
}
