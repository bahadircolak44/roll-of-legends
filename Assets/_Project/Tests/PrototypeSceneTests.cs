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
            Assert.IsNull(GameObject.Find("RollDiceButton"));
        }

        [Test]
        public void PlayButton_PutsHeroOnStartTile()
        {
            Click("PlayButton");

            GameObject hero = GameObject.Find("Hero");
            Assert.IsNotNull(hero, "Hero should appear after PLAY");
            Assert.AreEqual(0, _controller.HeroTileIndex);
            AssertHeroStandsOn(hero, 0);
            Assert.IsNull(GameObject.Find("PlayButton"));
            Assert.IsNotNull(GameObject.Find("RollDiceButton"));
            StringAssert.Contains("Position: START", InfoText());
        }

        [UnityTest]
        public IEnumerator RollDice_DieShowsRolledNumberAndHeroHopsThere()
        {
            Click("PlayButton");
            GameObject hero = GameObject.Find("Hero");
            Button rollButton = GameObject.Find("RollDiceButton").GetComponent<Button>();
            float highestHeroY = hero.transform.position.y;

            for (int i = 0; i < 10; i++)
            {
                int before = _controller.HeroTileIndex;
                rollButton.onClick.Invoke();
                Assert.IsTrue(_controller.IsRolling);
                Assert.IsFalse(rollButton.interactable, "ROLL DICE should be disabled while moving");

                while (_controller.IsRolling)
                {
                    highestHeroY = Mathf.Max(highestHeroY, hero.transform.position.y);
                    yield return null;
                }

                int expected = (before + _controller.LastRoll) % _board.TileCount;
                Assert.AreEqual(expected, _controller.HeroTileIndex);
                AssertHeroStandsOn(hero, expected);
                Assert.AreEqual(_controller.LastRoll, _dice.TopValue(), "The die should land showing the rolled number");
                Assert.IsTrue(rollButton.interactable);

                string info = InfoText();
                StringAssert.Contains($"Dice: {_controller.LastDiceType}", info);
                StringAssert.Contains($"Rolled: {_controller.LastRoll}", info);
                StringAssert.Contains(expected == 0 ? "Position: START" : $"Position: Tile {expected}", info);
            }

            float restingY = _board.GetTilePosition(0).y + 0.5f;
            Assert.Greater(highestHeroY, restingY + 0.2f, "Hero should hop, not slide or teleport");
        }

        [UnityTest]
        public IEnumerator RollDice_IgnoresExtraTapsWhileMoving()
        {
            Click("PlayButton");
            Click("RollDiceButton");
            Click("RollDiceButton"); // second tap while the first roll is still running

            while (_controller.IsRolling)
            {
                yield return null;
            }

            Assert.AreEqual(_controller.LastRoll % _board.TileCount, _controller.HeroTileIndex);
        }

        [TestCase(9f / 19.5f)] // portrait phone
        [TestCase(19.5f / 9f)] // landscape phone
        [TestCase(16f / 9f)]   // typical Editor Game view
        public void Camera_ShowsWholeBoard(float aspect)
        {
            Camera camera = Camera.main;
            camera.aspect = aspect;
            camera.GetComponent<CameraFit>().Fit();

            foreach (Renderer tile in _board.GetComponentsInChildren<Renderer>())
            {
                AssertInView(camera, tile.bounds.min);
                AssertInView(camera, tile.bounds.max);
            }

            camera.ResetAspect();
        }

        private static void Click(string buttonName)
        {
            GameObject.Find(buttonName).GetComponent<Button>().onClick.Invoke();
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

        private static void AssertInView(Camera camera, Vector3 worldPoint)
        {
            Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
            Assert.That(viewport.x, Is.InRange(0f, 1f), $"{worldPoint} is off-screen horizontally");
            Assert.That(viewport.y, Is.InRange(0f, 1f), $"{worldPoint} is off-screen vertically");
            Assert.Greater(viewport.z, 0f, $"{worldPoint} is behind the camera");
        }
    }
}
