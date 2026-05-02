// MkDocs Material fetches /releases/latest (non-pre-releases only).
// If that returns 404, this script falls back to /releases?per_page=1 so
// the source widget still shows the most recent pre-release version.
document.addEventListener("DOMContentLoaded", function () {
  var source = document.querySelector("[data-md-component=\"source\"]");
  if (!source) return;

  var match = source.href.match(/github\.com\/([^/]+\/[^/]+)/);
  if (!match) return;

  var repo = match[1];

  fetch("https://api.github.com/repos/" + repo + "/releases/latest")
    .then(function (r) {
      if (r.ok) return; // Non-pre-release Latest exists; Material handles it.

      // 404: no non-pre-release Latest — fetch newest release including pre-releases.
      return fetch("https://api.github.com/repos/" + repo + "/releases?per_page=1")
        .then(function (r2) { return r2.json(); })
        .then(function (releases) {
          if (!releases || !releases.length || !releases[0].tag_name) return;
          var version = releases[0].tag_name;

          function updateVersion() {
            var facts = source.querySelector(".md-source__facts");
            if (!facts) return false;
            var el = facts.querySelector(".md-source__fact--version");
            if (!el) {
              el = document.createElement("li");
              el.className = "md-source__fact md-source__fact--version";
              facts.prepend(el);
            }
            el.textContent = version;
            return true;
          }

          if (!updateVersion()) {
            var observer = new MutationObserver(function () {
              if (updateVersion()) observer.disconnect();
            });
            observer.observe(source, { childList: true, subtree: true });
          }
        });
    })
    .catch(function () {});
});
