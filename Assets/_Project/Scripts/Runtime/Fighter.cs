using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// Game state of one side (you or the rival): hero health, attack, defense, base health and position.
    /// Health has no upper limit: potions can take it past the starting value.
    /// Plain C# (no scene object) so the rules are easy to test.
    /// </summary>
    public class Fighter
    {
        public readonly string Name;
        public readonly int HomeTile;
        public readonly int StartHp;
        public readonly int StartAttack;

        public int Hp;
        public int Attack;
        public int Defense;
        public int BaseHp;
        public int Tile;
        public int SkipTurns;
        public int TurnsTaken;

        /// <summary>Added to the next roll (forest -3, haste +2), then cleared.</summary>
        public int NextRollBonus;

        /// <summary>The next card this side draws is a rare DOUBLE card (from a treasure chest).</summary>
        public bool LuckyDraw;

        public Fighter(string name, int homeTile, int startHp, int attack, int baseHp)
        {
            Name = name;
            HomeTile = homeTile;
            StartHp = startHp;
            StartAttack = attack;
            Hp = startHp;
            Attack = attack;
            BaseHp = baseHp;
            Tile = homeTile;
        }

        public bool IsKnockedOut => Hp <= 0;

        /// <summary>Takes damage reduced by Defense (but always at least 1). Returns the damage actually taken.</summary>
        public int TakeDamage(int amount)
        {
            int taken = Mathf.Max(1, amount - Defense);
            Hp = Mathf.Max(0, Hp - taken);
            return taken;
        }

        /// <summary>Damage that goes straight through armor (e.g. magic).</summary>
        public void TakePiercingDamage(int amount)
        {
            Hp = Mathf.Max(0, Hp - amount);
        }

        public void Heal(int amount)
        {
            Hp += amount; // no upper limit
        }

        public int DamageBase(int amount)
        {
            int taken = Mathf.Min(BaseHp, amount);
            BaseHp -= taken;
            return taken;
        }

        public void RepairBase(int amount)
        {
            BaseHp += amount; // no upper limit
        }

        /// <summary>The number of tiles to move for a roll after bonuses (always at least 1). Clears the bonus.</summary>
        public int UseRollBonus(int roll)
        {
            int steps = Mathf.Max(1, roll + NextRollBonus);
            NextRollBonus = 0;
            return steps;
        }

        /// <summary>
        /// Knocked out: back home, starting over from scratch. All buffs are lost (attack, defense,
        /// roll bonuses, rare card); only the base keeps its health.
        /// </summary>
        public void Respawn()
        {
            Hp = StartHp;
            Attack = StartAttack;
            Defense = 0;
            NextRollBonus = 0;
            SkipTurns = 0;
            LuckyDraw = false;
            Tile = HomeTile;
        }
    }
}
