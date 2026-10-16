using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// Runs a match against the rival AI.
    /// Your turn: tap a card -> the die rolls -> your hero hops -> the tile's effect happens -> you draw a new card.
    /// Then the rival does the same. Passing the enemy base damages it; the first to destroy the other base wins.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Scene objects")]
        [SerializeField] private Board _board;
        [SerializeField] private BoardEffects _effects;
        [SerializeField] private DiceView _dice;
        [SerializeField] private Transform _heroView;
        [SerializeField] private Transform _rivalView;

        [Header("UI")]
        [SerializeField] private Button _playButton;
        [SerializeField] private CardView[] _cardSlots;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _messageText;

        [Header("Starting stats (both sides)")]
        [SerializeField] private int _startHp = 20;
        [SerializeField] private int _startAttack = 2;
        [SerializeField] private int _baseHp = 8;

        [Header("Movement and pacing")]
        [Tooltip("How far above the tile the heroes' centers stand.")]
        [SerializeField] private float _heroHeight = 0.5f;

        [Tooltip("Sideways offset so both heroes stay visible on the same tile.")]
        [SerializeField] private float _heroSideOffset = 0.22f;

        [Tooltip("Seconds for one hop from a tile to the next.")]
        [SerializeField] private float _hopDuration = 0.3f;

        [Tooltip("How high the heroes jump on each hop.")]
        [SerializeField] private float _hopHeight = 0.6f;

        [Tooltip("Pause after the die lands, before moving.")]
        [SerializeField] private float _pauseAfterDice = 0.3f;

        [Tooltip("Pause so messages can be read, e.g. between turns.")]
        [SerializeField] private float _messagePause = 0.8f;

        private DiceCard[] _hand;
        private DiceCard[] _rivalHand;

        public Fighter Player { get; private set; }
        public Fighter Rival { get; private set; }
        public IReadOnlyList<DiceCard> Hand => _hand;
        public int LastRoll { get; private set; }
        public DiceCard LastCard { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>True while anything is happening (your move or the rival's turn). Cards are locked meanwhile.</summary>
        public bool IsBusy { get; private set; }

        public string Message => _messageText.text;

        private void Awake()
        {
            _hand = new DiceCard[_cardSlots.Length];
            _rivalHand = new DiceCard[_cardSlots.Length];
            _playButton.onClick.AddListener(StartGame);

            for (int i = 0; i < _cardSlots.Length; i++)
            {
                int slot = i; // copy for the click handler below
                _cardSlots[i].Button.onClick.AddListener(() => PlayCard(slot));
            }
        }

        private void Start()
        {
            _heroView.gameObject.SetActive(false);
            _rivalView.gameObject.SetActive(false);
            _dice.gameObject.SetActive(false);
            foreach (CardView card in _cardSlots)
            {
                card.Hide();
            }
            _playButton.gameObject.SetActive(true);
            _statusText.text = "";
            Say("Press PLAY to start");
        }

        public void StartGame()
        {
            StopAllCoroutines();
            IsBusy = false;
            IsGameOver = false;

            int rivalHome = _board.TileCount / 2; // the corner diagonally opposite START
            Player = new Fighter("You", 0, _startHp, _startAttack, _baseHp);
            Rival = new Fighter("Rival", rivalHome, _startHp, _startAttack, _baseHp);
            _effects.Setup(Player.HomeTile, Rival.HomeTile);

            PlaceView(Player);
            PlaceView(Rival);
            _heroView.gameObject.SetActive(true);
            _rivalView.gameObject.SetActive(true);
            _playButton.gameObject.SetActive(false);

            for (int slot = 0; slot < _hand.Length; slot++)
            {
                DrawInto(slot);
                _rivalHand[slot] = DiceCard.Draw();
            }
            SetCardsInteractable(true);

            UpdateStatus();
            Say("Your turn: pick a card");
        }

        public void PlayCard(int slot)
        {
            if (IsBusy || IsGameOver || Player == null)
            {
                return; // ignore taps while something is happening
            }
            StartCoroutine(PlayerTurn(slot));
        }

        /// <summary>Puts a specific card in your hand (handy for testing and debugging).</summary>
        public void GiveCard(int slot, DiceCard card)
        {
            _hand[slot] = card;
            _cardSlots[slot].Show(card);
        }

        private IEnumerator PlayerTurn(int slot)
        {
            IsBusy = true;
            SetCardsInteractable(false);

            DiceCard card = _hand[slot];
            _cardSlots[slot].Hide(); // the card leaves the hand
            yield return TakeTurn(Player, Rival, card);
            if (!IsGameOver)
            {
                DrawInto(slot);
                yield return RivalTurns();
            }

            if (!IsGameOver)
            {
                SetCardsInteractable(true);
                Say("Your turn: pick a card");
            }
            IsBusy = false;
        }

        /// <summary>The rival plays; if you are snared, the rival keeps playing until your turn comes back.</summary>
        private IEnumerator RivalTurns()
        {
            while (true)
            {
                yield return new WaitForSeconds(_messagePause);

                if (Rival.SkipTurns > 0)
                {
                    Rival.SkipTurns--;
                    Rival.TurnsTaken++;
                    Say("Rival is stuck in a snare and skips this turn.");
                    yield return new WaitForSeconds(_messagePause);
                }
                else
                {
                    int slot = RivalAI.ChooseCard(_rivalHand, Rival, Player, _effects.GetEffect, _board.TileCount);
                    DiceCard card = _rivalHand[slot];
                    _rivalHand[slot] = DiceCard.Draw();
                    yield return TakeTurn(Rival, Player, card);
                    if (IsGameOver) yield break;
                }

                if (Player.SkipTurns == 0) yield break;

                Player.SkipTurns--;
                Player.TurnsTaken++;
                Say("You are stuck in a snare and skip this turn.");
                yield return new WaitForSeconds(_messagePause);
            }
        }

        private IEnumerator TakeTurn(Fighter mover, Fighter other, DiceCard card)
        {
            mover.TurnsTaken++;
            LastCard = card;
            LastRoll = card.Roll();

            Say($"{mover.Name} played {card.Describe()}...");
            yield return _dice.Roll(LastRoll);
            Say($"{mover.Name} played {card.Describe()} and rolled {LastRoll}.");
            yield return new WaitForSeconds(_pauseAfterDice);

            for (int step = 0; step < LastRoll; step++)
            {
                mover.Tile = Board.Advance(mover.Tile, 1, _board.TileCount);
                yield return HopTo(ViewOf(mover), PositionOn(mover));

                if (mover.Tile == other.HomeTile)
                {
                    other.BaseHp = Mathf.Max(0, other.BaseHp - mover.Attack);
                    Say($"{mover.Name} hit the {other.Name.ToLower()} base: -{mover.Attack}!");
                    UpdateStatus();
                    if (other.BaseHp == 0)
                    {
                        EndGame(mover == Player);
                        yield break;
                    }
                }
                else if (mover.Tile == mover.HomeTile)
                {
                    mover.Heal(TileRules.PassHomeHeal);
                    UpdateStatus();
                }
            }

            ResolveLanding(mover, other);
            UpdateStatus();
            yield return new WaitForSeconds(_messagePause);
        }

        private void ResolveLanding(Fighter mover, Fighter other)
        {
            if (mover.Tile == other.Tile)
            {
                other.TakeDamage(mover.Attack);
                Say($"{mover.Name} attacked {other.Name.ToLower()}: -{mover.Attack} HP!");
                if (other.IsKnockedOut) KnockOut(other);
                return;
            }

            TileEffect effect = _effects.GetEffect(mover.Tile);
            if (effect == TileEffect.None) return;

            Say(TileRules.Apply(mover, effect, out bool removeEffect));
            if (removeEffect)
            {
                _effects.MoveElsewhere(mover.Tile, Player.Tile, Rival.Tile);
            }
            if (mover.IsKnockedOut) KnockOut(mover);
        }

        private void KnockOut(Fighter fighter)
        {
            fighter.Respawn();
            PlaceView(fighter);
            Say($"{fighter.Name} {(fighter == Player ? "were" : "was")} knocked out and returned to base with full HP.");
        }

        private void EndGame(bool playerWon)
        {
            IsGameOver = true;
            Say(playerWon ? "YOU WIN! The rival base is destroyed." : "YOU LOSE! Your base is destroyed.");
            foreach (CardView card in _cardSlots)
            {
                card.Hide();
            }
            _playButton.GetComponentInChildren<Text>().text = "PLAY AGAIN";
            _playButton.gameObject.SetActive(true);
        }

        private void DrawInto(int slot)
        {
            _hand[slot] = DiceCard.Draw();
            _cardSlots[slot].Show(_hand[slot]);
        }

        private void SetCardsInteractable(bool interactable)
        {
            foreach (CardView card in _cardSlots)
            {
                card.Button.interactable = interactable;
            }
        }

        private IEnumerator HopTo(Transform view, Vector3 target)
        {
            Vector3 start = view.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / _hopDuration)
            {
                Vector3 position = Vector3.Lerp(start, target, t);
                position.y += Mathf.Sin(t * Mathf.PI) * _hopHeight; // goes up, then back down
                view.position = position;
                yield return null;
            }
            view.position = target;
        }

        private Transform ViewOf(Fighter fighter)
        {
            return fighter == Player ? _heroView : _rivalView;
        }

        private void PlaceView(Fighter fighter)
        {
            ViewOf(fighter).position = PositionOn(fighter);
        }

        private Vector3 PositionOn(Fighter fighter)
        {
            float side = fighter == Player ? -_heroSideOffset : _heroSideOffset;
            return _board.GetTilePosition(fighter.Tile) + new Vector3(side, _heroHeight, 0f);
        }

        private void UpdateStatus()
        {
            _statusText.text =
                $"<color=#7fb2ff><b>YOU</b></color>    HP {Player.Hp}/{Player.MaxHp}   ATK {Player.Attack}   BASE {Player.BaseHp}\n" +
                $"<color=#ff8a7a><b>RIVAL</b></color>  HP {Rival.Hp}/{Rival.MaxHp}   ATK {Rival.Attack}   BASE {Rival.BaseHp}";
        }

        private void Say(string message)
        {
            _messageText.text = message;
        }
    }
}
