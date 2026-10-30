for (const command of ["visible", "pick", "element"])
    document.getElementById(command).onclick = async () => {
        const status = document.getElementById("status");
        status.textContent = "Working…";
        try {
            const response = await chrome.runtime.sendMessage({ command });
            status.textContent = response.ok
                ? "Capture sent to SnippyGrab."
                : response.error;
            if (command === "pick" && response.ok) window.close();
        } catch (error) {
            status.textContent = error.message;
        }
    };
