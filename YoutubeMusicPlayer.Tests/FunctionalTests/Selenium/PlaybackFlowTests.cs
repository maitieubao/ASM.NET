using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using NUnit.Framework;
using System;
using System.Threading;

namespace YoutubeMusicPlayer.Tests.FunctionalTests.Selenium;

[TestFixture]
public class PlaybackFlowTests
{
    private IWebDriver _driver = null!;
    private string _baseUrl = "http://localhost:5088";

    [SetUp]
    public void Setup()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless"); 
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--mute-audio"); // Chống ồn khi chạy test
        options.AddArgument("--autoplay-policy=no-user-gesture-required"); // Cho phép audio tự play ở headless
        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
    }

    [TearDown]
    public void Teardown()
    {
        _driver.Quit();
        _driver.Dispose();
    }

    [Test]
    public void SE03_Search_And_Play_Updates_Player_Bar()
    {
        // 1. Vào trang chủ
        _driver.Navigate().GoToUrl(_baseUrl);
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
        
        // 2. Điền từ khóa tìm kiếm
        var searchInput = wait.Until(d => d.FindElement(By.Id("globalSearchInput")));
        searchInput.SendKeys("Lạc Trôi");
        searchInput.SendKeys(Keys.Enter);

        // Đợi kết quả tìm kiếm load (phải có class search-card hoặc thẻ span hiển thị Loading...)
        // Sau đó card xuất hiện
        var resultCard = wait.Until(d => d.FindElement(By.CssSelector(".song-card, .search-result-row, .track-card")));
        
        // 3. Click vào bài đầu tiên
        // Chúng ta có thể dùng JavascriptExecutor để click đảm bảo dính
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", resultCard);
        
        // 4. Kiểm tra thanh Player
        // ID track-title phải thay đổi (không còn rỗng và chứa kí tự)
        Thread.Sleep(5000); // Đợi backend lấy stream Url từ Youtube

        var playerTitle = wait.Until(d => {
            var el = d.FindElement(By.Id("currentTitle"));
            return !string.IsNullOrEmpty(el.Text) ? el : null;
        });

        Assert.That(playerTitle, Is.Not.Null);
        TestContext.WriteLine("Bài hát đang phát: " + playerTitle?.Text);
        
        // Nút Play chuyển thành nút Pause
        var playPauseBtn = _driver.FindElement(By.Id("playPauseBtn"));
        Assert.That(playPauseBtn.GetAttribute("class"), Does.Contain("fa-pause"));
    }
}
