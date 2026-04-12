using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using NUnit.Framework;
using System;
using System.Linq;

namespace YoutubeMusicPlayer.Tests.FunctionalTests.Selenium
{
    [TestFixture]
    public class LyricsTests
    {
        private IWebDriver _driver;
        private readonly string _baseUrl = "http://localhost:5088";
        private WebDriverWait _wait;

        [SetUp]
        public void Setup()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless"); // Run in headless mode for CI/CD
            options.AddArgument("--window-size=1920,1080");
            _driver = new ChromeDriver(options);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(180));
        }

        [TestCase("Adele Someone Like You")]
        [TestCase("Ed Sheeran Shape of You")]
        [TestCase("Son Tung M-TP Lac Troi")]
        public void Test_Lyrics_Display_For_Song(string songQuery)
        {
            try
            {
                _driver.Navigate().GoToUrl(_baseUrl);

                // 1. Search for a specific song
                var searchInput = _wait.Until(ExpectedConditions.ElementIsVisible(By.Id("globalSearchInput")));
                searchInput.Clear();
                TestContext.WriteLine($"Searching for: {songQuery}");
                searchInput.SendKeys(songQuery);
                searchInput.SendKeys(Keys.Enter);

                // 2. Wait for search results and play the first song
                var firstPlayBtn = _wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(".song-list-play-overlay i")));
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", firstPlayBtn);

                // 3. Wait for player to stabilize and show "Lyrics" button
                var lyricsBtn = _wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector("button[title='Lời bài hát & Nghệ sĩ']")));
                
                // Debug: Get current videoId from URL if possible or from state
                string currentUrl = _driver.Url;
                TestContext.WriteLine($"Current URL: {currentUrl}");

                lyricsBtn.Click();

                // 4. Verify Full Player Overlay is open
                var overlay = _wait.Until(ExpectedConditions.ElementIsVisible(By.Id("fullPlayerOverlay")));
                Assert.That(overlay.Displayed, Is.True, "Full player overlay should be visible.");

                // 5. Wait for lyrics (with retry-aware timeout)
                // The frontend has a 4s retry interval, so we wait up to 20s for at least one line to appear
                var lyricsLines = _wait.Until(d => {
                    var lines = d.FindElements(By.CssSelector(".lyrics-line"));
                    return lines.Count > 0 ? lines : null;
                });

                Assert.That(lyricsLines.Count, Is.GreaterThan(0), $"Lyrics for '{songQuery}' should be rendered within the timeout period.");
                
                var firstLine = lyricsLines.First();
                Assert.That(string.IsNullOrWhiteSpace(firstLine.Text), Is.False, "First lyrics line should have text.");
                TestContext.WriteLine($"Song: {songQuery} - First line: {firstLine.Text}");

                // 6. Close Functionality
                var closeBtn = _driver.FindElement(By.CssSelector(".full-player-close"));
                closeBtn.Click();
                _wait.Until(ExpectedConditions.InvisibilityOfElementLocated(By.Id("fullPlayerOverlay")));
            }
            catch (Exception ex)
            {
                SaveFailureState(songQuery, ex);
                throw;
            }
        }

        private void SaveFailureState(string query, Exception ex)
        {
            var screenshot = ((ITakesScreenshot)_driver).GetScreenshot();
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string safeQuery = string.Concat(query.Split(System.IO.Path.GetInvalidFileNameChars()));
            string screenshotPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), $"Fail_{safeQuery}_{timestamp}.png");
            screenshot.SaveAsFile(screenshotPath);
            
            TestContext.WriteLine($"Test failed for '{query}': {ex.Message}");
            TestContext.WriteLine($"Screenshot saved to: {screenshotPath}");
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
    }
}
