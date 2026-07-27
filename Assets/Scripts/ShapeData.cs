using UnityEngine;

// Оставляем наш enum тут, чтобы он был доступен везде
public enum SymmetryType
{
    None,
    Axial,
    Cubic
}

public class ShapeData : MonoBehaviour
{
    [Header("Настройки фигуры")]
    public SymmetryType symmetry = SymmetryType.None;
    
    [Header("Сложность (от 1 до 20)")]
    [Range(1, 20)] public int complexityWeight = 1;
}