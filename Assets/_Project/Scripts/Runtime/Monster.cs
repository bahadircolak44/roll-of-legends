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
    /// A monster waiting on a tile. Its winning numbers are a secret until someone lands on it: then it
    /// picks them at random (small: 4 numbers, medium: 3, big: just 1). The fighter gets a new card and
    /// chooses a card from the hand to roll; rolling one of the numbers defeats the monster, otherwise
    /// it strikes back. Bigger monsters are harder but give more levels.
    /// </summary>
    public class Monster
    {
        public readonly MonsterTier Tier;

        /// <summary>Numbers that beat it in the current fight (empty until it is revealed).</summary>
        public int[] WinningNumbers { get; private set; }

        /// <summary>True while a fight is on and the numbers are known.</summary>
        public bool IsRevealed { get; private set; }

        private readonly bool _fixedNumbers;

        /// <summary>A monster of this size; its numbers are picked when someone lands on it.</summary>
        public Monster(MonsterTier tier)
        {
            Tier = tier;
            WinningNumbers = new int[0];
        }

        /// <summary>A monster that always uses these winning numbers (handy for testing).</summary>
        public Monster(MonsterTier tier, params int[] winningNumbers)
        {
            Tier = tier;
            WinningNumbers = winningNumbers;
            _fixedNumbers = true;
        }

        public static Monster Create(MonsterTier tier)
        {
            return new Monster(tier);
        }

        /// <summary>How many winning numbers a monster of this size picks.</summary>
        public static int NumberCount(MonsterTier tier)
        {
            return tier == MonsterTier.Small ? 4 : tier == MonsterTier.Medium ? 3 : 1;
        }

        /// <summary>Someone landed on it: it picks its winning numbers for this fight.</summary>
        public void Reveal()
        {
            IsRevealed = true;
            if (_fixedNumbers) return;

            var numbers = new List<int> { 1, 2, 3, 4, 5, 6 };
            while (numbers.Count > NumberCount(Tier))
            {
                numbers.RemoveAt(Random.Range(0, numbers.Count));
            }
            WinningNumbers = numbers.ToArray();
        }

        /// <summary>The fight is over: next time it picks new numbers.</summary>
        public void Hide()
        {
            IsRevealed = false;
        }

        public bool IsBeatenBy(int roll)
        {
            return System.Array.IndexOf(WinningNumbers, roll) >= 0;
        }

        /// <summary>How many of a card's possible rolls win, out of how many (e.g. 2 of 3 for ODD against 1·3·4).</summary>
        public int WinningRolls(DiceCard card, out int possibleRolls)
        {
            int[] rolls = Dice.PossibleValues(card.Type, card.FixedValue);
            possibleRolls = rolls.Length;
            int wins = 0;
            foreach (int roll in rolls)
            {
                if (IsBeatenBy(roll)) wins++;
            }
            return wins;
        }

        public float WinChance(DiceCard card)
        {
            int wins = WinningRolls(card, out int possible);
            return possible == 0 ? 0f : (float)wins / possible;
        }

        /// <summary>Rough chance to win before the numbers are known (with a plain 1-6 roll).</summary>
        public float ExpectedWinChance => NumberCount(Tier) / 6f;

        /// <summary>Damage the monster deals when you lose (before your defense).</summary>
        public int Damage => Tier == MonsterTier.Small ? 2 : Tier == MonsterTier.Medium ? 3 : 5;

        /// <summary>Levels gained for beating it: small +1, medium +2, big +3.</summary>
        public int LevelReward => Tier == MonsterTier.Small ? 1 : Tier == MonsterTier.Medium ? 2 : 3;
        public int HpReward => Tier == MonsterTier.Small ? 3 : Tier == MonsterTier.Medium ? 0 : 3;

        public string TierName => Tier.ToString();

        /// <summary>"1·2·6" during a fight, "?" before.</summary>
        public string NumbersText => IsRevealed || _fixedNumbers ? string.Join("·", WinningNumbers) : "?";

        /// <summary>The label above the monster: its size until a fight starts, then its winning numbers.</summary>
        public string LabelText => IsRevealed ? NumbersText : TierName.ToUpper();
    }
}
