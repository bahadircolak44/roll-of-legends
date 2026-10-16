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
        private static readonly Color SpellColor = new Color(0.85f, 0.6f, 1f);
        private static readonly Vector3 AboveHead = Vector3.up * 1.5f; // where floating texts start

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
        [SerializeField] private int _baseHp = 10;

        [Header("Movement and pacing")]
        [Tooltip("How far above the tile the heroes' centers stand.")]
        [SerializeField] private float _heroHeight = 0.62f;

        [Tooltip("Sideways offset so both heroes stay visible on the same tile.")]
        [SerializeField] private float _heroSideOffset = 0.3f;

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
        /// <summary>The total rolled last turn (before forest/haste bonuses).</summary>
        public int LastRoll { get; private set; }

        /// <summary>How many tiles the last mover actually moved (roll plus bonuses, at least 1).</summary>
        public int LastSteps { get; private set; }
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
            foreach (DiceType type in new[] { DiceType.Odd, DiceType.Even, DiceType.Fixed, DiceType.Random, DiceType.Double })
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
            ShowLandingPreview(card);
            string bonus = Player.NextRollBonus == 0 ? "" : $" (next roll {Player.NextRollBonus:+#;-#})";
            Say($"Tap {CardArt.Title(card.Type)} again to roll!{bonus}");
        }

        /// <summary>Highlights every tile this card could take you to, and names what is waiting there.</summary>
        private void ShowLandingPreview(DiceCard card)
        {
            var steps = new SortedSet<int>();
            foreach (int roll in Dice.PossibleValues(card.Type, card.FixedValue))
            {
                steps.Add(Mathf.Max(1, roll + Player.NextRollBonus));
            }

            var stepList = new List<int>(steps);
            var notes = new string[stepList.Count];
            for (int i = 0; i < stepList.Count; i++)
            {
                int tile = Board.Advance(Player.Tile, stepList[i], _board.TileCount);
                notes[i] = tile == Rival.Tile ? "Attack!" : TileRules.ShortName(_effects.GetEffect(tile));
            }
            _highlighter.Show(Player.Tile, stepList.ToArray(), CardArt.AccentColor(card.Type), notes);
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
                    yield return TakeTurn(Rival, Player, card);
                    if (IsGameOver) yield break;
                    _rivalHand[slot] = DrawFor(Rival);
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
            int[] dice = card.RollDice();
            LastRoll = 0;

            Say($"{mover.Name} plays {CardArt.Title(card.Type)}...");
            foreach (int die in dice)
            {
                yield return _dice.Roll(die); // a DOUBLE card throws the die twice
                LastRoll += die;
            }
            string rolled = dice.Length > 1 ? $"{string.Join(" + ", dice)} = <b>{LastRoll}</b>" : $"<b>{LastRoll}</b>";
            Log($"{NameOf(mover)} played {CardTag(card)} → rolled {rolled}");

            int bonus = mover.NextRollBonus;
            LastSteps = mover.UseRollBonus(LastRoll);
            if (bonus != 0)
            {
                Color bonusColor = bonus < 0 ? LossColor : GainColor;
                _fx.Popup(ViewOf(mover).position + AboveHead, $"MOVE {LastSteps}", bonusColor);
                Log($"{NameOf(mover)}'s roll changes by <color=#{Hex(bonusColor)}>{bonus:+#;-#}</color>: moves {LastSteps}");
                UpdateBadges();
            }
            yield return new WaitForSeconds(_pauseAfterDice);

            Transform view = ViewOf(mover);
            for (int step = 0; step < LastSteps; step++)
            {
                mover.Tile = Board.Advance(mover.Tile, 1, _board.TileCount);
                yield return HopTo(view, PositionOn(mover));
                StartCoroutine(Squash(view));

                if (mover.Tile == other.HomeTile)
                {
                    int damage = other.DamageBase(mover.Attack);
                    Transform castle = CastleOf(other);
                    _fx.Popup(castle.position + Vector3.up * 2.2f, $"-{damage} BASE", BaseHitColor);
                    _fx.Burst(castle.position + Vector3.up * 1f, BaseHitColor);
                    Log($"{NameOf(mover)} hit {NameOf(other)}'s base: <color=#{Hex(BaseHitColor)}>-{damage}</color> ({other.BaseHp} left)");
                    UpdateBadges();
                    if (other.BaseHp == 0)
                    {
                        EndGame(mover == Player);
                        yield break;
                    }
                }
                else if (mover.Tile == mover.HomeTile)
                {
                    mover.Heal(TileRules.PassHomeHeal);
                    _fx.Popup(view.position + AboveHead, $"+{TileRules.PassHomeHeal} HP", GainColor);
                    Log($"{NameOf(mover)} passed home: <color=#{Hex(GainColor)}>+{TileRules.PassHomeHeal} HP</color>");
                    UpdateBadges();
                }
            }

            ResolveLanding(mover, other);
            UpdateBadges();
            if (other.BaseHp == 0) // e.g. an earthquake spell
            {
                EndGame(mover == Player);
                yield break;
            }
            yield return new WaitForSeconds(_messagePause);
        }

        private void ResolveLanding(Fighter mover, Fighter other)
        {
            if (mover.Tile == other.Tile)
            {
                int damage = other.TakeDamage(mover.Attack);
                Transform target = ViewOf(other);
                _fx.Popup(target.position + AboveHead, $"-{damage} HP", LossColor);
                _fx.Burst(target.position, LossColor);
                Log($"{NameOf(mover)} attacked {NameOf(other)}: <color=#{Hex(LossColor)}>-{damage} HP</color>");
                if (other.IsKnockedOut) KnockOut(other);
                return;
            }

            TileEffect effect = _effects.GetEffect(mover.Tile);
            EffectResult result = TileRules.Apply(mover, other, effect);
            if (!result.HasEffect) return;

            Color color = ChangeColor(result);
            Transform view = ViewOf(mover);
            _fx.Popup(view.position + AboveHead, result.Change, color);
            _fx.Burst(_board.GetTilePosition(mover.Tile) + Vector3.up * 0.4f, color);
            string extra = result.OtherChange != null ? $" (<color=#{Hex(LossColor)}>{result.OtherChange}</color>)" : "";
            Log($"{NameOf(mover)} {result.Description}: <color=#{Hex(color)}>{result.Change}</color>{extra}");

            if (result.OtherChange != null)
            {
                // A spell hit the other side: show it over their hero or their castle.
                Vector3 at = result.OtherChangeOnBase ? CastleOf(other).position + Vector3.up * 2.2f : ViewOf(other).position + AboveHead;
                _fx.Popup(at, result.OtherChange, result.OtherChangeOnBase ? BaseHitColor : LossColor);
                _fx.Burst(at - Vector3.up, LossColor);
                if (other.IsKnockedOut) KnockOut(other);
            }

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
            _fx.Popup(ViewOf(fighter).position + AboveHead, "KNOCKED OUT", LossColor);
            Log($"{NameOf(fighter)} was <color=#{Hex(LossColor)}>knocked out</color> and returns home with {fighter.StartHp} HP");
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
            _hand[slot] = DrawFor(Player);
            _cardSlots[slot].Show(_hand[slot]);
        }

        /// <summary>A random card, or the rare DOUBLE if this side opened a treasure chest.</summary>
        private static DiceCard DrawFor(Fighter fighter)
        {
            if (!fighter.LuckyDraw) return DiceCard.Draw();
            fighter.LuckyDraw = false;
            return DiceCard.DrawRare();
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

        /// <summary>The fighter's spot on its tile: you stand a little to the left, the rival to the right (as seen by the camera).</summary>
        private Vector3 PositionOn(Fighter fighter)
        {
            Camera camera = Camera.main;
            Vector3 right = camera != null ? Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized : Vector3.right;
            float side = fighter == Player ? -_heroSideOffset : _heroSideOffset;
            return _board.GetTilePosition(fighter.Tile) + right * side + Vector3.up * _heroHeight;
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
            if (!result.Good) return result.Change.Contains("SKIP") || result.Change.Contains("ROLL") ? SkipColor : LossColor;
            if (result.Change.Contains("ATK") || result.Change.Contains("DEF")) return AttackColor;
            if (result.Change.Contains("BASE")) return BaseHitColor;
            if (result.Change.Contains("!")) return SpellColor;
            return GainColor;
        }

        private static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
