using System.Collections.Generic;
using UnityEngine;

namespace RollOfLegends
{
    public enum MonsterTier
    {
        Small,
        Medium,
        Big,
    }

    /// <summary>
    /// A monster waiting on a tile. Landing there starts a fight: you get a random fight card and roll it.
    /// Rolling one of the monster's winning numbers defeats it; otherwise it attacks you.
    /// Bigger monsters have fewer winning numbers but give more levels.
    /// </summary>
    public class Monster
    {
        public readonly MonsterTier Tier;
        public readonly int[] WinningNumbers;

        public Monster(MonsterTier tier, params int[] winningNumbers)
        {
            Tier = tier;
            WinningNumbers = winningNumbers;
        }

        /// <summary>A monster of this size with a random set of winning numbers (small: 5, medium: 3-4, big: 1-2).</summary>
        public static Monster Create(MonsterTier tier)
        {
            int count = tier == MonsterTier.Small ? 5 : tier == MonsterTier.Medium ? Random.Range(3, 5) : Random.Range(1, 3);
            var numbers = new List<int> { 1, 2, 3, 4, 5, 6 };
            while (numbers.Count > count)
            {
                numbers.RemoveAt(Random.Range(0, numbers.Count));
            }
            return new Monster(tier, numbers.ToArray());
        }

        public bool IsBeatenBy(int roll)
        {
            return System.Array.IndexOf(WinningNumbers, roll) >= 0;
        }

        /// <summary>Chance to win with a plain 1-6 roll.</summary>
        public float WinChance => WinningNumbers.Length / 6f;

        /// <summary>Damage the monster deals when you lose (before your defense).</summary>
        public int Damage => Tier == MonsterTier.Small ? 2 : Tier == MonsterTier.Medium ? 3 : 5;

        /// <summary>Levels gained for beating it: small +1, medium +2, big +3.</summary>
        public int LevelReward => Tier == MonsterTier.Small ? 1 : Tier == MonsterTier.Medium ? 2 : 3;
        public int HpReward => Tier == MonsterTier.Small ? 3 : Tier == MonsterTier.Medium ? 0 : 3;

        public string TierName => Tier.ToString();

        /// <summary>"1·2·6" — shown above the monster and in the landing preview.</summary>
        public string NumbersText => string.Join("·", WinningNumbers);
    }
}
