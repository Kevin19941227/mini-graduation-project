using UnityEngine;

[CreateAssetMenu(fileName = "MapPieceData", menuName = "GameData/Map Piece Data")]
public class MapPieceData : ScriptableObject
{
    [Header("Identity")]
    public int mapPieceID;
    public string mapPieceName;

    [Header("Presentation")]
    public GameObject mapPrefab;
} 