using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// Game state of one side (you or the rival): health, attack, base health and position.
    /// Plain C# (no scene object) so the rules are easy to test.
    /// </summary>
    public class Fighter
    {
        public readonly string Name;
        public readonly int HomeTile;
        public readonly int MaxHp;

        public int Hp;
        public int Attack;
        public int BaseHp;
        public int Tile;
        public int SkipTurns;
        public int TurnsTaken;

        public Fighter(string name, int homeTile, int maxHp, int attack, int baseHp)
        {
            Name = name;
            HomeTile = homeTile;
            MaxHp = maxHp;
            Hp = maxHp;
            Attack = attack;
            BaseHp = baseHp;
            Tile = homeTile;
        }

        public bool IsKnockedOut => Hp <= 0;

        public void TakeDamage(int amount)
        {
            Hp = Mathf.Max(0, Hp - amount);
        }

        public void Heal(int amount)
        {
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        /// <summary>Back to the home base with full health (after being knocked out).</summary>
        public void Respawn()
        {
            Hp = MaxHp;
            Tile = HomeTile;
        }
    }
}
