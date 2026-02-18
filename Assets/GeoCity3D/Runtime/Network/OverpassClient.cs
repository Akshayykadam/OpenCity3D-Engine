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
                webRequest.timeout = 30; // Set timeout to 30 seconds
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    onError?.Invoke(webRequest.error);
                }
                else
                {
                    onSuccess?.Invoke(webRequest.downloadHandler.text);
                }
            }
        }

        private string BuildQuery(double lat, double lon, double radius)
        {
            // Convert radius to degrees approx (1 degree lat ~= 111km)
            // But Overpass takes bounding box (south, west, north, east) or around(radius, lat, lon)
            // Let's use around for simplicity: (around:radius, lat, lon)
            
            // Add timeout of 25 seconds to the query itself
            return $"[out:xml][timeout:25];(node(around:{radius},{lat},{lon});way(around:{radius},{lat},{lon});relation(around:{radius},{lat},{lon}););out body;>;out skel qt;";
        }
    }
}
