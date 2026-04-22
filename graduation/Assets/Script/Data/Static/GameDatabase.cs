using System.Collections.Generic;
using UnityEngine;

public class GameDatabase : MonoBehaviour
{
    #region Static Data Lists
    [Header("Player")]
    public PlayerBaseData playerBaseData;

    [Header("Parts")]
    public List<PartData> parts = new List<PartData>();

    [Header("Monsters")]
    public List<MonsterData> monsters = new List<MonsterData>();

    [Header("Drop Tables")]
    public List<DropTableData> dropTables = new List<DropTableData>();

    [Header("Map Pieces")]
    public List<MapPieceData> mapPieces = new List<MapPieceData>();

    [Header("Buffs")]
    public List<BuffData> buffs = new List<BuffData>();
    #endregion

    #region Lookup Dictionaries
    private readonly Dictionary<int, PartData> partDict = new Dictionary<int, PartData>();
    private readonly Dictionary<int, MonsterData> monsterDict = new Dictionary<int, MonsterData>();
    private readonly Dictionary<int, DropTableData> dropTableDict = new Dictionary<int, DropTableData>();
    private readonly Dictionary<int, MapPieceData> mapPieceDict = new Dictionary<int, MapPieceData>();
    private readonly Dictionary<int, BuffData> buffDict = new Dictionary<int, BuffData>();
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        BuildLookup();
    }
    #endregion

    #region Build Lookup
    /// <summary>
    /// 建立所有資料表的 ID 查找字典。
    /// </summary>
    private void BuildLookup()
    {
        partDict.Clear();
        monsterDict.Clear();
        dropTableDict.Clear();
        mapPieceDict.Clear();
        buffDict.Clear();

        for (int i = 0; i < parts.Count; i++)
        {
            PartData data = parts[i];
            if (data == null) continue;

            if (partDict.ContainsKey(data.partID))
            {
                Debug.LogWarning($"Duplicate Part ID: {data.partID}");
                continue;
            }

            partDict.Add(data.partID, data);
        }

        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterData data = monsters[i];
            if (data == null) continue;

            if (monsterDict.ContainsKey(data.monsterID))
            {
                Debug.LogWarning($"Duplicate Monster ID: {data.monsterID}");
                continue;
            }

            monsterDict.Add(data.monsterID, data);
        }

        for (int i = 0; i < dropTables.Count; i++)
        {
            DropTableData data = dropTables[i];
            if (data == null) continue;

            if (dropTableDict.ContainsKey(data.dropTableID))
            {
                Debug.LogWarning($"Duplicate DropTable ID: {data.dropTableID}");
                continue;
            }

            dropTableDict.Add(data.dropTableID, data);
        }

        for (int i = 0; i < mapPieces.Count; i++)
        {
            MapPieceData data = mapPieces[i];
            if (data == null) continue;

            if (mapPieceDict.ContainsKey(data.mapPieceID))
            {
                Debug.LogWarning($"Duplicate MapPiece ID: {data.mapPieceID}");
                continue;
            }

            mapPieceDict.Add(data.mapPieceID, data);
        }

        for (int i = 0; i < buffs.Count; i++)
        {
            BuffData data = buffs[i];
            if (data == null) continue;

            if (buffDict.ContainsKey(data.buffID))
            {
                Debug.LogWarning($"Duplicate Buff ID: {data.buffID}");
                continue;
            }

            buffDict.Add(data.buffID, data);
        }
    }
    #endregion

    #region Get Methods
    /// <summary>
    /// 依據 partID 取得 PartData。
    /// </summary>
    public PartData GetPartData(int partID)
    {
        partDict.TryGetValue(partID, out PartData data);
        return data;
    }

    /// <summary>
    /// 依據 monsterID 取得 MonsterData。
    /// </summary>
    public MonsterData GetMonsterData(int monsterID)
    {
        monsterDict.TryGetValue(monsterID, out MonsterData data);
        return data;
    }

    /// <summary>
    /// 依據 dropTableID 取得 DropTableData。
    /// </summary>
    public DropTableData GetDropTableData(int dropTableID)
    {
        dropTableDict.TryGetValue(dropTableID, out DropTableData data);
        return data;
    }

    /// <summary>
    /// 依據 mapPieceID 取得 MapPieceData。
    /// </summary>
    public MapPieceData GetMapPieceData(int mapPieceID)
    {
        mapPieceDict.TryGetValue(mapPieceID, out MapPieceData data);
        return data;
    }

    /// <summary>
    /// 依據 buffID 取得 BuffData。
    /// </summary>
    public BuffData GetBuffData(int buffID)
    {
        buffDict.TryGetValue(buffID, out BuffData data);
        return data;
    }
    #endregion
}