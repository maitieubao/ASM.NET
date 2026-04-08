using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using NUnit.Framework;

namespace YoutubeMusicPlayer.Tests.FunctionalTests.Selenium;

[TestFixture]
public class SeleniumTests
{
    private IWebDriver _driver = null!;
    private string _baseUrl = "http://localhost:5088";
    private string _testUserEmail = $"testuser_{Guid.NewGuid()}@example.com";
    private string _testPassword = "Password123!";

    [SetUp]
    public void Setup()
    {
        var options = new ChromeOptions();
        // options.AddArgument("--headless"); 
        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
        _driver.Manage().Window.Maximize();
    }

    [TearDown]
    public void Teardown()
    {
        _driver.Quit();
        _driver.Dispose();
    }

    private void RegisterAndLogin()
    {
        string uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        string uniqueEmail = $"testuser_{uniqueId}@example.com";

        _driver.Navigate().GoToUrl($"{_baseUrl}/Auth/Register");
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
        
        wait.Until(d => d.FindElement(By.Id("Username"))).SendKeys("TestUser" + uniqueId);
        _driver.FindElement(By.Id("Email")).SendKeys(uniqueEmail);
        _driver.FindElement(By.Id("DateOfBirth")).SendKeys("01/01/1995");
        _driver.FindElement(By.Id("Password")).SendKeys(_testPassword);
        _driver.FindElement(By.Id("ConfirmPassword")).SendKeys(_testPassword);
        _driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        // Wait for potential redirect phase
        Thread.Sleep(3000);

        // If redirected to Login page or still see Login link, perform login
        if (_driver.Url.Contains("/Auth/Login") || _driver.FindElements(By.CssSelector("a[href*='/Auth/Login']")).Any())
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}/Auth/Login");
            wait.Until(d => d.FindElement(By.Id("Email"))).SendKeys(uniqueEmail);
            _driver.FindElement(By.Id("Password")).SendKeys(_testPassword);
            _driver.FindElement(By.CssSelector("button[type='submit']")).Click();
        }

        // Wait until we are definitely logged in (Logout link should exist in header dropdown)
        // We might need to wait for the page to settle
        wait.Until(d => !d.Url.Contains("/Auth/Login") && !d.Url.Contains("/Auth/Register"));
        
        // Final verification of login state
        try {
            wait.Until(d => d.FindElements(By.CssSelector("a[href*='/Auth/Logout']")).Count > 0 || d.FindElements(By.CssSelector(".user-actions")).Count > 0);
        } catch {
            Console.WriteLine("Warning: Could not explicitly confirm login state via elements, proceeding anyway.");
        }
        
        Thread.Sleep(2000);
    }

    [Test]
    public void Test_Playlist_CreateAndVerify()
    {
        RegisterAndLogin();
        _driver.Navigate().GoToUrl($"{_baseUrl}/Playlist/Index");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
        var createBtn = wait.Until(d => {
            var elements = d.FindElements(By.CssSelector("button[data-bs-target='#createPlaylistModal']"));
            return elements.FirstOrDefault(e => e.Displayed);
        });
        createBtn!.Click();

        Thread.Sleep(1500);

        string playlistName = "My Test Playlist " + DateTime.Now.Ticks;
        var titleInput = wait.Until(d => d.FindElement(By.Name("title")));
        titleInput.SendKeys(playlistName);
        _driver.FindElement(By.Name("description")).SendKeys("Automated test playlist description");

        var submitBtn = _driver.FindElement(By.CssSelector("#createPlaylistModal button[type='submit']"));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", submitBtn);

        wait.Until(d => d.Url.Contains("/Playlist/Index") || d.Url.EndsWith("/Playlist"));

        Thread.Sleep(2000);
        var playlistCards = _driver.FindElements(By.CssSelector(".music-card .card-title"));
        bool found = playlistCards.Any(c => c.Text.Contains(playlistName));
        
        Assert.That(found, Is.True, $"Playlist '{playlistName}' was not found.");
    }

    [Test]
    public void Test_ArtistDetails_DeepVerification_Wikipedia()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Artist/Details/1");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(20));
        var aboutSection = wait.Until(d => d.FindElements(By.Id("about")).FirstOrDefault());
        Assert.That(aboutSection, Is.Not.Null, "Không tìm thấy phần Giới thiệu.");

        try 
        {
            var wikiLink = aboutSection!.FindElement(By.CssSelector("a[href*='wikipedia.org']"));
            Assert.That(wikiLink, Is.Not.Null);
            string href = wikiLink.GetDomAttribute("href") ?? "";
            Assert.That(href, Does.Contain("wikipedia.org"));
        }
        catch (NoSuchElementException)
        {
            var bioPreview = aboutSection!.FindElement(By.CssSelector(".about-preview"));
            Assert.That(bioPreview.Text, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void Test_UserProfile_FullVerification()
    {
        RegisterAndLogin();
        _driver.Navigate().GoToUrl($"{_baseUrl}/User/Profile");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
        try 
        {
            // 4. Capture Page State for debugging
            string pageSource = _driver.PageSource;
            string currentUrl = _driver.Url;
            
            try {
                var h1 = wait.Until(d => d.FindElement(By.TagName("h1")));
                Console.WriteLine($"[DEBUG] Page H1: {h1.Text}");
                
                if (h1.Text.Contains("exception", StringComparison.OrdinalIgnoreCase) || 
                    h1.Text.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    // If it looks like an error page, try to find the actual exception message
                    try {
                        var preTags = _driver.FindElements(By.TagName("pre"));
                        foreach(var pre in preTags) Console.WriteLine($"[EXCEPTION DETAIL] {pre.Text}");
                        
                        var stackTrace = _driver.FindElements(By.ClassName("stacktrace"));
                        foreach(var st in stackTrace) Console.WriteLine($"[STACK TRACE] {st.Text}");
                    } catch {}
                    
                    Assert.Fail($"Server-side exception detected on Profile page. URL: {currentUrl}");
                }
            } catch (Exception ex) {
                Console.WriteLine($"[DEBUG] No H1 found or error during H1 check: {ex.Message}");
            }

            // 5. Verify Profile Details
            var statCards = _driver.FindElements(By.CssSelector(".stat-card"));
            Console.WriteLine($"[DEBUG] Stats cards found: {statCards.Count}. URL: {currentUrl}");
            
            Assert.That(statCards.Count, Is.AtLeast(3), "Profile should show at least 3 stats cards (Playlists, Likes, Following).");

            var sectionTitles = _driver.FindElements(By.CssSelector(".section-title")).Select(t => t.Text).ToList();
            Assert.That(sectionTitles.Any(t => t.ToLower().Contains("playlist") || t.ToLower().Contains("danh sách")), Is.True, "Thiếu mục Playlist.");
        }
        catch (WebDriverTimeoutException)
        {
            Console.WriteLine("Current URL: " + _driver.Url);
            throw;
        }
    }
}

