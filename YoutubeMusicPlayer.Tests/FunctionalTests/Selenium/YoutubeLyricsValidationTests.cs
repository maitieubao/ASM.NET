using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using NUnit.Framework;
using System;
using System.Linq;
using System.Collections.Generic;

namespace YoutubeMusicPlayer.Tests.FunctionalTests.Selenium
{
    [TestFixture]
    public class YoutubeLyricsValidationTests
    {
        private IWebDriver _driver;
        private readonly string _baseUrl = "http://localhost:5088";
        private WebDriverWait _wait;

        [SetUp]
        public void Setup()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless"); 
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            
            _driver = new ChromeDriver(options);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60));
        }

        [TestCase("Someone Like You Adele")]
        [TestCase("Lạc Trôi Sơn Tùng M-TP")]
        public void Verify_Lyrics_Fetched_And_Displayed_Successfully(string searchQuery)
        {
            try
            {
                TestContext.WriteLine($"=== STARTING LYRICS VALIDATION: {searchQuery} ===");
                _driver.Navigate().GoToUrl(_baseUrl);

                // 1. Search for the song
                var searchInput = _wait.Until(ExpectedConditions.ElementIsVisible(By.Id("globalSearchInput")));
                searchInput.Clear();
                searchInput.SendKeys(searchQuery);
                searchInput.SendKeys(Keys.Enter);
                TestContext.WriteLine("Step 1: Search query submitted.");

                // 2. Click play on first result
                var firstPlayBtn = _wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(".song-card i.fa-play, .song-list-play-overlay i")));
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", firstPlayBtn);
                TestContext.WriteLine("Step 2: Song playback initiated.");

                // 3. Open lyrics overlay
                var lyricsBtn = _wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector("button[title='Lời bài hát & Nghệ sĩ']")));
                lyricsBtn.Click();
                TestContext.WriteLine("Step 3: Lyrics overlay opened.");

                // 4. Verify Loading State
                var container = _wait.Until(ExpectedConditions.ElementIsVisible(By.Id("lyricsContent")));
                Assert.That(container.Displayed, Is.True, "Lyrics container should be visible.");
                
                // 5. Assert Lyrics Content (Wait for retrieval)
                // We wait for .lyrics-line elements which indicate successful parsing and rendering
                var lyricsLines = _wait.Until(d => {
                    var lines = d.FindElements(By.CssSelector(".lyrics-line"));
                    return lines.Count > 0 ? lines : null;
                });

                Assert.That(lyricsLines.Count, Is.GreaterThan(0), "Lyrics rendered but no lines found.");
                
                var firstLineText = lyricsLines[0].Text;
                Assert.That(string.IsNullOrWhiteSpace(firstLineText), Is.False, "First lyrics line is empty.");
                
                TestContext.WriteLine("SUCCESS: API call returned lyrics data.");
                TestContext.WriteLine("SUCCESS: UI rendered lyrics lines successfully.");
                TestContext.WriteLine($"Sample Lyrics: {firstLineText}");

                TestContext.WriteLine($"=== COMPLETED LYRICS VALIDATION: {searchQuery} ===");
            }
            catch (Exception ex)
            {
                TakeScreenshot(searchQuery);
                TestContext.WriteLine($"FAILURE: Test failed for {searchQuery}. Reason: {ex.Message}");
                throw;
            }
        }

        private void TakeScreenshot(string name)
        {
            var ss = ((ITakesScreenshot)_driver).GetScreenshot();
            string path = System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, $"LyricsTest_{name}_{DateTime.Now:HHmmss}.png");
            ss.SaveAsFile(path);
            TestContext.WriteLine($"Screenshot saved to: {path}");
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
    }
}
