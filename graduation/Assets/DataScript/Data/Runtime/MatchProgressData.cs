using UnityEngine;

[System.Serializable]
public class MatchProgressData
{
    public int currentStageIndex = 0;
    public bool bossDefeated = false;
    public bool isPvpMode = false;
    public int alivePlayerCount = 4;
    public float elapsedTime = 0f;
    public int matchSeed = 0;
}