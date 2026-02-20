using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoCity3D.Network
{
    public class OverpassClient
    {
        private static readonly string[] OverpassApiUrls = new string[]
        {
            "https://overpass-api.de/api/interpreter",
            "https://maps.mail.ru/osm/tools/overpass/api/interpreter", // Fallback 1
            "https://overpass.kumi.systems/api/interpreter"             // Fallback 2
        };

        public IEnumerator GetMapData(double lat, double lon, double radius, Action<string> onSuccess, Action<string> onError)
        {
            string query = BuildQuery(lat, lon, radius);
            string focusedQuery = BuildFocusedQuery(lat, lon, radius);
            bool success = false;
            string lastError = "";

            // Try the full query across fallback servers first
            foreach (string baseUrl in OverpassApiUrls)
            {
                string url = $"{baseUrl}?data={UnityWebRequest.EscapeURL(query)}";
                using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
                {
                    webRequest.timeout = 60;
                    yield return webRequest.SendWebRequest();

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        onSuccess?.Invoke(webRequest.downloadHandler.text);
                        success = true;
                        break;
                    }
                    else
                    {
                        lastError = webRequest.error;
                        Debug.LogWarning($"Primary Query failed on {baseUrl}: {webRequest.error}");
                    }
                }
            }

            // If full query failed on ALL servers, try focused query on ALL servers
            if (!success)
            {
                Debug.LogWarning("⚠️ OVERPASS API LIMIT HIT ⚠️\nThe Overpass servers are extremely busy or rate-limiting your connection. Falling back to a lightweight query (Buildings and Highways ONLY). Parks, water, beaches, etc., will be temporarily missing from the generation!\nWait 2-3 minutes before trying again for a complete map.");
                
                foreach (string baseUrl in OverpassApiUrls)
                {
                    string retryUrl = $"{baseUrl}?data={UnityWebRequest.EscapeURL(focusedQuery)}";
                    using (UnityWebRequest retry = UnityWebRequest.Get(retryUrl))
                    {
                        retry.timeout = 60;
                        yield return retry.SendWebRequest();

                        if (retry.result == UnityWebRequest.Result.Success)
                        {
                            onSuccess?.Invoke(retry.downloadHandler.text);
                            success = true;
                            break;
                        }
                        else
                        {
                            lastError = retry.error;
                            Debug.LogWarning($"Focused Query failed on {baseUrl}: {retry.error}");
                        }
                    }
                }
            }

            if (!success)
            {
                onError?.Invoke($"All Overpass servers failed. Last error: {lastError}");
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
