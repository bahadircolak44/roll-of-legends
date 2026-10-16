using UnityEngine;

namespace RollOfLegends
{
    public enum DiceType
    {
        Odd,    // rolls 1, 3 or 5
        Even,   // rolls 2, 4 or 6
        Fixed,  // always rolls the same number (set on the GameController)
        Random, // rolls any number from 1 to 6
    }

    /// <summary>
    /// Dice rules. Kept free of scene objects so they are easy to read and test.
    /// </summary>
    public static class Dice
    {
        private static readonly int[] OddFaces = { 1, 3, 5 };
        private static readonly int[] EvenFaces = { 2, 4, 6 };
        private static readonly DiceType[] AllTypes = { DiceType.Odd, DiceType.Even, DiceType.Fixed, DiceType.Random };

        public static DiceType RandomType()
        {
            return AllTypes[Random.Range(0, AllTypes.Length)];
        }

        public static int Roll(DiceType type, int fixedValue)
        {
            switch (type)
            {
                case DiceType.Odd:
                    return OddFaces[Random.Range(0, OddFaces.Length)];
                case DiceType.Even:
                    return EvenFaces[Random.Range(0, EvenFaces.Length)];
                case DiceType.Fixed:
                    return Mathf.Clamp(fixedValue, 1, 6);
                default:
                    return Random.Range(1, 7); // the max value of Random.Range(int, int) is exclusive
            }
        }

        /// <summary>Every number this dice type can roll.</summary>
        public static int[] PossibleValues(DiceType type, int fixedValue)
        {
            switch (type)
            {
                case DiceType.Odd:
                    return OddFaces;
                case DiceType.Even:
                    return EvenFaces;
                case DiceType.Fixed:
                    return new[] { Mathf.Clamp(fixedValue, 1, 6) };
                default:
                    return new[] { 1, 2, 3, 4, 5, 6 };
            }
        }

        public static string Describe(DiceType type, int fixedValue)
        {
            switch (type)
            {
                case DiceType.Odd:
                    return "Odd (1, 3, 5)";
                case DiceType.Even:
                    return "Even (2, 4, 6)";
                case DiceType.Fixed:
                    return $"Fixed ({Mathf.Clamp(fixedValue, 1, 6)})";
                default:
                    return "Random (1-6)";
            }
        }
    }
}
