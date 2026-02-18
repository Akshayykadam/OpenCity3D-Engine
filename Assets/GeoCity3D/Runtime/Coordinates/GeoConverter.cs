using System;
using UnityEngine;

namespace GeoCity3D.Coordinates
{
    public static class GeoConverter
    {
        private const double EarthRadius = 6378137;
        private const double OriginShift = 2 * Math.PI * EarthRadius / 2.0;

        /// <summary>
        /// Converts WGS84 coordinates (Lat, Lon) to Spherical Mercator (x, y) coordinates in meters.
        /// </summary>
        public static Vector2d LatLonToMeters(double lat, double lon)
        {
            double mx = lon * OriginShift / 180.0;
            double my = Math.Log(Math.Tan((90 + lat) * Math.PI / 360.0)) / (Math.PI / 180.0);

            my = my * OriginShift / 180.0;
            return new Vector2d(mx, my);
        }

         /// <summary>
        /// Converts Spherical Mercator (x, y) coordinates in meters to WGS84 coordinates (Lat, Lon).
        /// </summary>
        public static Vector2d MetersToLatLon(double mx, double my)
        {
            double lon = (mx / OriginShift) * 180.0;
            double lat = (my / OriginShift) * 180.0;

            lat = 180 / Math.PI * (2 * Math.Atan(Math.Exp(lat * Math.PI / 180.0)) - Math.PI / 2.0);
            return new Vector2d(lon, lat); // Note: Order is Lon, Lat
        }
    }

    // Helper struct for double precision vector
    public struct Vector2d
    {
        public static readonly Vector2d zero = new Vector2d(0, 0);

        public double x;
        public double y;

        public Vector2d(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector3 ToVector3()
        {
            return new Vector3((float)x, 0, (float)y);
        }
        
        public static Vector2d operator -(Vector2d a, Vector2d b)
        {
            return new Vector2d(a.x - b.x, a.y - b.y);
        }
    }
}
