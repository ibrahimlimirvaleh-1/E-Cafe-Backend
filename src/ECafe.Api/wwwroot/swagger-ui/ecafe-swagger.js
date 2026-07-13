(function () {
  const storageKey = "ecafe-swagger-theme";
  const logos = {
    light: "/swagger-ui/ecafe-logo-light.png?v=20260714-3",
    dark: "/swagger-ui/ecafe-logo-dark.png?v=20260714-3"
  };

  function getTheme() {
    return localStorage.getItem(storageKey) || "dark";
  }

  function updateLogo(theme) {
    document.querySelectorAll(".ecafe-brand-logo").forEach(function (logo) {
      logo.src = theme === "dark" ? logos.dark : logos.light;
      logo.alt = theme === "dark" ? "ECafe dark logo" : "ECafe light logo";
    });
  }

  function applyTheme(theme) {
    if (!document.body) {
      return;
    }

    document.body.classList.toggle("ecafe-dark", theme === "dark");
    document.body.classList.toggle("ecafe-light", theme === "light");
    updateLogo(theme);
  }

  function setTheme(theme) {
    localStorage.setItem(storageKey, theme);
    applyTheme(theme);
  }

  function addBrand() {
    if (!document.body) {
      return;
    }

    if (document.querySelector(".ecafe-floating-brand")) {
      updateLogo(getTheme());
      return;
    }

    const brand = document.createElement("div");
    brand.className = "ecafe-brand ecafe-floating-brand";

    const logo = document.createElement("img");
    logo.className = "ecafe-brand-logo";
    logo.width = 72;
    logo.height = 72;

    const text = document.createElement("div");
    text.className = "ecafe-brand-copy";
    text.innerHTML = '<strong>ECafe API</strong><span>Restaurant booking platform</span>';

    brand.appendChild(logo);
    brand.appendChild(text);
    document.body.appendChild(brand);
    updateLogo(getTheme());
  }

  function addToolbar() {
    if (!document.body) {
      return;
    }

    if (document.querySelector(".ecafe-toolbar")) {
      return;
    }

    const toolbar = document.createElement("div");
    toolbar.className = "ecafe-toolbar";

    const collapseButton = document.createElement("button");
    collapseButton.type = "button";
    collapseButton.title = "Collapse all opened endpoints";
    collapseButton.setAttribute("aria-label", "Collapse all endpoints");
    collapseButton.textContent = "Collapse all";
    collapseButton.addEventListener("click", function () {
      document.querySelectorAll(".opblock.is-open .opblock-summary").forEach(function (summary) {
        summary.click();
      });
    });

    const themeButton = document.createElement("button");
    themeButton.type = "button";
    themeButton.className = "ecafe-theme-button";
    themeButton.title = "Toggle Swagger light/dark mode";
    themeButton.setAttribute("aria-label", "Toggle Swagger theme");

    function syncThemeText() {
      themeButton.textContent = document.body.classList.contains("ecafe-dark") ? "Light mode" : "Dark mode";
    }

    themeButton.addEventListener("click", function () {
      const nextTheme = document.body.classList.contains("ecafe-dark") ? "light" : "dark";
      setTheme(nextTheme);
      syncThemeText();
    });

    toolbar.appendChild(collapseButton);
    toolbar.appendChild(themeButton);
    document.body.appendChild(toolbar);
    syncThemeText();
  }

  function boot() {
    if (!document.body) {
      return;
    }

    applyTheme(getTheme());
    addBrand();
    addToolbar();
  }

  function start() {
    boot();
    setTimeout(boot, 100);
    setTimeout(boot, 500);
    setTimeout(boot, 1200);

    const observer = new MutationObserver(function () {
      window.requestAnimationFrame(boot);
    });

    observer.observe(document.body, {
      childList: true,
      subtree: true
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", start, { once: true });
  } else {
    start();
  }
})();
