mergeInto(LibraryManager.library, {
  // === Shared helpers to reduce duplication ===
  $WVD_Helpers__deps: ['$__getWasmFunction', '$AllocUTF8'],
  $WVD_Helpers: {
    send: function (cb, requestId, responseObj) {
      var json = JSON.stringify({ requestId: requestId, response: responseObj });
      var buf = AllocUTF8(json);
      cb(buf);
      _free(buf);
    },
    run: function (getPromise, cb, requestId, args) {
      var p;
      try {
        p = getPromise();
      } catch (e) {
        return WVD_Helpers.send(cb, requestId, {
          success: false,
          data: null,
          args: args,
          message: String(e)
        });
      }
      p.then(function (resp) {
        WVD_Helpers.send(cb, requestId, resp);
      }).catch(function (err) {
        WVD_Helpers.send(cb, requestId, {
          success: false,
          data: null,
          args: args,
          message: String(err)
        });
      });
    }
  },
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
      var userObj = window.WavedashJS.getUser();
      if (userObj) {
        return AllocUTF8(JSON.stringify(userObj));
      }
    }
    return 0;
  },

  WavedashJS_GetLeaderboard__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_GetLeaderboard: function (leaderboardNamePtr, callbackPtr, requestIdPtr) {
    var lbName = UTF8ToString(leaderboardNamePtr);
    var requestId = UTF8ToString(requestIdPtr);

    var cb = __getWasmFunction(callbackPtr);

    var args = { name: lbName };

    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.getLeaderboard) {
          return Promise.reject('WavedashJS.getLeaderboard not available');
        }
        return window.WavedashJS.getLeaderboard(lbName);
      },
      cb,
      requestId,
      args
    );
  },

  WavedashJS_GetOrCreateLeaderboard__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_GetOrCreateLeaderboard: function (leaderboardNamePtr, sortMethod, displayType, callbackPtr, requestIdPtr) {
    var lbName = UTF8ToString(leaderboardNamePtr);
    var requestId = UTF8ToString(requestIdPtr);

    var cb = __getWasmFunction(callbackPtr);

    var args = { name: lbName, sortOrder: sortMethod, displayType: displayType };

    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.getOrCreateLeaderboard) {
          return Promise.reject('WavedashJS.getOrCreateLeaderboard not available');
        }
        return window.WavedashJS.getOrCreateLeaderboard(lbName, sortMethod, displayType);
      },
      cb,
      requestId,
      args
    );
  }

});
