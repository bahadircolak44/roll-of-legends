using System;
using System.Collections.Generic;

namespace RollOfLegends
{
    /// <summary>
    /// "Standard" difficulty rival: scores every card by the average outcome of its possible rolls
    /// and plays the best one. Easier/harder rivals can later change the scores or add randomness.
    /// </summary>
    public static class RivalAI
    {
        public static int ChooseCard(IReadOnlyList<DiceCard> hand, Fighter self, Fighter enemy,
            Func<int, TileEffect> effectAt, int tileCount)
        {
            int bestSlot = 0;
            float bestScore = float.MinValue;
            for (int slot = 0; slot < hand.Count; slot++)
            {
                int[] rolls = Dice.PossibleValues(hand[slot].Type, hand[slot].FixedValue);
                float total = 0f;
                foreach (int roll in rolls)
                {
                    total += ScoreMove(self, enemy, effectAt, tileCount, roll);
                }

                float average = total / rolls.Length;
                if (average > bestScore)
                {
                    bestScore = average;
                    bestSlot = slot;
                }
            }
            return bestSlot;
        }

        /// <summary>How good it is for <paramref name="self"/> to move <paramref name="steps"/> tiles.</summary>
        public static float ScoreMove(Fighter self, Fighter enemy, Func<int, TileEffect> effectAt, int tileCount, int steps)
        {
            float score = 0f;
            for (int step = 1; step <= steps; step++)
            {
                if ((self.Tile + step) % tileCount == enemy.HomeTile)
                {
                    score += 6f; // hitting the enemy base is the way to win
                }
            }

            int landing = (self.Tile + steps) % tileCount;
            if (landing == enemy.Tile)
            {
                score += 3f;
            }

            switch (effectAt(landing))
            {
                case TileEffect.Sword: score += 4f; break;
                case TileEffect.Potion: score += self.Hp < self.MaxHp ? 3f : 0.5f; break;
                case TileEffect.Spikes: score -= 4f; break;
                case TileEffect.Snare: score -= 3f; break;
                case TileEffect.Monster: score += self.Attack >= TileRules.MonsterPower ? 3f : -4f; break;
            }
            return score;
        }
    }
}
