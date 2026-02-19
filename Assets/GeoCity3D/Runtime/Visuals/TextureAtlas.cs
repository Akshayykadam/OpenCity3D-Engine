using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GeoCity3D.Visuals
{
    /// <summary>
    /// Generates shared texture atlases for buildings.
    /// 16 facade variations with different colors, 16 roof variations.
    /// Uses procedural plaster facades for realistic Indian building appearance.
    /// </summary>
    public class TextureAtlas
    {
        public Texture2D WallAtlas { get; private set; }
        public Texture2D RoofAtlas { get; private set; }

        public const int GRID_SIZE = 4;
        public const int WALL_TILE_SIZE = 256;  // Good detail per tile
        public const int ROOF_TILE_SIZE = 128;

        public int WallAtlasSize => GRID_SIZE * WALL_TILE_SIZE; // 1024
        public int RoofAtlasSize => GRID_SIZE * ROOF_TILE_SIZE; // 512

        public void Build()
        {
            BuildWallAtlas();
            BuildRoofAtlas();
        }

        private void BuildWallAtlas()
        {
            int size = WallAtlasSize;
            WallAtlas = new Texture2D(size, size);

            for (int row = 0; row < GRID_SIZE; row++)
            {
                for (int col = 0; col < GRID_SIZE; col++)
                {
                    Color wallColor = TextureGenerator.GetRandomWallColor();
                    Texture2D tile = TextureGenerator.CreateFacadeTexture(WALL_TILE_SIZE, WALL_TILE_SIZE, wallColor);

                    CopyTileToAtlas(tile, WallAtlas, col, row, WALL_TILE_SIZE);
                    Object.DestroyImmediate(tile);
                }
            }

            WallAtlas.Apply();
            WallAtlas.wrapMode = TextureWrapMode.Repeat;
            WallAtlas.filterMode = FilterMode.Bilinear;
        }

        private void BuildRoofAtlas()
        {
            int size = RoofAtlasSize;
            RoofAtlas = new Texture2D(size, size);

            for (int row = 0; row < GRID_SIZE; row++)
            {
                for (int col = 0; col < GRID_SIZE; col++)
                {
                    Color roofColor = TextureGenerator.GetRandomRoofColor();
                    Texture2D tile = TextureGenerator.CreateRoofTexture(ROOF_TILE_SIZE, ROOF_TILE_SIZE, roofColor);

                    CopyTileToAtlas(tile, RoofAtlas, col, row, ROOF_TILE_SIZE);
                    Object.DestroyImmediate(tile);
                }
            }

            RoofAtlas.Apply();
            RoofAtlas.wrapMode = TextureWrapMode.Clamp;
            RoofAtlas.filterMode = FilterMode.Bilinear;
        }

        private void CopyTileToAtlas(Texture2D tile, Texture2D atlas, int col, int row, int tileSize)
        {
            Color[] tilePixels = tile.GetPixels();
            int offsetX = col * tileSize;
            int offsetY = row * tileSize;

            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    atlas.SetPixel(offsetX + x, offsetY + y, tilePixels[y * tileSize + x]);
                }
            }
        }

        public void GetRandomWallTile(out Vector2 uvOffset, out Vector2 uvScale)
        {
            int col = Random.Range(0, GRID_SIZE);
            int row = Random.Range(0, GRID_SIZE);
            float tileSize = 1f / GRID_SIZE;
            uvOffset = new Vector2(col * tileSize, row * tileSize);
            uvScale = new Vector2(tileSize, tileSize);
        }

        public void GetRandomRoofTile(out Vector2 uvOffset, out Vector2 uvScale)
        {
            int col = Random.Range(0, GRID_SIZE);
            int row = Random.Range(0, GRID_SIZE);
            float tileSize = 1f / GRID_SIZE;
            uvOffset = new Vector2(col * tileSize, row * tileSize);
            uvScale = new Vector2(tileSize, tileSize);
        }
    }
}
