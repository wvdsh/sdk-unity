mergeInto(LibraryManager.library, {
  // === Helpers (JS library variables) ===
  // Define with $Name; call as Name(...) at runtime.
  $__getWasmFunction: function (ptr) {
    if (typeof Module !== "undefined" && Module["wasmTable"]) {
      return Module["wasmTable"].get(ptr);
    }
    if (typeof wasmTable !== "undefined") {
      return wasmTable.get(ptr);
    }
    if (typeof dynCall_vi !== "undefined") {
      return function (arg) { dynCall_vi(ptr, arg); };
    }
    throw new Error("Could not resolve function pointer " + ptr);
  },

  $AllocUTF8: function (str) {
    var size = lengthBytesUTF8(str) + 1;
    var buf = _malloc(size);
    stringToUTF8(str, buf, size);
    return buf;
  },

  // === Exports ===
  WavedashJS_Init: function (configPtr) {
    var configJson = UTF8ToString(configPtr);
    if (typeof window !== 'undefined' &&
        window.WavedashJS &&
        typeof window.WavedashJS.init === 'function') {
      try { window.WavedashJS.init(JSON.parse(configJson)); }
      catch (e) { console.error('Failed to parse WavedashJS config:', e); }
    }
  },

  WavedashJS_IsReady: function () {
    if (typeof window !== 'undefined' &&
        window.WavedashJS &&
        typeof window.WavedashJS.isReady === 'function') {
      return window.WavedashJS.isReady() ? 1 : 0;
    }
    return 0;
  },

  WavedashJS_GetUser__deps: ['$AllocUTF8'],
  WavedashJS_GetUser: function () {
    if (typeof window !== 'undefined' &&
        window.WavedashJS &&
        typeof window.WavedashJS.getUser === 'function') {
      var userJson = window.WavedashJS.getUser();
      if (userJson) return AllocUTF8(userJson);
    }
    return 0;
  },

  WavedashJS_GetOrCreateLeaderboard__deps: ['$__getWasmFunction', '$AllocUTF8'],
  WavedashJS_GetOrCreateLeaderboard: function (leaderboardNamePtr, sortMethod, displayType, callbackPtr, requestIdPtr) {
    var lbName = UTF8ToString(leaderboardNamePtr);
    var requestId = UTF8ToString(requestIdPtr);

    var cb = __getWasmFunction(callbackPtr);

    var args = {
      name: lbName,
      sortOrder: sortMethod,
      displayType: displayType
    };

    function sendResponse(success, data, message) {
      var json = JSON.stringify({
        requestId: requestId,
        success: success,
        data: data,
        args: args,
        message: message || ""
      });
      var buf = AllocUTF8(json);
      cb(buf);
      _free(buf);
    }

    var p = (window.WavedashJS && window.WavedashJS.getOrCreateLeaderboard)
      ? window.WavedashJS.getOrCreateLeaderboard(lbName, sortMethod, displayType)
      : Promise.reject("WavedashJS.getOrCreateLeaderboard not available");

    p.then(function (response) {
        sendResponse(true, response, "");
      })
    .catch(function (err) {
        sendResponse(false, null, String(err));
      });
  }
});
