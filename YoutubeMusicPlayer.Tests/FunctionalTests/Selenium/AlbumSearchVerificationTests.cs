using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace YoutubeMusicPlayer.Tests.FunctionalTests.Selenium
{
    [TestFixture]
    public class AlbumSearchVerificationTests
    {
        private IWebDriver _driver;
        private WebDriverWait _wait;
        private string _baseUrl = "http://localhost:5088";
        private string _logPath = @"c:\Users\maiti\OneDrive\Desktop\ASM.NET\YoutubeMusicPlayer.Application\album_test_results.txt";

        [SetUp]
        public void Setup()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless"); 
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--window-size=1920,1080");
            
            _driver = new ChromeDriver(options);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30)); // Increased from 15s
            File.AppendAllText(_logPath, $"\n--- Album Search Test Started: {DateTime.Now} ---\n");
        }

        [TearDown]
        public void TearDown()
        {
            _driver?.Dispose();
        }

        [Test]
        public void Verify_Album_Search_And_Navigation()
        {
            try
            {
                // 1. Open Home Page
                _driver.Navigate().GoToUrl(_baseUrl);
                Log("Navigated to Home Page.");

                // 2. Perform Search for "Adele 25"
                var searchInput = _wait.Until(d => d.FindElement(By.Id("globalSearchInput")));
                searchInput.Clear();
                searchInput.SendKeys("Adele 25");
                
                var searchBtn = _driver.FindElement(By.Id("globalSearchBtn"));
                searchBtn.Click();
                Log("Searching for 'Adele 25' using Search Button...");

                // 3 & 4. Wait for search results and click an album (extremely robust)
                bool clicked = false;
                Log("Waiting for search results container...");
                _wait.Until(d => d.FindElement(By.Id("search-results-main")).Displayed);
                Log("Search results container is now visible.");
                
                // Wait for the query title to update (ensures search initiated)
                _wait.Until(d => d.FindElement(By.Id("search-query-title")).Text.Contains("Adele 25"));
                Log("Search query title updated.");

                // NEW: Explicitly wait for spinner to disappear (if it exists)
                Log("Waiting for spinner to disappear...");
                try {
                    _wait.Until(d => d.FindElements(By.ClassName("spinner-border")).Count == 0);
                    Log("Spinner cleared.");
                } catch (WebDriverTimeoutException) {
                    Log("Warning: Spinner still present after timeout, proceeding anyway...");
                }

                for (int attempt = 0; attempt < 30; attempt++) // Increased from 15
                {
                    try {
                        var rows = _driver.FindElements(By.ClassName("search-result-row"));
                        if (rows.Count > 0) {
                            Log($"Attempt {attempt}: Found {rows.Count} rows.");
                            
                            var externalAlbum = rows.FirstOrDefault(r => 
                                r.Text.Contains("Deezer") || 
                                r.Text.Contains("iTunes") || 
                                (r.Text.Contains("Album") && r.Text.Contains("•")));
                            
                            if (externalAlbum != null) {
                                Log($"Attempting to click album: {externalAlbum.Text.Split('\n')[0]}");
                                externalAlbum.Click();
                                clicked = true;
                                break;
                            }
                        }

                        // Check for 'No results' message to fail early
                        var noResults = _driver.FindElements(By.Id("no-search-results"));
                        if (noResults.Count > 0 && noResults[0].Displayed) {
                            Log("!!! TEST FAILED: 'No results' message detected.");
                            break;
                        }

                    } catch (StaleElementReferenceException) {
                        Log("Stale element encountered, retrying...");
                    }
                    System.Threading.Thread.Sleep(1000);
                }
                
                if (!clicked) {
                    var htmlDump = _driver.FindElement(By.Id("search-results-content")).GetAttribute("innerHTML");
                    Log($"!!! FAILURE DUMP (Inner HTML): {htmlDump}");
                }
                
                Assert.That(clicked, Is.True, "Could not find or click any album result after multiple attempts.");

                // 5. Verify the External Details Page
                _wait.Until(d => d.Url.Contains("Details"));
                Log($"Current URL: {_driver.Url}");
                
                var h1 = _wait.Until(d => d.FindElement(By.TagName("h1")));
                Log($"Navigated to album page. H1: {h1.Text}");
                
                Assert.That(h1.Text, Is.Not.Null.Or.Empty);
                
                // 6. Verify Tracklist existence and Playback Resolution
                var tracks = _driver.FindElements(By.ClassName("track-row"));
                Log($"Found {tracks.Count} tracks in the album tracklist.");
                Assert.That(tracks.Count, Is.GreaterThan(0), "Album has no tracks displayed.");

                // 7. Click first track and verify resolution (wait for toastr or state change)
                Log("Clicking first track to verify playback resolution...");
                tracks[0].Click();
                
                // Wait for a few seconds to ensure the AJAX call finishes
                System.Threading.Thread.Sleep(3000);
                
                // Check for any error toastrs
                var errors = _driver.FindElements(By.ClassName("toast-warning"));
                if (errors.Count > 0) {
                    Log($"!!! Playback resolution failed with toastr: {errors[0].Text}");
                    Assert.Fail($"Playback resolution failed: {errors[0].Text}");
                }

                Log("--- TEST SUCCESSFUL (Playback Initiated) ---");
            }
            catch (Exception ex)
            {
                Log($"!!! TEST FAILED: {ex.Message}");
                throw;
            }
        }

        private void Log(string message)
        {
            Console.WriteLine(message);
            File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss} - {message}\n");
        }
    }
}
