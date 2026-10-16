using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>
    /// Runs a match against the rival AI.
    /// Your turn: tap a card to see where you could land, tap it again to roll -> the die rolls ->
    /// your hero hops -> the tile's effect happens -> you draw a new card. Then the rival does the same.
    /// Passing the enemy base damages it; the first to destroy the other base wins.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public static readonly Color PlayerColor = new Color(0.36f, 0.62f, 1f);
        public static readonly Color RivalColor = new Color(1f, 0.42f, 0.36f);

        private static readonly Color GainColor = new Color(0.45f, 1f, 0.55f);
        private static readonly Color AttackColor = new Color(1f, 0.83f, 0.3f);
        private static readonly Color LossColor = new Color(1f, 0.38f, 0.35f);
        private static readonly Color BaseHitColor = new Color(1f, 0.6f, 0.2f);
        private static readonly Color SkipColor = new Color(0.8f, 0.65f, 1f);

        [Header("Scene objects")]
        [SerializeField] private Board _board;
        [SerializeField] private BoardEffects _effects;
        [SerializeField] private DiceView _dice;
        [SerializeField] private Transform _heroView;
        [SerializeField] private Transform _rivalView;
        [SerializeField] private Transform _playerCastle;
        [SerializeField] private Transform _rivalCastle;
        [SerializeField] private MoveHighlighter _highlighter;
        [SerializeField] private FxSpawner _fx;

        [Header("UI")]
        [SerializeField] private Button _playButton;
        [SerializeField] private GameObject _title;
        [SerializeField] private CardView[] _cardSlots;
        [SerializeField] private FighterBadge _playerBadge;
        [SerializeField] private FighterBadge _rivalBadge;
        [SerializeField] private EventLog _eventLog;
        [SerializeField] private Text _promptText;

        [Header("Names")]
        [SerializeField] private string _playerName = "Bahadır";
        [SerializeField] private string _rivalName = "Rival";

        [Header("Starting stats (both sides)")]
        [SerializeField] private int _startHp = 20;
        [SerializeField] private int _startAttack = 2;
        [SerializeField] private int _baseHp = 8;

        [Header("Movement and pacing")]
        [Tooltip("How far above the tile the heroes' centers stand.")]
        [SerializeField] private float _heroHeight = 0.62f;

        [Tooltip("Sideways offset so both heroes stay visible on the same tile.")]
        [SerializeField] private float _heroSideOffset = 0.27f;

        [Tooltip("Seconds for one hop from a tile to the next.")]
        [SerializeField] private float _hopDuration = 0.3f;

        [Tooltip("How high the heroes jump on each hop.")]
        [SerializeField] private float _hopHeight = 0.7f;

        [Tooltip("Pause after the die lands, before moving.")]
        [SerializeField] private float _pauseAfterDice = 0.3f;

        [Tooltip("Pause so events can be read, e.g. between turns.")]
        [SerializeField] private float _messagePause = 0.9f;

        private DiceCard[] _hand;
        private DiceCard[] _rivalHand;

        public Fighter Player { get; private set; }
        public Fighter Rival { get; private set; }
        public IReadOnlyList<DiceCard> Hand => _hand;
        public int LastRoll { get; private set; }
        public DiceCard LastCard { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>The card waiting for a second tap to roll, or -1.</summary>
        public int SelectedSlot { get; private set; } = -1;

        /// <summary>True while anything is happening (your move or the rival's turn). Cards are locked meanwhile.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>The short instruction shown above the cards.</summary>
        public string Message => _promptText.text;

        private void Awake()
        {
            _hand = new DiceCard[_cardSlots.Length];
            _rivalHand = new DiceCard[_cardSlots.Length];
            _playButton.onClick.AddListener(StartGame);

            for (int i = 0; i < _cardSlots.Length; i++)
            {
                int slot = i; // copy for the click handler below
                _cardSlots[i].Button.onClick.AddListener(() => TapCard(slot));
            }
        }

        private void Start()
        {
            _heroView.gameObject.SetActive(false);
            _rivalView.gameObject.SetActive(false);
            _dice.gameObject.SetActive(false);
            _playerBadge.gameObject.SetActive(false);
            _rivalBadge.gameObject.SetActive(false);
            foreach (CardView card in _cardSlots)
            {
                card.Hide();
            }
            _title.SetActive(true);
            _playButton.gameObject.SetActive(true);
            Say("Press PLAY to start");
            StartCoroutine(PrepareCardArt());
        }

        /// <summary>Draws the card pictures one per frame on the title screen, so the game never stutters later.</summary>
        private IEnumerator PrepareCardArt()
        {
            foreach (DiceType type in new[] { DiceType.Odd, DiceType.Even, DiceType.Fixed, DiceType.Random })
            {
                CardArt.Get(type);
                yield return null;
            }
            _ = CardArt.Glow;
        }

        public void StartGame()
        {
            StopAllCoroutines();
            IsBusy = false;
            IsGameOver = false;

            int rivalHome = _board.TileCount / 2; // the corner diagonally opposite START
            Player = new Fighter(_playerName, 0, _startHp, _startAttack, _baseHp);
            Rival = new Fighter(_rivalName, rivalHome, _startHp, _startAttack, _baseHp);
            _effects.Setup(Player.HomeTile, Rival.HomeTile);

            PlaceView(Player);
            PlaceView(Rival);
            _heroView.localScale = Vector3.one; // in case a landing squash was interrupted
            _rivalView.localScale = Vector3.one;
            _heroView.gameObject.SetActive(true);
            _rivalView.gameObject.SetActive(true);
            _playerBadge.gameObject.SetActive(true);
            _rivalBadge.gameObject.SetActive(true);
            _title.SetActive(false);
            _playButton.gameObject.SetActive(false);

            for (int slot = 0; slot < _hand.Length; slot++)
            {
                DrawInto(slot);
                _rivalHand[slot] = DiceCard.Draw();
            }
            ClearSelection();
            SetCardsInteractable(true);

            UpdateBadges();
            _eventLog.Clear();
            Log($"<b>A new match begins!</b> Destroy the {NameOf(Rival)} base to win.");
            Say("Your turn: pick a card");
        }

        /// <summary>First tap selects a card and shows where you could land; a second tap rolls it.</summary>
        public void TapCard(int slot)
        {
            if (IsBusy || IsGameOver || Player == null) return;

            if (SelectedSlot == slot)
            {
                PlayCard(slot);
                return;
            }

            SelectedSlot = slot;
            for (int i = 0; i < _cardSlots.Length; i++)
            {
                _cardSlots[i].SetSelected(i == slot);
            }
            DiceCard card = _hand[slot];
            _highlighter.Show(Player.Tile, Dice.PossibleValues(card.Type, card.FixedValue), CardArt.AccentColor(card.Type));
            Say($"Tap {CardArt.Title(card.Type)} again to roll!");
        }

        /// <summary>Plays a card right away (the second tap of TapCard).</summary>
        public void PlayCard(int slot)
        {
            if (IsBusy || IsGameOver || Player == null) return;
            ClearSelection();
            StartCoroutine(PlayerTurn(slot));
        }

        /// <summary>Puts a specific card in your hand (handy for testing and debugging).</summary>
        public void GiveCard(int slot, DiceCard card)
        {
            _hand[slot] = card;
            _cardSlots[slot].Show(card);
            if (SelectedSlot == slot) ClearSelection();
        }

        private void ClearSelection()
        {
            SelectedSlot = -1;
            foreach (CardView card in _cardSlots)
            {
                card.SetSelected(false);
            }
            _highlighter.Hide();
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
                Say($"{Rival.Name}'s turn...");
                yield return new WaitForSeconds(_messagePause);

                if (Rival.SkipTurns > 0)
                {
                    Rival.SkipTurns--;
                    Rival.TurnsTaken++;
                    Log($"{NameOf(Rival)} is stuck in a snare and <color=#{Hex(SkipColor)}>skips a turn</color>");
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
                Log($"{NameOf(Player)} is stuck in a snare and <color=#{Hex(SkipColor)}>skips a turn</color>");
            }
        }

        private IEnumerator TakeTurn(Fighter mover, Fighter other, DiceCard card)
        {
            mover.TurnsTaken++;
            LastCard = card;
            LastRoll = card.Roll();

            Say($"{mover.Name} plays {CardArt.Title(card.Type)}...");
            yield return _dice.Roll(LastRoll);
            Log($"{NameOf(mover)} played {CardTag(card)} → rolled <b>{LastRoll}</b>");
            yield return new WaitForSeconds(_pauseAfterDice);

            Transform view = ViewOf(mover);
            for (int step = 0; step < LastRoll; step++)
            {
                mover.Tile = Board.Advance(mover.Tile, 1, _board.TileCount);
                yield return HopTo(view, PositionOn(mover));
                StartCoroutine(Squash(view));

                if (mover.Tile == other.HomeTile)
                {
                    other.BaseHp = Mathf.Max(0, other.BaseHp - mover.Attack);
                    Transform castle = CastleOf(other);
                    _fx.Popup(castle.position + Vector3.up * 1.6f, $"-{mover.Attack} BASE", BaseHitColor);
                    _fx.Burst(castle.position + Vector3.up * 0.8f, BaseHitColor);
                    Log($"{NameOf(mover)} hit {NameOf(other)}'s base: <color=#{Hex(BaseHitColor)}>-{mover.Attack}</color> ({other.BaseHp} left)");
                    UpdateBadges();
                    if (other.BaseHp == 0)
                    {
                        EndGame(mover == Player);
                        yield break;
                    }
                }
                else if (mover.Tile == mover.HomeTile && mover.Hp < mover.MaxHp)
                {
                    mover.Heal(TileRules.PassHomeHeal);
                    _fx.Popup(view.position + Vector3.up, $"+{TileRules.PassHomeHeal} HP", GainColor);
                    Log($"{NameOf(mover)} passed home: <color=#{Hex(GainColor)}>+{TileRules.PassHomeHeal} HP</color>");
                    UpdateBadges();
                }
            }

            ResolveLanding(mover, other);
            UpdateBadges();
            yield return new WaitForSeconds(_messagePause);
        }

        private void ResolveLanding(Fighter mover, Fighter other)
        {
            if (mover.Tile == other.Tile)
            {
                other.TakeDamage(mover.Attack);
                Transform target = ViewOf(other);
                _fx.Popup(target.position + Vector3.up, $"-{mover.Attack} HP", LossColor);
                _fx.Burst(target.position, LossColor);
                Log($"{NameOf(mover)} attacked {NameOf(other)}: <color=#{Hex(LossColor)}>-{mover.Attack} HP</color>");
                if (other.IsKnockedOut) KnockOut(other);
                return;
            }

            TileEffect effect = _effects.GetEffect(mover.Tile);
            EffectResult result = TileRules.Apply(mover, effect);
            if (!result.HasEffect) return;

            Color color = ChangeColor(result);
            Transform view = ViewOf(mover);
            _fx.Popup(view.position + Vector3.up, result.Change, color);
            _fx.Burst(_board.GetTilePosition(mover.Tile) + Vector3.up * 0.4f, color);
            Log($"{NameOf(mover)} {result.Description}: <color=#{Hex(color)}>{result.Change}</color>");

            if (result.RemoveEffect)
            {
                _effects.MoveElsewhere(mover.Tile, Player.Tile, Rival.Tile);
            }
            if (mover.IsKnockedOut) KnockOut(mover);
        }

        private void KnockOut(Fighter fighter)
        {
            fighter.Respawn();
            PlaceView(fighter);
            _fx.Popup(ViewOf(fighter).position + Vector3.up, "KNOCKED OUT", LossColor);
            Log($"{NameOf(fighter)} was <color=#{Hex(LossColor)}>knocked out</color> and returns home with full HP");
        }

        private void EndGame(bool playerWon)
        {
            IsGameOver = true;
            Say(playerWon ? "YOU WIN! The rival base is destroyed." : "YOU LOSE! Your base is destroyed.");
            Log(playerWon
                ? $"<b><color=#{Hex(AttackColor)}>{Player.Name} wins!</color></b> The {Rival.Name} base has fallen."
                : $"<b><color=#{Hex(LossColor)}>{Rival.Name} wins!</color></b> Your base has fallen.");
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

        /// <summary>A quick squash on landing, then back to normal (runs alongside the next hop).</summary>
        private static IEnumerator Squash(Transform view)
        {
            const float duration = 0.15f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float squash = Mathf.Sin(t * Mathf.PI) * 0.18f;
                view.localScale = new Vector3(1f + squash, 1f - squash, 1f + squash);
                yield return null;
            }
            view.localScale = Vector3.one;
        }

        private Transform ViewOf(Fighter fighter)
        {
            return fighter == Player ? _heroView : _rivalView;
        }

        private Transform CastleOf(Fighter fighter)
        {
            return fighter == Player ? _playerCastle : _rivalCastle;
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

        private void UpdateBadges()
        {
            _playerBadge.Show(Player, PlayerColor);
            _rivalBadge.Show(Rival, RivalColor);
        }

        private void Say(string message)
        {
            _promptText.text = message;
        }

        private void Log(string line)
        {
            _eventLog.Add(line);
        }

        private string NameOf(Fighter fighter)
        {
            return $"<b><color=#{Hex(fighter == Player ? PlayerColor : RivalColor)}>{fighter.Name}</color></b>";
        }

        private static string CardTag(DiceCard card)
        {
            string label = card.Type == DiceType.Fixed ? $"FIXED {card.FixedValue}" : CardArt.Title(card.Type);
            return $"<b><color=#{Hex(CardArt.AccentColor(card.Type))}>{label}</color></b>";
        }

        private static Color ChangeColor(EffectResult result)
        {
            if (!result.Good) return result.Change.Contains("SKIP") ? SkipColor : LossColor;
            return result.Change.Contains("ATK") ? AttackColor : GainColor;
        }

        private static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
