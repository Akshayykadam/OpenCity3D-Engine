# GeoCity3D — OpenStreetMap 3D City Generator

**GeoCity3D** generates real-world 3D cities in Unity from OpenStreetMap data with a single click. Clean architectural maquette style with solid, volumetric geometry — ready for urban planning, visualization, and game prototyping.

<div style="display: flex; justify-content: center; gap: 8px;">
  <img width="240" alt="Screenshot 1" src="https://github.com/user-attachments/assets/dcdbee57-a24a-450f-a098-2544b5df7b66" />
  <img width="240" alt="Screenshot 2" src="https://github.com/user-attachments/assets/1b24a4ee-313e-4133-a616-7110c8946b69" />
  <img width="240" alt="Screenshot 3" src="https://github.com/user-attachments/assets/38f67f7c-5f3f-4642-a326-68693a212d5b" />
  <img width="240" alt="Screenshot 4" src="https://github.com/user-attachments/assets/09e3cc74-edeb-43d0-8d28-13c85030dcf8" />
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
