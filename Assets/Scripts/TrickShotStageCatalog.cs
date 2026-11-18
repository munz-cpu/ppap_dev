using System;
using UnityEngine;

[CreateAssetMenu(menuName = "PPAP/Trick Shot Stage Catalog")]
public sealed class TrickShotStageCatalog : ScriptableObject
{
    [Serializable]
    public struct Wall
    {
        public Vector2 center;
        public Vector2 size;
        public bool breakable;
    }

    [Serializable]
    public struct Shot
    {
        public Vector2 penTip;
        public float penAngle;
        public Wall[] walls;
    }

    [Serializable]
    public struct Stage
    {
        public Vector2 fruitSpawn;
        public Shot apple;
        public Shot pineapple;
    }

    [SerializeField] private Stage[] stages;

    public int Count => stages?.Length ?? 0;
    public Stage this[int index] => stages[index];
}
