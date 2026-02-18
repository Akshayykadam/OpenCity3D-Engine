# GeoCity3D - OpenStreetMap Procedural City Generator

**GeoCity3D** allows you to generate real-world 3D cities in Unity directly from OpenStreetMap data with a single click. No external tools or complex parsing required.

## Features

- **One-Click Generation**: Enter coordinates, set radius, and click Generate.
- **Real-World Data**: Uses OpenStreetMap (OSM) via the Overpass API.
- **Procedural Geometry**: 
  - Buildings with correct footprints and heights (from tags or estimates).
  - Roads with actual widths based on highway types.
- **Extensible**: Uses `CityController` to manage materials and settings.
- **Floating Point Correction**: Built-in `OriginShifter` to handle large geospatial coordinates.

## Installation

1. Import the **GeoCity3D** package into your Unity project.
2. Ensure you have an internet connection (required for fetching map data).

## Quick Start

1. Open the **Demo Scene** or create a new scene.
2. If starting fresh, go to **GeoCity3D > Setup Demo Scene** to initialize the necessary components (`CityController` and default materials).
3. Open the generator window via **GeoCity3D > City Generator**.
4. Enter the **Latitude** and **Longitude** of your desired location.
   - *Example (Eiffel Tower)*: `48.8584`, `2.2945`
5. Set the **Radius** (e.g., `500` meters).
6. Click **Generate City**.

## Troubleshooting

- **"Download failed"**: Check your internet connection. the Overpass API might be temporarily unavailable or rate-limiting your IP.
- **Empty Scene**: The location you selected might not have building/road data in OSM. Try a known city center.
- **Jittery Movement**: Ensure the `OriginShifter` component is present in the scene. It centers the world to avoid floating-point errors.
