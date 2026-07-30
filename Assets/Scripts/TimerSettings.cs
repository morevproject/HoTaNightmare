using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewTimerSettings", menuName = "Game/Timer Settings")]
public class TimerSettings : ScriptableObject
{
    [Serializable]
    public struct TimerThreshold
    {
        public int minLevel; // Начиная с какого уровня работает это время
        public float timeInSeconds; // Сколько секунд даем
    }

    public List<TimerThreshold> thresholds = new List<TimerThreshold>();

    // Метод, который сам находит нужное время для текущего уровня
    public float GetTimeForLevel(int level)
    {
        float time = 60f; // Время по умолчанию на всякий случай
        int highestLevelMatch = -1;

        foreach (var threshold in thresholds)
        {
            // Ищем наивысший порог, который меньше или равен текущему уровню
            if (level >= threshold.minLevel && threshold.minLevel > highestLevelMatch)
            {
                highestLevelMatch = threshold.minLevel;
                time = threshold.timeInSeconds;
            }
        }
        return time;
    }
}