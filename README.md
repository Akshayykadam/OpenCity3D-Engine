# GeoCity3D — OpenStreetMap 3D City Generator

**GeoCity3D** generates real-world 3D cities in Unity from OpenStreetMap data with a single click. Clean architectural maquette style with solid, volumetric geometry — ready for urban planning, visualization, and game prototyping.

<div align="center">
  <img width="220" src="https://github.com/user-attachments/assets/1ebe3168-d560-4892-8e63-dadaf897e22d" />
  <img width="220" src="https://github.com/user-attachments/assets/acdecc60-fd46-4627-ae5f-e8afdfe3309d" />
  <img width="220" src="https://github.com/user-attachments/assets/edc71f8a-4d80-4560-9873-1476dcfe7892" />
  <img width="220" src="https://github.com/user-attachments/assets/1fc4bd82-f747-48b3-879f-ea2ecf78d43c" />
  <img width="220" src="https://github.com/user-attachments/assets/dae84ba0-9922-4b93-bc98-5ee7b4e6ecc8" />
  <img width="220" src="https://github.com/user-attachments/assets/5cb585d2-7b81-4a98-865c-8658ccc01769" />
</div> 

## Features

### Solid Geometry
- **Volumetric buildings** — watertight sealed extrusions with roof caps, bottom caps, and proper normals
- **Thick roads** — top surface + side walls + end caps. No paper-thin strips
- **Elevated bridges** — detects OSM `bridge` tags, creates raised decks with railings and support pillars
- **Rivers & waterways** — linear waterways (rivers, streams, canals) rendered as wide water strips
- **Area features** — parks, water bodies, and forests with visible edge thickness

### Environment
- **Solid trees** — grounded trunks with base disc + smooth sphere canopies, scattered in parks and along streets
- **Parks & green spaces** — detected from OSM landuse/leisure tags
- **Water bodies** — lakes, reservoirs, bays, riverbanks with distinct materials

### Architectural Maquette Style
- **Solid color materials** — clean, professional look with no textures
- **Double-sided rendering** — geometry never appears see-through
- **Shadow casting** — all elements cast and receive shadows for depth
- **Color palette** — light gray buildings, dark charcoal roads, vibrant green parks, dark teal water

### Real-World Data
- **One-click generation** — enter coordinates, set radius, click Generate
- **OpenStreetMap** — real building footprints, road networks, and land use via Overpass API
- **Smart height estimation** — uses `building:levels`, `height` tags, or estimates from building type
- **Road width by type** — motorways (12m), primary (10m), residential (6m), footways (2m)
- **Raised platform base** — city sits on a proportional pedestal like architectural models

### Technical
- **Floating-point precision** — built-in `OriginShifter` for large geospatial coordinates
- **Render pipeline agnostic** — auto-detects URP, HDRP, or Built-in shaders
- **No mesh leaks** — uses `sharedMesh` throughout for edit-mode safety
- **MeshColliders** — accurate collision on buildings

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
