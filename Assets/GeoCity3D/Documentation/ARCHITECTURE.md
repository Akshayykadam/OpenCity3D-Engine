# GeoCity3D Architecture

This document provides a high-level overview of the GeoCity3D codebase for developers and contributors.

## System Overview

GeoCity3D follows a linear pipeline:
1. **Fetch**: Download raw XML data from Overpass API.
2. **Parse**: Convert XML into internal C# data models (`OsmData`).
3. **Convert**: Transform WGS84 (Lat/Lon) coordinates into Unity World Space (Meters).
4. **Generate**: Construct Unity GameObjects (Meshes) from the parsed data.

## Directory Structure

- **Runtime/**
  - **Coordinates/**: `GeoConverter` (Math) and `OriginShifter` (Scene Component).
  - **Data/**: `OsmNode`, `OsmWay`, `OsmData` structures.
  - **Geometry/**: `BuildingBuilder`, `RoadBuilder`, `GeometryUtils` (Triangulation).
  - **Network/**: `OverpassClient` for HTTP requests.
  - **Parsing/**: `OsmXmlParser`.
  - `CityController.cs`: Monobehaviour for configuration.
- **Editor/**
  - `CityGeneratorWindow.cs`: EditorWindow UI.
  - `DemoSetup.cs`: Menu item helper.

## Key Components

### Coordinate System (`GeoCity3D.Coordinates`)
- **GeoConverter**: Converts Lat/Lon to Spherical Mercator meters.
- **OriginShifter**:  Unity uses `float` (32-bit), which lacks precision for global coordinates. We use a "Floating Origin" approach:
  - The first generated point becomes `(0,0,0)` in Unity.
  - All subsequent points are relative to this origin.

### Geometry (`GeoCity3D.Geometry`)
- **BuildingBuilder**: 
  - Reads `building:levels` or `height` tags. 
  - Uses ear-clipping triangulation for the roof.
  - Generates walls by extruding the footprint.
- **RoadBuilder**:
  - Generates a mesh strip along the way nodes.
  - Expands line segments by `width` perpendicular to the direction.

### Data Flow
1. User clicks "Generate" in `CityGeneratorWindow`.
2. `OverpassClient` fetches XML string.
3. `OsmXmlParser` creates `OsmData` object.
4. Loop through `OsmData.Ways`:
   - If `building` tag exists -> Call `BuildingBuilder`.
   - If `highway` tag exists -> Call `RoadBuilder`.
5. Instantiated GameObjects are parented to a root object.
