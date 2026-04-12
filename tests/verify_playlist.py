import time
import os
from selenium import webdriver
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.chrome.options import Options
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC

# CONFIG
BASE_URL = "http://localhost:5088"
LOG_FILE = "playlist_test_log.txt"
# Use the found driver
DRIVER_PATH = r"C:\Users\maiti\OneDrive\Desktop\ASM.NET\YoutubeMusicPlayer.Tests\bin\Debug\net10.0\chromedriver.exe"

def log(message):
    timestamp = time.strftime("%Y-%m-%d %H:%M:%S")
    line = f"[{timestamp}] {message}"
    print(line)
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(line + "\n")

def run_test():
    if os.path.exists(LOG_FILE):
        os.remove(LOG_FILE)
    
    log("Starting Playlist Feature Verification Test (using Chrome)...")
    
    options = Options()
    # options.add_argument("--headless") # Run in foreground so user sees it OR keep commented
    options.add_argument("--window-size=1920,1080")
    options.binary_location = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
    
    service = Service(executable_path=DRIVER_PATH)
    driver = webdriver.Chrome(service=service, options=options)
    
    try:
        # 1. Open Site
        log(f"Navigating to {BASE_URL}")
        driver.get(BASE_URL)
        wait = WebDriverWait(driver, 15)
        
        # 2. Search for Adele
        log("Searching for 'Adele'...")
        search_input = wait.until(EC.presence_of_element_id("searchInput"))
        search_input.clear()
        search_input.send_keys("Adele")
        
        search_btn = driver.find_element(By.CSS_SELECTOR, "button[onclick='performSearch()']")
        search_btn.click()
        
        # 3. Verify Search Results & Button Visibility
        log("Waiting for search results...")
        wait.until(EC.presence_of_element_located((By.CLASS_NAME, "search-result-row")))
        
        rows = driver.find_elements(By.CLASS_NAME, "search-result-row")
        log(f"Found {len(rows)} search results.")
        
        first_row = rows[0]
        # Check for our new button
        add_btn = first_row.find_element(By.CSS_SELECTOR, "button[title='Thêm vào Playlist']")
        
        # Check visibility explicitly
        is_visible = add_btn.is_displayed()
        opacity = add_btn.value_of_css_property("opacity") or "1"
        
        log(f"Button 'Thêm vào Playlist' visibility in search list: {is_visible}")
        log(f"Button computed opacity: {opacity}")
        
        if not is_visible:
            log("ERROR: Button is not visible on search result row!")
        
        # 4. Test Opening Modal
        log("Clicking 'Add to Playlist' on the first result...")
        driver.execute_script("arguments[0].click();", add_btn)
        
        log("Waiting for Modal to appear...")
        modal = wait.until(EC.visibility_of_element_id("addToPlaylistModal"))
        log("Modal is visible.")
        
        # 5. Verify Quick Create Feature
        log("Testing Quick Create Playlist...")
        quick_input = driver.find_element(By.ID, "quickPlaylistTitle")
        playlist_name = f"Test Playlist {int(time.time())}"
        quick_input.send_keys(playlist_name)
        
        create_btn = driver.find_element(By.XPATH, "//button[contains(text(), 'Tạo & Thêm')]")
        create_btn.click()
        
        log(f"Waiting for new playlist '{playlist_name}' to appear in list...")
        # Should appear after AJAX re-render
        new_playlist_item = wait.until(EC.presence_of_element_located((By.XPATH, f"//div[contains(text(), '{playlist_name}')]")))
        log("New playlist successfully created and visible in modal.")
        
        # 6. Verify Player Bar Button
        log("Closing modal and playing a song...")
        close_btn = driver.find_element(By.CSS_SELECTOR, "#addToPlaylistModal .btn-close")
        close_btn.click()
        wait.until(EC.invisibility_of_element_id("addToPlaylistModal"))
        
        log("Starting playback of the first song...")
        driver.execute_script("arguments[0].click();", first_row)
        
        log("Waiting for player bar 'Add to Playlist' button...")
        player_add_btn = wait.until(EC.presence_of_element_located((By.CSS_SELECTOR, "button[title='Thêm vào danh sách phát']")))
        
        is_player_btn_visible = player_add_btn.is_displayed()
        log(f"Player bar 'Add to Playlist' button visibility: {is_player_btn_visible}")
        
        if is_player_btn_visible:
            log("SUCCESS: Playlist buttons are visible and functional.")
        else:
            log("ERROR: Player bar button is not visible!")

    except Exception as e:
        log(f"TEST FAILED with error: {str(e)}")
        # Save screenshot for debugging
        driver.save_screenshot("test_error_screenshot_chrome.png")
        log("Screenshot saved as test_error_screenshot_chrome.png")
    finally:
        log("Test script finished.")
        driver.quit()

if __name__ == "__main__":
    run_test()
