using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RollOfLegends.Tests
{
    /// <summary>Plays the Main scene the way a player would: press PLAY, then ROLL DICE many times.</summary>
    public class PrototypeSceneTests
    {
        private GameController _controller;
        private Board _board;

        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            SceneManager.LoadScene("Main");
            yield return null; // scene loads
            yield return null; // Start() runs

            _controller = Object.FindFirstObjectByType<GameController>();
            _board = Object.FindFirstObjectByType<Board>();
        }

        [Test]
        public void Board_HasTwelveToSixteenTiles()
        {
            Assert.That(_board.TileCount, Is.InRange(12, 16));
        }

        [Test]
        public void BeforePlay_HeroIsHiddenAndOnlyPlayButtonIsShown()
        {
            Assert.IsNull(GameObject.Find("Hero"), "Hero should be hidden until PLAY");
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

        [Test]
        public void RollDiceButton_MovesHeroByRolledValueAndShowsInfo()
        {
            Click("PlayButton");
            GameObject hero = GameObject.Find("Hero");

            for (int i = 0; i < 50; i++)
            {
                int before = _controller.HeroTileIndex;
                Click("RollDiceButton");

                int expected = (before + _controller.LastRoll) % _board.TileCount;
                Assert.AreEqual(expected, _controller.HeroTileIndex);
                AssertHeroStandsOn(hero, expected);

                string info = InfoText();
                StringAssert.Contains($"Dice: {_controller.LastDiceType}", info);
                StringAssert.Contains($"Rolled: {_controller.LastRoll}", info);
                StringAssert.Contains(expected == 0 ? "Position: START" : $"Position: Tile {expected}", info);
            }
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
