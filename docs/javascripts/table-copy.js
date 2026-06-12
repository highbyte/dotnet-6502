/*
 * Adds a copy-to-clipboard button next to inline-code commands in table cells.
 *
 * MkDocs Material's built-in `content.code.copy` only adds copy buttons to fenced
 * code blocks, not to inline `code` spans inside tables. This enhances the
 * package-manager command tables (e.g. host-apps/installation.md) by adding a
 * small copy button to every command cell.
 *
 * Scope: only columns whose header is exactly "Install", "Update" or "Remove",
 * so it targets the install/update/remove command tables and leaves other tables
 * (Available applications, manual-download file lists, etc.) untouched.
 */
(function () {
  var TARGET_HEADERS = ["install", "update", "remove"];

  function enhance() {
    document.querySelectorAll(".md-typeset table").forEach(function (table) {
      // Find the column indices whose header matches a target name.
      var cols = [];
      table.querySelectorAll("thead th").forEach(function (th, i) {
        if (TARGET_HEADERS.indexOf(th.textContent.trim().toLowerCase()) !== -1) {
          cols.push(i);
        }
      });
      if (cols.length === 0) return;

      table.querySelectorAll("tbody tr").forEach(function (tr) {
        cols.forEach(function (i) {
          var cell = tr.children[i];
          if (!cell) return;
          var code = cell.querySelector("code");
          if (!code || cell.querySelector(".cmd-copy")) return;

          var btn = document.createElement("button");
          btn.className = "cmd-copy";
          btn.type = "button";
          btn.title = "Copy to clipboard";
          btn.setAttribute("aria-label", "Copy command to clipboard");
          btn.addEventListener("click", function () {
            navigator.clipboard.writeText(code.textContent).then(function () {
              btn.classList.add("cmd-copied");
              btn.title = "Copied!";
              setTimeout(function () {
                btn.classList.remove("cmd-copied");
                btn.title = "Copy to clipboard";
              }, 1500);
            });
          });
          cell.appendChild(btn);
        });
      });
    });
  }

  // `document$` is provided by Material and emits on initial load and after each
  // instant navigation; fall back to DOMContentLoaded if it is unavailable.
  if (typeof document$ !== "undefined") {
    document$.subscribe(enhance);
  } else {
    document.addEventListener("DOMContentLoaded", enhance);
  }
})();
