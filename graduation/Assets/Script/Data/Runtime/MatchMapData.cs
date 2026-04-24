using UnityEngine;

[System.Serializable]
public class MatchMapData
{
    public int width = 3;
    public int height = 3;
    public int[] cellMapIDs = new int[9];

    /// <summary>
    /// 初始化地圖尺寸。
    /// </summary>
    public void Initialize(int newWidth, int newHeight)
    {
        width = newWidth;
        height = newHeight;
        cellMapIDs = new int[width * height];
    }

    /// <summary>
    /// 取得指定格子的陣列索引。
    /// </summary>
    public int GetIndex(int x, int y)
    {
        return y * width + x;
    }

    /// <summary>
    /// 取得指定格子的地圖 ID。
    /// </summary>
    public int GetMapID(int x, int y)
    {
        return cellMapIDs[GetIndex(x, y)];
    }

    /// <summary>
    /// 設定指定格子的地圖 ID。
    /// </summary>
    public void SetMapID(int x, int y, int mapID)
    {
        cellMapIDs[GetIndex(x, y)] = mapID;
    }
}