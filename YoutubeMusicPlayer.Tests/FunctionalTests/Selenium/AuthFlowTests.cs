using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using NUnit.Framework;
using System;
using System.Threading;

namespace YoutubeMusicPlayer.Tests.FunctionalTests.Selenium;

[TestFixture]
public class AuthFlowTests
{
    private IWebDriver _driver = null!;
    private string _baseUrl = "http://localhost:5088";

    [SetUp]
    public void Setup()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless"); // Chạy ngầm trong nền tĩnh lặng để không popup Chrome làm lag CI (Bạn có thể comment dòng này nếu muốn xem Chrome chạy)
        options.AddArgument("--window-size=1920,1080");
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
    public void SE01_Register_Login_Logout_Flow_Works_Properly()
    {
        string uniqueId = Guid.NewGuid().ToString().Substring(0, 6);
        string uniqueEmail = $"auto_{uniqueId}@test.com";
        string password = "AutoPassword123!";
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

        // 1. Phân cảnh Register
        _driver.Navigate().GoToUrl($"{_baseUrl}/Auth/Register");
        _driver.FindElement(By.Id("Username")).SendKeys("AutoUser" + uniqueId);
        _driver.FindElement(By.Id("Email")).SendKeys(uniqueEmail);

        // Fill Date input reliably using JavaScript
        var dobInput = _driver.FindElement(By.Id("DateOfBirth"));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].value = '2000-01-01';", dobInput);

        _driver.FindElement(By.Id("Password")).SendKeys(password);
        _driver.FindElement(By.Id("ConfirmPassword")).SendKeys(password);
        
        var submitBtn = _driver.FindElement(By.CssSelector("button[type='submit']"));
        submitBtn.Click();

        // Đợi chuyển về trang chủ (hoặc login) sau khi đăng ký
        wait.Until(d => d.Url.Contains("/Auth/Login") || d.Url.Equals($"{_baseUrl}/"));
        
        // 2. Chuyển hướng sang Login (nếu cần)
        if (!_driver.Url.Contains("/Auth/Login"))
        {
            _driver.Navigate().GoToUrl($"{_baseUrl}/Auth/Login");
        }

        // 3. Phân cảnh Login
        _driver.FindElement(By.Id("Email")).SendKeys(uniqueEmail);
        _driver.FindElement(By.Id("Password")).SendKeys(password);
        _driver.FindElement(By.CssSelector("button[type='submit']")).Click();

        // Kiểm tra xem đã log in thành công và về trang chủ chưa
        wait.Until(d => d.Url == $"{_baseUrl}/" || d.Url == $"{_baseUrl}");
        
        // Cần đảm bảo UI đổi sang trạng thái đã login (hiển thị Avatar thay vì nút Đăng nhập)
        var profileAvatar = _driver.FindElement(By.CssSelector(".profile-wrapper"));
        Assert.That(profileAvatar, Is.Not.Null);

        // 4. Phân cảnh Logout (Bấm vào avatar, bấm Logout)
        profileAvatar.Click();
        var logoutBtn = wait.Until(d => d.FindElement(By.CssSelector("a[href='/Auth/Logout']")));
        logoutBtn.Click();

        // Kiểm tra log out thành công (Về trang chủ và thấy nút Login)
        wait.Until(d => d.FindElement(By.CssSelector("a[href='/Auth/Login']")));
        var loginBtn = _driver.FindElement(By.CssSelector("a[href='/Auth/Login']"));
        Assert.That(loginBtn, Is.Not.Null);
    }
}
