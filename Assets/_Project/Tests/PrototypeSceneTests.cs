using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RollOfLegends.Tests
{
    /// <summary>Plays the Main scene the way a player would: press PLAY, then ROLL DICE several times.</summary>
    public class PrototypeSceneTests
    {
        private const float TestTimeScale = 4f; // speeds up the dice and hop animations

        private GameController _controller;
        private Board _board;
        private DiceView _dice;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            Time.timeScale = TestTimeScale;
            SceneManager.LoadScene("Main");
            yield return null; // scene loads
            yield return null; // Start() runs

            _controller = Object.FindFirstObjectByType<GameController>();
            _board = Object.FindFirstObjectByType<Board>();
            _dice = Object.FindFirstObjectByType<DiceView>(FindObjectsInactive.Include);
        }

        [TearDown]
        public void ResetTime()
        {
            Time.timeScale = 1f;
        }

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

        [Test]
        public void BeforePlay_OnlyPlayButtonIsShown()
        {
            Assert.IsNull(GameObject.Find("Hero"), "Hero should be hidden until PLAY");
            Assert.IsNull(GameObject.Find("Dice"), "Dice should be hidden until the first roll");
            Assert.IsNotNull(GameObject.Find("PlayButton"));
            Assert.IsNull(GameObject.Find("Card_0"), "Cards are dealt on PLAY");
            Assert.IsNull(GameObject.Find("RollDiceButton"), "The ROLL DICE button was replaced by cards");
        }

        [Test]
        public void PlayButton_PutsHeroOnStartAndDealsThreeCards()
        {
            Click("PlayButton");

            GameObject hero = GameObject.Find("Hero");
            Assert.IsNotNull(hero, "Hero should appear after PLAY");
            Assert.AreEqual(0, _controller.HeroTileIndex);
            AssertHeroStandsOn(hero, 0);
            Assert.IsNull(GameObject.Find("PlayButton"));
            StringAssert.Contains("Position: START", InfoText());

            Assert.AreEqual(3, _controller.Hand.Count);
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.IsNotNull(GameObject.Find($"Card_{slot}"), $"Card {slot} should be shown");
                Assert.IsNotEmpty(CardLabel(slot));
            }
        }

        [UnityTest]
        public IEnumerator PlayingACard_RollsItsDiceMovesHeroAndDrawsANewCard()
        {
            Click("PlayButton");
            GameObject hero = GameObject.Find("Hero");
            float highestHeroY = hero.transform.position.y;

            for (int i = 0; i < 12; i++)
            {
                int slot = i % 3;
                int before = _controller.HeroTileIndex;
                DiceCard played = _controller.Hand[slot];
                Button otherCard = CardButton((slot + 1) % 3);

                Click($"Card_{slot}");
                Assert.IsTrue(_controller.IsRolling);
                Assert.IsNull(GameObject.Find($"Card_{slot}"), "The played card should leave the hand");
                Assert.IsFalse(otherCard.interactable, "Cards should be disabled while moving");

                while (_controller.IsRolling)
                {
                    highestHeroY = Mathf.Max(highestHeroY, hero.transform.position.y);
                    yield return null;
                }

                AssertRollMatchesCard(played, _controller.LastRoll);
                int expected = (before + _controller.LastRoll) % _board.TileCount;
                Assert.AreEqual(expected, _controller.HeroTileIndex);
                AssertHeroStandsOn(hero, expected);
                Assert.AreEqual(_controller.LastRoll, _dice.TopValue(), "The die should land showing the rolled number");

                Assert.IsNotNull(GameObject.Find($"Card_{slot}"), "A new card should fill the empty slot");
                Assert.AreEqual(3, _controller.Hand.Count);
                Assert.IsTrue(otherCard.interactable);

                string info = InfoText();
                StringAssert.Contains($"Card: {played.Describe()}", info);
                StringAssert.Contains($"Rolled: {_controller.LastRoll}", info);
                StringAssert.Contains(expected == 0 ? "Position: START" : $"Position: Tile {expected}", info);
            }

            float restingY = _board.GetTilePosition(0).y + 0.5f;
            Assert.Greater(highestHeroY, restingY + 0.2f, "Hero should hop, not slide or teleport");
        }

        [UnityTest]
        public IEnumerator PlayingCards_IgnoresExtraTapsWhileMoving()
        {
            Click("PlayButton");
            DiceCard secondCard = _controller.Hand[1];
            Click("Card_0");
            CardButton(1).onClick.Invoke(); // second tap while the first move is still running

            while (_controller.IsRolling)
            {
                yield return null;
            }

            Assert.AreEqual(_controller.LastRoll % _board.TileCount, _controller.HeroTileIndex);
            Assert.AreEqual(secondCard, _controller.Hand[1], "The second card should still be in the hand");
        }

        [TestCase(9f / 19.5f)] // portrait phone
        [TestCase(19.5f / 9f)] // landscape phone
        [TestCase(16f / 9f)]   // typical Editor Game view
        public void Camera_ShowsWholeBoard(float aspect)
        {
            Camera camera = Camera.main;
            camera.aspect = aspect;
            camera.GetComponent<CameraFit>().Fit();

            var fit = camera.GetComponent<CameraFit>();
            foreach (Renderer tile in _board.GetComponentsInChildren<Renderer>())
            {
                AssertInView(camera, tile.bounds.min, fit);
                AssertInView(camera, tile.bounds.max, fit);
            }

            camera.ResetAspect();
        }

        private static void Click(string buttonName)
        {
            GameObject.Find(buttonName).GetComponent<Button>().onClick.Invoke();
        }

        private static Button CardButton(int slot)
        {
            return GameObject.Find($"Card_{slot}").GetComponent<Button>();
        }

        private static string CardLabel(int slot)
        {
            return GameObject.Find($"Card_{slot}").GetComponentInChildren<Text>().text;
        }

        private static void AssertRollMatchesCard(DiceCard card, int roll)
        {
            switch (card.Type)
            {
                case DiceType.Odd: CollectionAssert.Contains(new[] { 1, 3, 5 }, roll); break;
                case DiceType.Even: CollectionAssert.Contains(new[] { 2, 4, 6 }, roll); break;
                case DiceType.Fixed: Assert.AreEqual(card.FixedValue, roll); break;
                default: Assert.That(roll, Is.InRange(1, 6)); break;
            }
        }

        private static string InfoText()
        {
            return GameObject.Find("InfoText").GetComponent<Text>().text;
        }

        private void AssertHeroStandsOn(GameObject hero, int tileIndex)
        {
            Vector3 tile = _board.GetTilePosition(tileIndex);
            Assert.AreEqual(tile.x, hero.transform.position.x, 0.001f);
            Assert.AreEqual(tile.z, hero.transform.position.z, 0.001f);
            Assert.Greater(hero.transform.position.y, tile.y);
        }

        private static void AssertInView(Camera camera, Vector3 worldPoint, CameraFit fit)
        {
            // The board must stay between the info panel (top) and the cards (bottom).
            Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
            Assert.That(viewport.x, Is.InRange(0f, 1f), $"{worldPoint} is off-screen horizontally");
            Assert.That(viewport.y, Is.InRange(fit.BottomMargin, 1f - fit.TopMargin), $"{worldPoint} is under the UI");
            Assert.Greater(viewport.z, 0f, $"{worldPoint} is behind the camera");
        }
    }
}
