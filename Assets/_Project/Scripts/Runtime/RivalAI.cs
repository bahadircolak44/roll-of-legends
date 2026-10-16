using System;
using System.Collections.Generic;
using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// "Standard" difficulty rival: scores every card by the average outcome of its possible rolls
    /// (including roll bonuses like forest or haste) and plays the best one.
    /// Easier/harder rivals can later change the scores or add randomness.
    /// </summary>
    public static class RivalAI
    {
        public static int ChooseCard(IReadOnlyList<DiceCard> hand, Fighter self, Fighter enemy,
            Func<int, TileEffect> effectAt, int tileCount, Func<int, Monster> monsterAt = null)
        {
            int bestSlot = 0;
            float bestScore = float.MinValue;
            for (int slot = 0; slot < hand.Count; slot++)
            {
                int[] rolls = Dice.PossibleValues(hand[slot].Type, hand[slot].FixedValue);
                float total = 0f;
                foreach (int roll in rolls)
                {
                    int steps = Mathf.Max(1, roll + self.NextRollBonus);
                    total += ScoreMove(self, enemy, effectAt, tileCount, steps, monsterAt);
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
        public static float ScoreMove(Fighter self, Fighter enemy, Func<int, TileEffect> effectAt, int tileCount, int steps,
            Func<int, Monster> monsterAt = null)
        {
            float score = 0f;
            for (int step = 1; step <= steps; step++)
            {
                int tile = (self.Tile + step) % tileCount;
                if (tile == enemy.HomeTile)
                {
                    score += BaseHitValue(self, enemy); // hitting the enemy base is the way to win
                }
                else if (tile == self.HomeTile)
                {
                    score += 2.5f; // a finished lap gives a level
                }
            }

            int landing = (self.Tile + steps) % tileCount;
            if (landing == enemy.Tile)
            {
                score += HeroHitValue(self, enemy);
            }

            switch (effectAt(landing))
            {
                case TileEffect.Sword: score += HeroHitValue(self, enemy); break;
                case TileEffect.Spear: score += BaseHitValue(self, enemy); break;
                case TileEffect.SkillCard: score += 3.5f; break;
                case TileEffect.HealthPotion: score += self.Hp < self.StartHp ? 3.5f : 2f; break;
                case TileEffect.RepairElixir: score += 2.5f; break;
                case TileEffect.Shield: score += 3f; break;
                case TileEffect.SpellBook: score += 3.5f; break;
                case TileEffect.Chest: score += 3.5f; break;
                case TileEffect.Spikes: score -= 3.5f; break;
                case TileEffect.Snare: score -= 3f; break;
                case TileEffect.Forest: score -= 2.5f; break;
                case TileEffect.Monster: score += MonsterValue(monsterAt?.Invoke(landing)); break;
            }
            return score;
        }

        /// <summary>
        /// Expected gain of a fight: chance to win times the reward, minus chance to lose times the damage.
        /// The numbers are only picked on landing, so it guesses from the monster's size (a little better
        /// than a plain roll, because it gets to choose a card from its hand).
        /// </summary>
        private static float MonsterValue(Monster monster)
        {
            if (monster == null) return 0f;
            float chance = Mathf.Min(0.9f, monster.ExpectedWinChance + 0.12f);
            float reward = monster.LevelReward * 5f + monster.HpReward * 0.6f;
            float loss = monster.Damage * 0.8f;
            return chance * reward - (1f - chance) * loss;
        }

        /// <summary>Damaging the base; a hit that destroys it wins the game, so it is worth everything.</summary>
        private static float BaseHitValue(Fighter self, Fighter enemy)
        {
            return enemy.BaseHp <= self.Siege ? 100f : 4f + self.Siege;
        }

        /// <summary>Hitting the other hero; knocking it out (it loses all its levels) is worth a lot more.</summary>
        private static float HeroHitValue(Fighter self, Fighter enemy)
        {
            int damage = Mathf.Max(1, self.Attack - enemy.Defense);
            return damage >= enemy.Hp ? 6f + enemy.Level * 2f : 2f + damage;
        }

        /// <summary>In a monster fight: the card from the hand with the best chance to roll a winning number.</summary>
        public static int ChooseFightCard(IReadOnlyList<DiceCard> hand, Monster monster)
        {
            int best = 0;
            for (int slot = 1; slot < hand.Count; slot++)
            {
                if (monster.WinChance(hand[slot]) > monster.WinChance(hand[best])) best = slot;
            }
            return best;
        }
    }
}
