(function bootstrapApiBase() {
  var configured = typeof window.__API_BASE_URL__ === "string" ? window.__API_BASE_URL__.trim() : "";
  var base = configured || window.location.origin || "";
  base = base.replace(/\/+$/, "");

  window.__API_BASE_URL__ = base;
  window.apiUrl = function apiUrl(path) {
    var value = String(path || "");
    if (!value.startsWith("/")) {
      value = "/" + value;
    }

    return base + value;
  };
})();
