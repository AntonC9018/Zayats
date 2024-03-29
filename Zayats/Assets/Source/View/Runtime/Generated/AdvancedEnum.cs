// <auto-generated>
// This file has been autogenerated by Kari.
// </auto-generated>

#pragma warning disable

namespace Zayats.Unity.View.Generated
{
    using System;
    [Serializable]
    public partial struct AnimationArray<T>
    {
        public /*readonly*/ T[] Values;
        private AnimationArray(T[] values) => Values = values;
        public static AnimationArray<T> Create() => new AnimationArray<T>(new T[3]);
        public readonly ref T GetRef(Zayats.Unity.View.AnimationKind key)
        {
            return ref Values[(int) key];
        }
        public readonly T Get(Zayats.Unity.View.AnimationKind key)
        {
            return Values[(int) key];
        }
        public readonly void Set(Zayats.Unity.View.AnimationKind key, T value)
        {
            Values[(int) key] = value;
        }
        public readonly ref T UIRef => ref Values[(int) Zayats.Unity.View.AnimationKind.UI];
        public T UI
        {
            readonly get => Values[(int) Zayats.Unity.View.AnimationKind.UI];
            set => Values[(int) Zayats.Unity.View.AnimationKind.UI] = value;
        }
        public readonly ref T GameRef => ref Values[(int) Zayats.Unity.View.AnimationKind.Game];
        public T Game
        {
            readonly get => Values[(int) Zayats.Unity.View.AnimationKind.Game];
            set => Values[(int) Zayats.Unity.View.AnimationKind.Game] = value;
        }
        public readonly ref T InitialThingSpawningRef => ref Values[(int) Zayats.Unity.View.AnimationKind.InitialThingSpawning];
        public T InitialThingSpawning
        {
            readonly get => Values[(int) Zayats.Unity.View.AnimationKind.InitialThingSpawning];
            set => Values[(int) Zayats.Unity.View.AnimationKind.InitialThingSpawning] = value;
        }
        public static implicit operator T[](AnimationArray<T> a) => a.Values;
        public readonly T[] Array => Values;
        public readonly ref T this[Zayats.Unity.View.AnimationKind key] => ref GetRef(key);
        public readonly ref T this[int index] => ref Values[index];
        public readonly int Length => 3;
        public static bool operator==(AnimationArray<T> a, AnimationArray<T> b)
        {
            for (int i = 0; i < a.Length; i++)
                if (!a.Values[i].Equals(b.Values[i]))
                    return false;
            return true;
        }
        public static bool operator!=(AnimationArray<T> a, AnimationArray<T> b)
        {
            return !(a == b);
        }
        public void FixSize()
        {
            if (Values is null || Values.Length != Length)
                System.Array.Resize(ref Values, Length);
        }
    }
    [Serializable]
    public partial struct CornersArray<T>
    {
        public /*readonly*/ T[] Values;
        private CornersArray(T[] values) => Values = values;
        public static CornersArray<T> Create() => new CornersArray<T>(new T[3]);
        public readonly ref T GetRef(Zayats.Unity.View.Corners key)
        {
            return ref Values[(int) key];
        }
        public readonly T Get(Zayats.Unity.View.Corners key)
        {
            return Values[(int) key];
        }
        public readonly void Set(Zayats.Unity.View.Corners key, T value)
        {
            Values[(int) key] = value;
        }
        public readonly ref T TopLeftRef => ref Values[(int) Zayats.Unity.View.Corners.TopLeft];
        public T TopLeft
        {
            readonly get => Values[(int) Zayats.Unity.View.Corners.TopLeft];
            set => Values[(int) Zayats.Unity.View.Corners.TopLeft] = value;
        }
        public readonly ref T BottomRightRef => ref Values[(int) Zayats.Unity.View.Corners.BottomRight];
        public T BottomRight
        {
            readonly get => Values[(int) Zayats.Unity.View.Corners.BottomRight];
            set => Values[(int) Zayats.Unity.View.Corners.BottomRight] = value;
        }
        public readonly ref T BottomLeftRef => ref Values[(int) Zayats.Unity.View.Corners.BottomLeft];
        public T BottomLeft
        {
            readonly get => Values[(int) Zayats.Unity.View.Corners.BottomLeft];
            set => Values[(int) Zayats.Unity.View.Corners.BottomLeft] = value;
        }
        public static implicit operator T[](CornersArray<T> a) => a.Values;
        public readonly T[] Array => Values;
        public readonly ref T this[Zayats.Unity.View.Corners key] => ref GetRef(key);
        public readonly ref T this[int index] => ref Values[index];
        public readonly int Length => 3;
        public static bool operator==(CornersArray<T> a, CornersArray<T> b)
        {
            for (int i = 0; i < a.Length; i++)
                if (!a.Values[i].Equals(b.Values[i]))
                    return false;
            return true;
        }
        public static bool operator!=(CornersArray<T> a, CornersArray<T> b)
        {
            return !(a == b);
        }
        public void FixSize()
        {
            if (Values is null || Values.Length != Length)
                System.Array.Resize(ref Values, Length);
        }
    }
    [Serializable]
    public partial struct GameplayButtonArray<T>
    {
        public /*readonly*/ T[] Values;
        private GameplayButtonArray(T[] values) => Values = values;
        public static GameplayButtonArray<T> Create() => new GameplayButtonArray<T>(new T[4]);
        public readonly ref T GetRef(Zayats.Unity.View.GameplayButtonKind key)
        {
            return ref Values[(int) key];
        }
        public readonly T Get(Zayats.Unity.View.GameplayButtonKind key)
        {
            return Values[(int) key];
        }
        public readonly void Set(Zayats.Unity.View.GameplayButtonKind key, T value)
        {
            Values[(int) key] = value;
        }
        public readonly ref T RollRef => ref Values[(int) Zayats.Unity.View.GameplayButtonKind.Roll];
        public T Roll
        {
            readonly get => Values[(int) Zayats.Unity.View.GameplayButtonKind.Roll];
            set => Values[(int) Zayats.Unity.View.GameplayButtonKind.Roll] = value;
        }
        public readonly ref T SettingsRef => ref Values[(int) Zayats.Unity.View.GameplayButtonKind.Settings];
        public T Settings
        {
            readonly get => Values[(int) Zayats.Unity.View.GameplayButtonKind.Settings];
            set => Values[(int) Zayats.Unity.View.GameplayButtonKind.Settings] = value;
        }
        public readonly ref T RestartRef => ref Values[(int) Zayats.Unity.View.GameplayButtonKind.Restart];
        public T Restart
        {
            readonly get => Values[(int) Zayats.Unity.View.GameplayButtonKind.Restart];
            set => Values[(int) Zayats.Unity.View.GameplayButtonKind.Restart] = value;
        }
        public readonly ref T TempBuyRef => ref Values[(int) Zayats.Unity.View.GameplayButtonKind.TempBuy];
        public T TempBuy
        {
            readonly get => Values[(int) Zayats.Unity.View.GameplayButtonKind.TempBuy];
            set => Values[(int) Zayats.Unity.View.GameplayButtonKind.TempBuy] = value;
        }
        public static implicit operator T[](GameplayButtonArray<T> a) => a.Values;
        public readonly T[] Array => Values;
        public readonly ref T this[Zayats.Unity.View.GameplayButtonKind key] => ref GetRef(key);
        public readonly ref T this[int index] => ref Values[index];
        public readonly int Length => 4;
        public static bool operator==(GameplayButtonArray<T> a, GameplayButtonArray<T> b)
        {
            for (int i = 0; i < a.Length; i++)
                if (!a.Values[i].Equals(b.Values[i]))
                    return false;
            return true;
        }
        public static bool operator!=(GameplayButtonArray<T> a, GameplayButtonArray<T> b)
        {
            return !(a == b);
        }
        public void FixSize()
        {
            if (Values is null || Values.Length != Length)
                System.Array.Resize(ref Values, Length);
        }
    }
    [Serializable]
    public partial struct GameplayTextArray<T>
    {
        public /*readonly*/ T[] Values;
        private GameplayTextArray(T[] values) => Values = values;
        public static GameplayTextArray<T> Create() => new GameplayTextArray<T>(new T[4]);
        public readonly ref T GetRef(Zayats.Unity.View.GameplayTextKind key)
        {
            return ref Values[(int) key];
        }
        public readonly T Get(Zayats.Unity.View.GameplayTextKind key)
        {
            return Values[(int) key];
        }
        public readonly void Set(Zayats.Unity.View.GameplayTextKind key, T value)
        {
            Values[(int) key] = value;
        }
        public readonly ref T WinRef => ref Values[(int) Zayats.Unity.View.GameplayTextKind.Win];
        public T Win
        {
            readonly get => Values[(int) Zayats.Unity.View.GameplayTextKind.Win];
            set => Values[(int) Zayats.Unity.View.GameplayTextKind.Win] = value;
        }
        public readonly ref T SeedRef => ref Values[(int) Zayats.Unity.View.GameplayTextKind.Seed];
        public T Seed
        {
            readonly get => Values[(int) Zayats.Unity.View.GameplayTextKind.Seed];
            set => Values[(int) Zayats.Unity.View.GameplayTextKind.Seed] = value;
        }
        public readonly ref T CoinCounterRef => ref Values[(int) Zayats.Unity.View.GameplayTextKind.CoinCounter];
        public T CoinCounter
        {
            readonly get => Values[(int) Zayats.Unity.View.GameplayTextKind.CoinCounter];
            set => Values[(int) Zayats.Unity.View.GameplayTextKind.CoinCounter] = value;
        }
        public readonly ref T RollValueRef => ref Values[(int) Zayats.Unity.View.GameplayTextKind.RollValue];
        public T RollValue
        {
            readonly get => Values[(int) Zayats.Unity.View.GameplayTextKind.RollValue];
            set => Values[(int) Zayats.Unity.View.GameplayTextKind.RollValue] = value;
        }
        public static implicit operator T[](GameplayTextArray<T> a) => a.Values;
        public readonly T[] Array => Values;
        public readonly ref T this[Zayats.Unity.View.GameplayTextKind key] => ref GetRef(key);
        public readonly ref T this[int index] => ref Values[index];
        public readonly int Length => 4;
        public static bool operator==(GameplayTextArray<T> a, GameplayTextArray<T> b)
        {
            for (int i = 0; i < a.Length; i++)
                if (!a.Values[i].Equals(b.Values[i]))
                    return false;
            return true;
        }
        public static bool operator!=(GameplayTextArray<T> a, GameplayTextArray<T> b)
        {
            return !(a == b);
        }
        public void FixSize()
        {
            if (Values is null || Values.Length != Length)
                System.Array.Resize(ref Values, Length);
        }
    }
    [Serializable]
    public partial struct MaterialArray<T>
    {
        public /*readonly*/ T[] Values;
        private MaterialArray(T[] values) => Values = values;
        public static MaterialArray<T> Create() => new MaterialArray<T>(new T[2]);
        public readonly ref T GetRef(Zayats.Unity.View.MaterialKind key)
        {
            return ref Values[(int) key];
        }
        public readonly T Get(Zayats.Unity.View.MaterialKind key)
        {
            return Values[(int) key];
        }
        public readonly void Set(Zayats.Unity.View.MaterialKind key, T value)
        {
            Values[(int) key] = value;
        }
        public readonly ref T DefaultRef => ref Values[(int) Zayats.Unity.View.MaterialKind.Default];
        public T Default
        {
            readonly get => Values[(int) Zayats.Unity.View.MaterialKind.Default];
            set => Values[(int) Zayats.Unity.View.MaterialKind.Default] = value;
        }
        public readonly ref T PreviewRef => ref Values[(int) Zayats.Unity.View.MaterialKind.Preview];
        public T Preview
        {
            readonly get => Values[(int) Zayats.Unity.View.MaterialKind.Preview];
            set => Values[(int) Zayats.Unity.View.MaterialKind.Preview] = value;
        }
        public static implicit operator T[](MaterialArray<T> a) => a.Values;
        public readonly T[] Array => Values;
        public readonly ref T this[Zayats.Unity.View.MaterialKind key] => ref GetRef(key);
        public readonly ref T this[int index] => ref Values[index];
        public readonly int Length => 2;
        public static bool operator==(MaterialArray<T> a, MaterialArray<T> b)
        {
            for (int i = 0; i < a.Length; i++)
                if (!a.Values[i].Equals(b.Values[i]))
                    return false;
            return true;
        }
        public static bool operator!=(MaterialArray<T> a, MaterialArray<T> b)
        {
            return !(a == b);
        }
        public void FixSize()
        {
            if (Values is null || Values.Length != Length)
                System.Array.Resize(ref Values, Length);
        }
    }
}

#pragma warning restore
