using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameDatabase", menuName = "GameData/Game Database")]
public class GameDatabase : ScriptableObject
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
    private bool lookupBuilt;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        BuildLookup();
    }

    private void OnValidate()
    {
        BuildLookup();
    }
    #endregion

    #region Build Lookup
    private void BuildLookup()
    {
        lookupBuilt = true;
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
    public PartData GetPartData(int partID)
    {
        EnsureLookup();
        partDict.TryGetValue(partID, out PartData data);
        return data;
    }

    public MonsterData GetMonsterData(int monsterID)
    {
        EnsureLookup();
        monsterDict.TryGetValue(monsterID, out MonsterData data);
        return data;
    }

    public DropTableData GetDropTableData(int dropTableID)
    {
        EnsureLookup();
        dropTableDict.TryGetValue(dropTableID, out DropTableData data);
        return data;
    }

    public MapPieceData GetMapPieceData(int mapPieceID)
    {
        EnsureLookup();
        mapPieceDict.TryGetValue(mapPieceID, out MapPieceData data);
        return data;
    }

    public BuffData GetBuffData(int buffID)
    {
        EnsureLookup();
        buffDict.TryGetValue(buffID, out BuffData data);
        return data;
    }
    #endregion

    #region Validation
    [ContextMenu("Validate Database")]
    public void ValidateDatabase()
    {
        BuildLookup();

        ValidatePlayer();
        ValidateParts();
        ValidateBuffs();
        ValidateMonsters();
        ValidateDropTables();
        ValidateMapPieces();

        Debug.Log("[GameDatabase] Validation finished.");
    }

    private void ValidatePlayer()
    {
        if (playerBaseData == null)
        {
            Debug.LogWarning("[GameDatabase] PlayerBaseData is missing.");
            return;
        }

        if (playerBaseData.playerPrefab == null)
        {
            Debug.LogWarning("[GameDatabase] PlayerBaseData.playerPrefab is missing.");
        }
    }

    private void ValidateParts()
    {
        for (int i = 0; i < parts.Count; i++)
        {
            PartData part = parts[i];
            if (part == null)
            {
                Debug.LogWarning($"[GameDatabase] Parts[{i}] is missing.");
                continue;
            }

            if (part.partID <= 0)
            {
                Debug.LogWarning($"[GameDatabase] Part '{part.name}' has invalid partID: {part.partID}.");
            }

            if (string.IsNullOrWhiteSpace(part.partName))
            {
                Debug.LogWarning($"[GameDatabase] Part ID {part.partID} has an empty partName.");
            }

            if (part.partPrefab == null)
            {
                Debug.LogWarning($"[GameDatabase] Part '{part.partName}' is missing partPrefab.");
            }

            if (part.icon == null)
            {
                Debug.LogWarning($"[GameDatabase] Part '{part.partName}' is missing icon.");
            }

            if (part.maxStack < 1)
            {
                Debug.LogWarning($"[GameDatabase] Part '{part.partName}' has invalid maxStack: {part.maxStack}.");
            }

            ValidatePassiveBuffs(part);
        }
    }

    private void ValidatePassiveBuffs(PartData part)
    {
        if (part.passiveBuffs == null)
        {
            return;
        }

        for (int i = 0; i < part.passiveBuffs.Count; i++)
        {
            BuffData buff = part.passiveBuffs[i];
            if (buff == null)
            {
                Debug.LogWarning($"[GameDatabase] Part '{part.partName}' has an empty passive buff at index {i}.");
                continue;
            }

            if (!buffDict.ContainsKey(buff.buffID))
            {
                Debug.LogWarning($"[GameDatabase] Part '{part.partName}' references Buff ID {buff.buffID}, but it is not in the database.");
            }
        }
    }

    private void ValidateBuffs()
    {
        for (int i = 0; i < buffs.Count; i++)
        {
            BuffData buff = buffs[i];
            if (buff == null)
            {
                Debug.LogWarning($"[GameDatabase] Buffs[{i}] is missing.");
                continue;
            }

            if (buff.buffID <= 0)
            {
                Debug.LogWarning($"[GameDatabase] Buff '{buff.name}' has invalid buffID: {buff.buffID}.");
            }

            if (string.IsNullOrWhiteSpace(buff.buffName))
            {
                Debug.LogWarning($"[GameDatabase] Buff ID {buff.buffID} has an empty buffName.");
            }

            if (buff.duration <= 0f)
            {
                Debug.LogWarning($"[GameDatabase] Buff '{buff.buffName}' has invalid duration: {buff.duration}.");
            }

            if (buff.stackable && buff.maxStack < 2)
            {
                Debug.LogWarning($"[GameDatabase] Buff '{buff.buffName}' is stackable but maxStack is less than 2.");
            }
        }
    }

    private void ValidateMonsters()
    {
        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterData monster = monsters[i];
            if (monster == null)
            {
                Debug.LogWarning($"[GameDatabase] Monsters[{i}] is missing.");
                continue;
            }

            if (monster.monsterID <= 0)
            {
                Debug.LogWarning($"[GameDatabase] Monster '{monster.name}' has invalid monsterID: {monster.monsterID}.");
            }

            if (monster.monsterPrefab == null)
            {
                Debug.LogWarning($"[GameDatabase] Monster '{monster.monsterName}' is missing monsterPrefab.");
            }

            if (monster.dropTableID > 0 && !dropTableDict.ContainsKey(monster.dropTableID))
            {
                Debug.LogWarning($"[GameDatabase] Monster '{monster.monsterName}' references missing DropTable ID {monster.dropTableID}.");
            }
        }
    }

    private void ValidateDropTables()
    {
        for (int i = 0; i < dropTables.Count; i++)
        {
            DropTableData dropTable = dropTables[i];
            if (dropTable == null)
            {
                Debug.LogWarning($"[GameDatabase] DropTables[{i}] is missing.");
                continue;
            }

            if (dropTable.dropTableID <= 0)
            {
                Debug.LogWarning($"[GameDatabase] DropTable '{dropTable.name}' has invalid dropTableID: {dropTable.dropTableID}.");
            }

            for (int j = 0; j < dropTable.dropEntries.Count; j++)
            {
                DropEntry entry = dropTable.dropEntries[j];
                if (entry == null)
                {
                    Debug.LogWarning($"[GameDatabase] DropTable ID {dropTable.dropTableID} has an empty entry at index {j}.");
                    continue;
                }

                if (!partDict.ContainsKey(entry.partID))
                {
                    Debug.LogWarning($"[GameDatabase] DropTable ID {dropTable.dropTableID} references missing Part ID {entry.partID}.");
                }

                if (entry.minCount < 1 || entry.maxCount < entry.minCount)
                {
                    Debug.LogWarning($"[GameDatabase] DropTable ID {dropTable.dropTableID} has invalid count range at entry {j}.");
                }
            }
        }
    }

    private void ValidateMapPieces()
    {
        for (int i = 0; i < mapPieces.Count; i++)
        {
            MapPieceData mapPiece = mapPieces[i];
            if (mapPiece == null)
            {
                Debug.LogWarning($"[GameDatabase] MapPieces[{i}] is missing.");
                continue;
            }

            if (mapPiece.mapPieceID <= 0)
            {
                Debug.LogWarning($"[GameDatabase] MapPiece '{mapPiece.name}' has invalid mapPieceID: {mapPiece.mapPieceID}.");
            }

            if (mapPiece.mapPrefab == null)
            {
                Debug.LogWarning($"[GameDatabase] MapPiece '{mapPiece.mapPieceName}' is missing mapPrefab.");
            }
        }
    }
    #endregion

    private void EnsureLookup()
    {
        if (!lookupBuilt)
        {
            BuildLookup();
        }
    }
}
