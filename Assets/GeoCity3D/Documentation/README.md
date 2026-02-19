# GeoCity3D — OpenStreetMap 3D City Generator

**GeoCity3D** generates real-world 3D cities in Unity from OpenStreetMap data with a single click. Clean architectural maquette style with solid, volumetric geometry — ready for urban planning, visualization, and game prototyping.

## Features

### 🏗️ Solid Geometry
- **Volumetric buildings** — watertight sealed extrusions with roof caps, bottom caps, and proper normals
- **Thick roads** — top surface + side walls + end caps. No paper-thin strips
- **Elevated bridges** — detects OSM `bridge` tags, creates raised decks with railings and support pillars
- **Rivers & waterways** — linear waterways (rivers, streams, canals) rendered as wide water strips
- **Area features** — parks, water bodies, and forests with visible edge thickness

### 🌳 Environment
- **Solid trees** — grounded trunks with base disc + smooth sphere canopies, scattered in parks and along streets
- **Parks & green spaces** — detected from OSM landuse/leisure tags
- **Water bodies** — lakes, reservoirs, bays, riverbanks with distinct materials

### 🎨 Architectural Maquette Style
- **Solid color materials** — clean, professional look with no textures
- **Double-sided rendering** — geometry never appears see-through
- **Shadow casting** — all elements cast and receive shadows for depth
- **Color palette** — light gray buildings, dark charcoal roads, vibrant green parks, dark teal water

### 🗺️ Real-World Data
- **One-click generation** — enter coordinates, set radius, click Generate
- **OpenStreetMap** — real building footprints, road networks, and land use via Overpass API
- **Smart height estimation** — uses `building:levels`, `height` tags, or estimates from building type
- **Road width by type** — motorways (12m), primary (10m), residential (6m), footways (2m)
- **Raised platform base** — city sits on a proportional pedestal like architectural models

### ⚙️ Technical
- **Floating-point precision** — built-in `OriginShifter` for large geospatial coordinates
- **Render pipeline agnostic** — auto-detects URP, HDRP, or Built-in shaders
- **No mesh leaks** — uses `sharedMesh` throughout for edit-mode safety
- **MeshColliders** — accurate collision on buildings

## Installation

1. Import the **GeoCity3D** package into your Unity project.
2. Ensure you have an internet connection (required for fetching map data).

## Quick Start

1. Open the **Demo Scene** or create a new scene.
2. If starting fresh, go to **GeoCity3D > Setup Demo Scene** to initialize default materials.
3. Open the generator via **GeoCity3D > City Generator**.
4. Enter **Latitude** and **Longitude** of your desired location.
   - *Example (Eiffel Tower)*: `48.8584`, `2.2945`
5. Set the **Radius** (e.g., `500` meters).
6. Click **Generate City**.

## Troubleshooting

- **"Download failed"**: Check your internet connection. The Overpass API might be temporarily unavailable or rate-limiting.
- **Empty scene**: The location might not have building/road data in OSM. Try a known city center.
- **Jittery movement**: Ensure the `OriginShifter` component is present. It centers the world to avoid floating-point errors.
