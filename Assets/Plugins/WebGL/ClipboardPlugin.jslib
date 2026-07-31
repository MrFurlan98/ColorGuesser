// Clipboard access for the WebGL build.
//
// GUIUtility.systemCopyBuffer does nothing in a browser: writing to the clipboard is only
// allowed from JavaScript, and only while the page still holds a user activation. Unity
// handles the click a frame after the browser dispatched it, which is still inside the
// activation window, so a copy started from a button press does go through.
//
// Kept as a single function on purpose. Splitting the fallback out would need an explicit
// __deps entry to stop Emscripten dead-stripping it, and a helper that only exists to be
// called from one place is not worth that.
mergeInto(LibraryManager.library, {

  ColorGuesserCopyToClipboard: function (textPointer) {
    var text = UTF8ToString(textPointer);

    // Off-screen textarea + execCommand. Deprecated, but it is the only path that works
    // without a secure context, so it stays as the backstop.
    var fallback = function () {
      try {
        var area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.top = '-1000px';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        document.execCommand('copy');
        document.body.removeChild(area);
      } catch (e) {
        console.warn('Could not copy to the clipboard: ' + e);
      }
    };

    // navigator.clipboard needs a secure context: fine over HTTPS, absent on plain http.
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(fallback);
      return;
    }

    fallback();
  },
});
