using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RollOfLegends.Tests
{
    /// <summary>Plays the Main scene the way a player would: press PLAY, then play cards against the rival.</summary>
    public class PrototypeSceneTests
    {
        private const float TestTimeScale = 6f; // speeds up the dice, hops and message pauses

        private GameController _controller;
        private Board _board;
        private BoardEffects _effects;
        private DiceView _dice;
        private MoveHighlighter _highlighter;
        private EventLog _log;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            Time.timeScale = TestTimeScale;
            SceneManager.LoadScene("Main");
            yield return null; // scene loads
            yield return null; // Start() runs

            _controller = Object.FindFirstObjectByType<GameController>();
            _board = Object.FindFirstObjectByType<Board>();
            _effects = Object.FindFirstObjectByType<BoardEffects>();
            _dice = Object.FindFirstObjectByType<DiceView>(FindObjectsInactive.Include);
            _highlighter = Object.FindFirstObjectByType<MoveHighlighter>();
            _log = Object.FindFirstObjectByType<EventLog>();
        }

        [TearDown]
        public void ResetTime()
        {
            Time.timeScale = 1f;
        }

        // ---------- Board ----------

        [Test]
        public void Board_IsClosedSquareLoopWithEightTilesPerSide()
        {
            Assert.AreEqual(28, _board.TileCount);

            float step = Vector3.Distance(_board.GetTilePosition(0), _board.GetTilePosition(1));
            for (int i = 0; i < _board.TileCount; i++)
            {
                Vector3 current = _board.GetTilePosition(i);
                Vector3 next = _board.GetTilePosition((i + 1) % _board.TileCount);
                Assert.AreEqual(step, Vector3.Distance(current, next), 0.001f, $"Tile {i} is not next to the following tile");
            }
        }

        [Test]
        public void Board_GoesClockwiseFromBottomLeftStart()
        {
            Vector3 start = _board.GetTilePosition(0);
            float signedArea = 0f;
            for (int i = 0; i < _board.TileCount; i++)
            {
                Vector3 tile = _board.GetTilePosition(i);
                Vector3 next = _board.GetTilePosition((i + 1) % _board.TileCount);
                Assert.GreaterOrEqual(tile.x, start.x - 0.001f, "START should be on the left side");
                Assert.GreaterOrEqual(tile.z, start.z - 0.001f, "START should be on the bottom (camera) side");
                signedArea += tile.x * next.z - next.x * tile.z;
            }

            // Seen from above (x to the right, z away from the camera), a negative area means clockwise.
            Assert.Less(signedArea, 0f, "Tiles should go clockwise");
        }

        // ---------- Start of a game ----------

        [Test]
        public void BeforePlay_OnlyPlayButtonIsShown()
        {
            Assert.IsNull(GameObject.Find("Hero"), "Hero should be hidden until PLAY");
            Assert.IsNull(GameObject.Find("Rival"), "Rival should be hidden until PLAY");
            Assert.IsNull(GameObject.Find("Dice"), "Dice should be hidden until the first roll");
            Assert.IsNotNull(GameObject.Find("PlayButton"));
            Assert.IsNull(GameObject.Find("Card_0"), "Cards are dealt on PLAY");
        }

        [Test]
        public void PlayButton_SetsUpBothSidesCardsAndEffects()
        {
            Click("PlayButton");

            Assert.AreEqual(0, _controller.Player.Tile);
            Assert.AreEqual(14, _controller.Rival.Tile, "Rival starts on the corner diagonally opposite START");
            AssertViewOn("Hero", 0);
            AssertViewOn("Rival", 14);
            Assert.IsNull(GameObject.Find("PlayButton"));
            Assert.AreEqual(3, _controller.Hand.Count);
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.IsNotNull(GameObject.Find($"Card_{slot}"), $"Card {slot} should be shown");
            }

            Assert.AreEqual(2, _effects.Count(TileEffect.Sword));
            Assert.AreEqual(2, _effects.Count(TileEffect.HealthPotion));
            Assert.AreEqual(2, _effects.Count(TileEffect.RepairElixir));
            Assert.AreEqual(1, _effects.Count(TileEffect.Shield));
            Assert.AreEqual(2, _effects.Count(TileEffect.SpellBook));
            Assert.AreEqual(1, _effects.Count(TileEffect.Chest));
            Assert.AreEqual(2, _effects.Count(TileEffect.Forest));
            Assert.AreEqual(2, _effects.Count(TileEffect.Spikes));
            Assert.AreEqual(1, _effects.Count(TileEffect.Snare));
            Assert.AreEqual(3, _effects.Count(TileEffect.Monster));
            Assert.AreEqual(TileEffect.None, _effects.GetEffect(0), "Bases stay free of effects");
            Assert.AreEqual(TileEffect.None, _effects.GetEffect(14), "Bases stay free of effects");
            StringAssert.Contains("new match", _log.Entries[0]);
        }

        // ---------- Choosing a card ----------

        [Test]
        public void SelectingACard_HighlightsLandingTilesWithoutMoving()
        {
            StartWithEmptyBoard();
            _controller.GiveCard(0, new DiceCard(DiceType.Odd, 0));
            _controller.GiveCard(1, new DiceCard(DiceType.Random, 0));

            Click("Card_0");
            Assert.AreEqual(0, _controller.SelectedSlot);
            CollectionAssert.AreEqual(new[] { 1, 3, 5 }, _highlighter.ActiveTiles, "ODD can land 1, 3 or 5 tiles ahead");
            Assert.IsFalse(_controller.IsBusy, "Selecting must not roll yet");
            Assert.AreEqual(0, _controller.Player.Tile);
            StringAssert.Contains("again to roll", _controller.Message);

            Click("Card_1");
            Assert.AreEqual(1, _controller.SelectedSlot, "Tapping another card switches the selection");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, _highlighter.ActiveTiles);
            Assert.AreEqual(0, _controller.Rival.TurnsTaken);
        }

        // ---------- Turns ----------

        [UnityTest]
        public IEnumerator PlayingACard_MovesHeroThenRivalTakesItsTurn()
        {
            StartWithEmptyBoard();
            GameObject hero = GameObject.Find("Hero");
            float highestHeroY = hero.transform.position.y;

            _controller.GiveCard(0, new DiceCard(DiceType.Fixed, 4));
            Click("Card_0");
            CollectionAssert.AreEqual(new[] { 4 }, _highlighter.ActiveTiles, "A FIXED 4 card can only land 4 ahead");
            Click("Card_0"); // second tap rolls
            Assert.IsTrue(_controller.IsBusy);
            Assert.AreEqual(0, _highlighter.ActiveTiles.Count, "Highlights disappear once you roll");
            Assert.IsNull(GameObject.Find("Card_0"), "The played card should leave the hand");
            Assert.IsFalse(CardButton(1).interactable, "Cards should be locked while moving");

            while (_controller.IsBusy)
            {
                highestHeroY = Mathf.Max(highestHeroY, hero.transform.position.y);
                yield return null;
            }

            Assert.AreEqual(4, _controller.Player.Tile);
            AssertViewOn("Hero", 4);
            Assert.Greater(highestHeroY, 1.0f, "Hero should hop, not slide or teleport");

            Assert.AreEqual(1, _controller.Rival.TurnsTaken, "Rival should play once after you");
            Assert.AreNotEqual(14, _controller.Rival.Tile, "Rival should have moved");
            AssertViewOn("Rival", _controller.Rival.Tile);
            Assert.AreEqual(_controller.LastRoll, _dice.TopValue(), "The die shows the last roll");

            Assert.IsNotNull(GameObject.Find("Card_0"), "A new card fills the empty slot");
            Assert.IsTrue(CardButton(1).interactable, "Cards unlock on your turn");
            StringAssert.Contains("Your turn", _controller.Message);
        }

        [UnityTest]
        public IEnumerator PlayingCards_IgnoresExtraTapsWhileBusy()
        {
            StartWithEmptyBoard();
            _controller.GiveCard(0, new DiceCard(DiceType.Fixed, 2));
            DiceCard secondCard = _controller.Hand[1];

            Click("Card_0");
            Click("Card_0");
            CardButton(1).onClick.Invoke(); // taps while busy
            CardButton(1).onClick.Invoke();
            yield return WaitUntilIdle();

            Assert.AreEqual(2, _controller.Player.Tile);
            Assert.AreEqual(secondCard, _controller.Hand[1], "The second card should still be in the hand");
            Assert.AreEqual(-1, _controller.SelectedSlot);
        }

        // ---------- Tile effects ----------

        [UnityTest]
        public IEnumerator LandingOnSword_RaisesAttackAndSwordMovesElsewhere()
        {
            StartWithEmptyBoard();
            _effects.SetEffect(3, TileEffect.Sword);

            yield return PlayFixed(3);

            Assert.AreEqual(3, _controller.Player.Attack);
            Assert.AreEqual(TileEffect.None, _effects.GetEffect(3));
            Assert.AreEqual(1, _effects.Count(TileEffect.Sword), "The sword reappears on another tile");
        }

        [UnityTest]
        public IEnumerator HistoryLog_RecordsWhoDidWhat()
        {
            StartWithEmptyBoard();
            _effects.SetEffect(3, TileEffect.Sword);

            yield return PlayFixed(3);

            string history = string.Join("\n", _log.Entries);
            StringAssert.Contains("Bahadır", history);
            StringAssert.Contains("FIXED 3", history);
            StringAssert.Contains("found a sword", history);
            StringAssert.Contains("+1 ATK", history);
            StringAssert.Contains("Rival", history, "The rival's turn is logged too");
        }

        [UnityTest]
        public IEnumerator LandingOnSpikes_Hurts()
        {
            StartWithEmptyBoard();
            _effects.SetEffect(3, TileEffect.Spikes);

            yield return PlayFixed(3);

            Assert.AreEqual(17, _controller.Player.Hp);
            Assert.AreEqual(TileEffect.Spikes, _effects.GetEffect(3), "Traps stay on the board");
        }

        [UnityTest]
        public IEnumerator LandingOnSnare_RivalPlaysTwice()
        {
            StartWithEmptyBoard();
            _effects.SetEffect(2, TileEffect.Snare);

            yield return PlayFixed(2);

            Assert.AreEqual(2, _controller.Rival.TurnsTaken, "Your skipped turn goes to the rival");
            Assert.AreEqual(0, _controller.Player.SkipTurns);
            Assert.AreEqual(2, _controller.Player.Tile, "You stayed on the snare");
        }

        [UnityTest]
        public IEnumerator LandingOnMonster_TooWeak_TakesDamageAndMonsterStays()
        {
            StartWithEmptyBoard();
            _effects.SetEffect(2, TileEffect.Monster);

            yield return PlayFixed(2);

            Assert.AreEqual(17, _controller.Player.Hp);
            Assert.AreEqual(TileEffect.Monster, _effects.GetEffect(2));
        }

        [UnityTest]
        public IEnumerator HealthPotion_CanRaiseHpAboveTheStart()
        {
            StartWithEmptyBoard();
            _effects.SetEffect(3, TileEffect.HealthPotion);

            yield return PlayFixed(3);

            Assert.AreEqual(20 + TileRules.PotionHeal, _controller.Player.Hp, "HP has no upper limit");
        }

        [UnityTest]
        public IEnumerator RepairElixir_HealsTheBase()
        {
            StartWithEmptyBoard();
            _controller.Player.BaseHp = 4;
            _effects.SetEffect(3, TileEffect.RepairElixir);

            yield return PlayFixed(3);

            Assert.AreEqual(4 + TileRules.BaseRepair, _controller.Player.BaseHp);
        }

        [UnityTest]
        public IEnumerator Shield_ReducesDamageFromSpikes()
        {
            StartWithEmptyBoard();
            _controller.Player.Defense = 1;
            _effects.SetEffect(3, TileEffect.Spikes);

            yield return PlayFixed(3);

            Assert.AreEqual(20 - (TileRules.SpikeDamage - 1), _controller.Player.Hp);
        }

        [UnityTest]
        public IEnumerator Forest_LowersTheNextRollAndThePreviewShowsIt()
        {
            StartWithEmptyBoard();
            _effects.SetEffect(2, TileEffect.Forest);
            yield return PlayFixed(2);
            Assert.AreEqual(-TileRules.ForestPenalty, _controller.Player.NextRollBonus);

            _controller.GiveCard(0, new DiceCard(DiceType.Fixed, 5));
            Click("Card_0");
            CollectionAssert.AreEqual(new[] { 2 + 5 - TileRules.ForestPenalty }, _highlighter.ActiveTiles, "Preview includes the forest penalty");
            Click("Card_0");
            yield return WaitUntilIdle();

            Assert.AreEqual(2 + 5 - TileRules.ForestPenalty, _controller.Player.Tile);
            Assert.AreEqual(0, _controller.Player.NextRollBonus, "The penalty is used up");
        }

        [UnityTest]
        public IEnumerator Chest_GivesARareDoubleCard()
        {
            StartWithEmptyBoard();
            _effects.SetEffect(3, TileEffect.Chest);

            yield return PlayFixed(3);

            Assert.AreEqual(DiceType.Double, _controller.Hand[0].Type, "The refilled slot holds the rare card");
        }

        [UnityTest]
        public IEnumerator DoubleCard_MovesTheTotalOfTwoDice()
        {
            StartWithEmptyBoard();
            _controller.GiveCard(0, DiceCard.DrawRare());
            Click("Card_0");
            Assert.Greater(_highlighter.ActiveTiles.Count, 6, "DOUBLE can reach 2 to 12 tiles ahead");
            Click("Card_0");

            // Wait for the hero to finish (the rival's turn changes LastRoll afterwards).
            while (_controller.Rival.TurnsTaken == 0 && _controller.IsBusy)
            {
                yield return null;
            }
            int rolled = _controller.Player.Tile;
            yield return WaitUntilIdle();

            Assert.That(rolled, Is.InRange(2, 12));
            StringAssert.Contains("DOUBLE", string.Join("\n", _log.Entries));
        }

        // ---------- Winning ----------

        [UnityTest]
        public IEnumerator PassingRivalBase_CanDestroyItAndWin_ThenPlayAgainResets()
        {
            StartWithEmptyBoard();
            _controller.Rival.BaseHp = 1;
            _controller.Player.Tile = 12;

            yield return PlayFixed(3); // 13, 14 (rival base), 15

            Assert.IsTrue(_controller.IsGameOver);
            StringAssert.Contains("YOU WIN", _controller.Message);
            StringAssert.Contains("wins", _log.Entries[_log.Entries.Count - 1]);
            Assert.AreEqual(0, _controller.Rival.TurnsTaken, "The game ends before the rival plays");
            Assert.IsNull(GameObject.Find("Card_0"), "Cards are hidden after the game ends");

            GameObject playButton = GameObject.Find("PlayButton");
            Assert.IsNotNull(playButton);
            Assert.AreEqual("PLAY AGAIN", playButton.GetComponentInChildren<Text>().text);

            Click("PlayButton");
            Assert.IsFalse(_controller.IsGameOver);
            Assert.AreEqual(10, _controller.Rival.BaseHp);
            Assert.AreEqual(0, _controller.Player.Tile);
        }

        // ---------- Camera ----------

        [TestCase(9f / 19.5f)] // portrait phone
        [TestCase(19.5f / 9f)] // landscape phone
        [TestCase(16f / 9f)]   // typical Editor Game view
        public void Camera_ShowsWholeBoard(float aspect)
        {
            Camera camera = Camera.main;
            camera.aspect = aspect;
            var fit = camera.GetComponent<CameraFit>();
            fit.Fit();

            foreach (Renderer tile in _board.GetComponentsInChildren<Renderer>())
            {
                AssertInView(camera, tile.bounds.min, fit.SafeArea);
                AssertInView(camera, tile.bounds.max, fit.SafeArea);
            }

            camera.ResetAspect();
        }

        // ---------- Helpers ----------

        /// <summary>Starts a game and removes all random effects so moves are predictable.</summary>
        private void StartWithEmptyBoard()
        {
            Click("PlayButton");
            for (int tile = 0; tile < _board.TileCount; tile++)
            {
                _effects.SetEffect(tile, TileEffect.None);
            }
        }

        private IEnumerator PlayFixed(int value)
        {
            _controller.GiveCard(0, new DiceCard(DiceType.Fixed, value));
            Click("Card_0"); // select
            Click("Card_0"); // roll
            yield return WaitUntilIdle();
        }

        private IEnumerator WaitUntilIdle()
        {
            float timeout = Time.realtimeSinceStartup + 30f;
            while (_controller.IsBusy)
            {
                Assert.Less(Time.realtimeSinceStartup, timeout, "The turn never finished");
                yield return null;
            }
        }

        private static void Click(string buttonName)
        {
            GameObject.Find(buttonName).GetComponent<Button>().onClick.Invoke();
        }

        private static Button CardButton(int slot)
        {
            return GameObject.Find($"Card_{slot}").GetComponent<Button>();
        }

        private void AssertViewOn(string name, int tileIndex)
        {
            Transform view = GameObject.Find(name).transform;
            Vector3 tile = _board.GetTilePosition(tileIndex);
            Vector2 offset = new Vector2(view.position.x - tile.x, view.position.z - tile.z);
            Assert.Less(offset.magnitude, 0.35f, $"{name} should stand on tile {tileIndex}");
            Assert.Greater(view.position.y, tile.y);
        }

        private static void AssertInView(Camera camera, Vector3 worldPoint, Rect safeArea)
        {
            // The board must stay inside the part of the screen the UI panels leave free.
            Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
            Assert.That(viewport.x, Is.InRange(safeArea.xMin - 0.001f, safeArea.xMax + 0.001f), $"{worldPoint} is off-screen or under the UI horizontally");
            Assert.That(viewport.y, Is.InRange(safeArea.yMin - 0.001f, safeArea.yMax + 0.001f), $"{worldPoint} is off-screen or under the UI vertically");
            Assert.Greater(viewport.z, 0f, $"{worldPoint} is behind the camera");
        }
    }
}
