using UnityEngine;

public class ShapeData : MonoBehaviour
{
    [Header("Сложность (от 1 до 20)")]
    [Range(1, 20)] public int complexityWeight = 1;
}