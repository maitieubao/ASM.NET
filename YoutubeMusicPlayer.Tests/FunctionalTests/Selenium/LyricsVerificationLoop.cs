using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;

namespace YoutubeMusicPlayer.Tests.FunctionalTests.Selenium
{
    [TestFixture]
    public class LyricsVerificationLoop
    {
        private IWebDriver _driver;
        private readonly string _baseUrl = "http://localhost:5088";
        private readonly string _logFile = @"C:\Users\maiti\OneDrive\Desktop\ASM.NET\YoutubeMusicPlayer.Application\lyrics_test_results.txt";

        [SetUp]
        public void Setup()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--window-size=1920,1080");
            _driver = new ChromeDriver(options);
        }

        [Test]
        public void Verify_Lyrics_Until_Found()
        {
            string searchQuery = "Adele Rolling in the Deep";
            Log("=== STARTING VERIFICATION LOOP ===");

            _driver.Navigate().GoToUrl(_baseUrl);
            
            // 1. Search
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
            var searchInput = wait.Until(ExpectedConditions.ElementIsVisible(By.Id("globalSearchInput")));
            searchInput.SendKeys(searchQuery + Keys.Enter);
            Log("Step: Search submitted.");

            // 2. Play first result
            var playBtn = wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(".search-result-item:first-child .fa-play, .song-card i.fa-play, .song-list-play-overlay i")));
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", playBtn);
            Log("Step: Playing song...");

            // 3. Open Overlay
            var lyricsBtn = wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector("button[title='Lời bài hát & Nghệ sĩ']")));
            lyricsBtn.Click();
            Log("Step: Overlay opened.");

            // 4. Loop Verification
            bool lyricsFound = false;
            int attempts = 0;
            int maxAttempts = 15;

            while (!lyricsFound && attempts < maxAttempts)
            {
                attempts++;
                Thread.Sleep(3000); // Wait for API/Render

                var lyricsContent = _driver.FindElement(By.Id("lyricsContent"));
                string html = lyricsContent.GetAttribute("innerHTML");

                if (html.Contains("lyrics-line"))
                {
                    lyricsFound = true;
                    var firstLine = _driver.FindElement(By.CssSelector(".lyrics-line")).Text;
                    Log($"Attempt {attempts}: SUCCESS! Lyrics found. Sample: {firstLine}");
                }
                else if (html.Contains("lyrics-skeleton"))
                {
                    Log($"Attempt {attempts}: LOADING...");
                }
                else if (html.Contains("lyrics-not-found"))
                {
                    Log($"Attempt {attempts}: NOT_FOUND. Waiting for background enrichment...");
                }
                else
                {
                    Log($"Attempt {attempts}: UNKNOWN STATE. HTML: " + (html.Length > 50 ? html.Substring(0, 50) : html));
                }
            }

            if (!lyricsFound)
            {
                Log("Verification stopped: Max attempts reached without lyrics.");
                Assert.Fail("Lyrics never appeared.");
            }
            
            Log("=== LOOP COMPLETED SUCCESSFULLY ===");
        }

        private void Log(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string line = $"[{timestamp}] {message}";
                File.AppendAllLines(_logFile, new[] { line });
                TestContext.WriteLine(line);
            }
            catch { }
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
    }
}
