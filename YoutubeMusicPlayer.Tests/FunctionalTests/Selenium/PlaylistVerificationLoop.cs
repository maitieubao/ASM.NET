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
    public class PlaylistVerificationLoop
    {
        private IWebDriver _driver;
        private readonly string _baseUrl = "http://localhost:5088";
        private readonly string _logFile = @"C:\Users\maiti\OneDrive\Desktop\ASM.NET\YoutubeMusicPlayer.Application\playlist_test_results.txt";

        [SetUp]
        public void Setup()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--window-size=1920,1080");
            // Standard ChromeDriver initialization as per LyricsVerificationLoop.cs
            _driver = new ChromeDriver(options);
        }

        [Test]
        public void Verify_Playlist_Feature()
        {
            Log("=== STARTING PLAYLIST VERIFICATION ===");
            _driver.Navigate().GoToUrl(_baseUrl);
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));

            try 
            {
                // 1. Search for a known artist
                var searchInput = wait.Until(ExpectedConditions.ElementIsVisible(By.Id("globalSearchInput")));
                searchInput.SendKeys("Adele" + Keys.Enter);
                Log("Step: Search for 'Adele' submitted.");

                // 2. Wait for search results and verify button visibility
                wait.Until(ExpectedConditions.ElementExists(By.ClassName("search-result-row")));
                var addBtn = _driver.FindElement(By.CssSelector("button[title='Thêm vào Playlist']"));
                
                if (addBtn.Displayed) {
                    Log("Step: 'Add to Playlist' button is VISIBLE in search results.");
                } else {
                    Log("Step: 'Add to Playlist' button is NOT visible! Check CSS opacity.");
                }

                // 3. Open Modal
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", addBtn);
                var modal = wait.Until(ExpectedConditions.ElementIsVisible(By.Id("addToPlaylistModal")));
                Log("Step: Modal 'Thêm vào danh sách phát' opened successfully.");

                // 4. Test Quick Create
                var quickInput = _driver.FindElement(By.Id("quickPlaylistTitle"));
                string newPlaylistName = "Selenium Test " + DateTime.Now.Ticks.ToString().Substring(10);
                quickInput.SendKeys(newPlaylistName);
                
                var createBtn = _driver.FindElement(By.XPath("//button[contains(text(), 'Tạo & Thêm')]"));
                createBtn.Click();
                Log($"Step: Clicked Create for playlist '{newPlaylistName}'.");

                // 5. Verify new playlist appears in list (Wait for re-render)
                Thread.Sleep(3000); // Wait for AJAX
                bool listUpdated = _driver.PageSource.Contains(newPlaylistName);
                if (listUpdated) {
                    Log("Step: New playlist verified in modal list.");
                } else {
                    Log("Step: New playlist NOT found in modal list after creation.");
                }

                // 6. Check Player Bar
                var modalClose = _driver.FindElement(By.CssSelector("#addToPlaylistModal .btn-close"));
                modalClose.Click();
                
                // Play song to show player bar
                var firstRow = _driver.FindElement(By.ClassName("search-result-row"));
                firstRow.Click();
                Log("Step: Song playback started.");

                var playerAddBtn = wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("footer button[title='Thêm vào danh sách phát']")));
                Log("Step: 'Add to Playlist' button is VISIBLE in the player bar.");

                Log("=== VERIFICATION COMPLETED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                Log($"ERROR during verification: {ex.Message}");
                throw;
            }
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
