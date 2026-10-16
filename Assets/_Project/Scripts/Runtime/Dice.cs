using UnityEngine;

namespace RollOfLegends
{
    public enum DiceType
    {
        Odd,    // rolls 1, 3 or 5
        Even,   // rolls 2, 4 or 6
        Fixed,  // always rolls the card's own number
        Random, // rolls any number from 1 to 6
        Double, // rare: rolls two dice, 2 to 12
    }

    /// <summary>
    /// Dice rules. Kept free of scene objects so they are easy to read and test.
    /// </summary>
    public static class Dice
    {
        private static readonly int[] OddFaces = { 1, 3, 5 };
        private static readonly int[] EvenFaces = { 2, 4, 6 };
        private static readonly int[] AllFaces = { 1, 2, 3, 4, 5, 6 };
        private static readonly DiceType[] CommonTypes = { DiceType.Odd, DiceType.Even, DiceType.Fixed, DiceType.Random };

        /// <summary>One of the four common dice types (not the rare DOUBLE).</summary>
        public static DiceType RandomType()
        {
            return CommonTypes[Random.Range(0, CommonTypes.Length)];
        }

        /// <summary>The value of each die thrown: one number, or two for a DOUBLE.</summary>
        public static int[] RollDice(DiceType type, int fixedValue)
        {
            switch (type)
            {
                case DiceType.Odd:
                    return new[] { OddFaces[Random.Range(0, OddFaces.Length)] };
                case DiceType.Even:
                    return new[] { EvenFaces[Random.Range(0, EvenFaces.Length)] };
                case DiceType.Fixed:
                    return new[] { Mathf.Clamp(fixedValue, 1, 6) };
                case DiceType.Double:
                    return new[] { Random.Range(1, 7), Random.Range(1, 7) };
                default:
                    return new[] { Random.Range(1, 7) }; // the max value of Random.Range(int, int) is exclusive
            }
        }

        /// <summary>The total rolled.</summary>
        public static int Roll(DiceType type, int fixedValue)
        {
            int total = 0;
            foreach (int die in RollDice(type, fixedValue)) total += die;
            return total;
        }

        /// <summary>Every possible total, once per way of rolling it (so a DOUBLE lists 7 six times).</summary>
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
                case DiceType.Double:
                    var totals = new int[36];
                    for (int a = 0; a < 6; a++)
                        for (int b = 0; b < 6; b++)
                            totals[a * 6 + b] = a + b + 2;
                    return totals;
                default:
                    return AllFaces;
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
                case DiceType.Double:
                    return "Double (2-12)";
                default:
                    return "Random (1-6)";
            }
        }
    }
}
