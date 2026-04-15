using System.Numerics;

namespace Strategy;

internal sealed record Province(
    int Id,
    string CountyName,
    string BaronyName,
    int StartTileX,
    int StartTileY,
    int WidthInTiles,
    int HeightInTiles,
    int TerrainType,
    int OwnerCharacterId)
{
    public int EndTileX => StartTileX + WidthInTiles - 1;
    public int EndTileY => StartTileY + HeightInTiles - 1;

    public bool ContainsTile(int tileX, int tileY)
    {
        return tileX >= StartTileX &&
               tileX <= EndTileX &&
               tileY >= StartTileY &&
               tileY <= EndTileY;
    }

    public Vector2 GetCenterInPixels(int tileSize)
    {
        return new Vector2(
            (StartTileX + WidthInTiles * 0.5f) * tileSize,
            (StartTileY + HeightInTiles * 0.5f) * tileSize);
    }
}
