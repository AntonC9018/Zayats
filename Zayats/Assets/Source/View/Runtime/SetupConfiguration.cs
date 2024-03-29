using System;
using Kari.Plugins.AdvancedEnum;
using UnityEngine;
using Zayats.Core.Generated;
using Zayats.Unity.View.Generated;

namespace Zayats.Unity.View
{
    [GenerateArrayWrapper("AnimationArray")]
    public enum AnimationKind
    {
        UI,
        Game,
        InitialThingSpawning,
    }

    [Serializable]
    public struct VisualConfiguration
    {
        // [Range(0.0f, 2.0f)]
        public AnimationArray<float> AnimationSpeed;

        [Range(0.0f, 2.0f)]
        public float MovementJumpPower;

        [Range(0.0f, 2.0f)]
        public float ToastTimeout;

        public ItemUsabilityArray<Color> ItemUsabilityColors;
        public float HighlightEmissionIntensity;
    }

    [Serializable]
    public struct GameConfiguration
    {
        public GameObject CellPrefab;
        public ThingArray<GameObject> PrefabsToSpawn;
        public ThingArray<int> CountsToSpawn;
        public ThingArray<int> ItemCosts;
        public Color[] PlayerCharacterColors;
        public int[] ShopPositions;
    }

    [Serializable]
    public class SetupConfiguration : ScriptableObject
    {
        public VisualConfiguration Visual;
        public GameConfiguration Game;
    }
}