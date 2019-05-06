from selenium import webdriver
import sys

# args - tplink_rebooter.py "router address" username password
if len(sys.argv) < 4:
    print("Invalid arguments - router address, username, password")
    sys.exit(1)

if __name__ == "__main__":
    address = sys.argv[1]
    username = sys.argv[2]
    password = sys.argv[3]
    
    driver = webdriver.Chrome()
    # login
    driver.get(address)
    driver.execute_script("document.getElementById('userName').value = '" + username + "';")
    driver.execute_script("document.getElementById('pcPassword').value = '" + password + "';")
    driver.find_element_by_id("loginBtn").click()
    # reboot menu
    driver.switch_to.frame("bottomLeftFrame")
    driver.find_element_by_partial_link_text("System Tools").click()
    driver.find_element_by_partial_link_text("Reboot").click()
    # reboot button
    driver.switch_to.parent_frame()
    driver.switch_to.frame("mainFrame")
    driver.find_element_by_id("reboot").click()
    # confirm popup
    alert = driver.switch_to.alert
    alert.accept()
    print("Rebooting...")
    driver.close()