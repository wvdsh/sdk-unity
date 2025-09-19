mergeInto(LibraryManager.library, {
  // === Shared helpers to reduce duplication ===
  $WVD_Helpers__deps: ['$AllocUTF8'],
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

  WavedashJS_BindFS: function () {
    if (typeof window.WavedashJS !== "undefined" && window.WavedashJS.engineInstance) {
      window.WavedashJS.engineInstance.FS = FS;
      console.log("[Wavedash] FS binded to WavedashJS.engineInstance");
    } else {
      console.error("[Wavedash] WavedashJS.engineInstance not set");
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
  },

  WavedashJS_UploadLeaderboardScore__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_UploadLeaderboardScore: function (leaderboardIdPtr, score, keepBest, ugcIdPtr, callbackPtr, requestIdPtr) {
    var lbId = UTF8ToString(leaderboardIdPtr);
    var ugcId = UTF8ToString(ugcIdPtr);
    var requestId = UTF8ToString(requestIdPtr);
  
    var cb = __getWasmFunction(callbackPtr);
  
    var keepBestBool = keepBest !== 0;
  
    var args = { leaderboardId: lbId, score: score, keepBest: keepBestBool };
    if (ugcId && ugcId.length > 0) {
      args.ugcId = ugcId;
    }
  
    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.uploadLeaderboardScore) {
          return Promise.reject('WavedashJS.uploadLeaderboardScore not available');
        }
        if (ugcId && ugcId.length > 0) {
          return window.WavedashJS.uploadLeaderboardScore(lbId, score, keepBestBool, ugcId);
        } else {
          return window.WavedashJS.uploadLeaderboardScore(lbId, score, keepBestBool);
        }
      },
      cb,
      requestId,
      args
    );
  },

  WavedashJS_GetMyLeaderboardEntries__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_GetMyLeaderboardEntries: function (leaderboardIdPtr, callbackPtr, requestIdPtr) {
    var lbId = UTF8ToString(leaderboardIdPtr);
    var requestId = UTF8ToString(requestIdPtr);

    var cb = __getWasmFunction(callbackPtr);

    var args = { leaderboardId: lbId };

    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.getMyLeaderboardEntries) {
          return Promise.reject('WavedashJS.getMyLeaderboardEntries not available');
        }
        return window.WavedashJS.getMyLeaderboardEntries(lbId);
      },
      cb,
      requestId,
      args
    );
  },

  WavedashJS_ListLeaderboardEntries__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_ListLeaderboardEntries: function (leaderboardIdPtr, offset, limit, callbackPtr, requestIdPtr) {
    var lbId = UTF8ToString(leaderboardIdPtr);
    var requestId = UTF8ToString(requestIdPtr);

    var cb = __getWasmFunction(callbackPtr);

    var args = { leaderboardId: lbId, offset: offset, limit: limit };

    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.listLeaderboardEntries) {
          return Promise.reject('WavedashJS.listLeaderboardEntries not available');
        }
        return window.WavedashJS.listLeaderboardEntries(lbId, offset, limit);
      },
      cb,
      requestId,
      args
    );
  },

  WavedashJS_ListLeaderboardEntriesAroundUser__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_ListLeaderboardEntriesAroundUser: function (leaderboardIdPtr, countAhead, countBehind, callbackPtr, requestIdPtr) {
    var lbId = UTF8ToString(leaderboardIdPtr);
    var requestId = UTF8ToString(requestIdPtr);
    
    var cb = __getWasmFunction(callbackPtr);

    var args = { leaderboardId: lbId, countAhead: countAhead, countBehind: countBehind };

    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.listLeaderboardEntriesAroundUser) {
          return Promise.reject('WavedashJS.listLeaderboardEntriesAroundUser not available');
        }
        return window.WavedashJS.listLeaderboardEntriesAroundUser(lbId, countAhead, countBehind);
      },
      cb,
      requestId,
      args
    );
  },

  WavedashJS_GetLeaderboardEntryCount: function (leaderboardIdPtr) {
    var lbId = UTF8ToString(leaderboardIdPtr);
  
    if (typeof window !== 'undefined' &&
        window.WavedashJS &&
        typeof window.WavedashJS.getLeaderboardEntryCount === 'function') {
      try {
        var count = window.WavedashJS.getLeaderboardEntryCount(lbId);
        if (typeof count === 'number') {
          return count;
        }
      } catch (e) {
        console.error("getLeaderboardEntryCount failed:", e);
      }
    }
  
    return 0;
  },

  WavedashJS_UploadRemoteFile__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_UploadRemoteFile: function (filePathPtr, uploadToLocationPtr, callbackPtr, requestIdPtr) {
    var filePath = UTF8ToString(filePathPtr);
    var uploadToLocation = UTF8ToString(uploadToLocationPtr);
    var requestId = UTF8ToString(requestIdPtr);

    var cb = __getWasmFunction(callbackPtr);

    var args = { filePath: filePath, uploadToLocation: uploadToLocation };

    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.uploadRemoteFile) {
          return Promise.reject('WavedashJS.uploadRemoteFile not available');
        }
        return window.WavedashJS.uploadRemoteFile(filePath, uploadToLocation);
      },
      cb,
      requestId,
      args
    );
  },

  WavedashJS_DownloadRemoteFile__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_DownloadRemoteFile: function (filePathPtr, downloadToLocationPtr, callbackPtr, requestIdPtr) {
    var filePath = UTF8ToString(filePathPtr);
    var downloadToLocation = UTF8ToString(downloadToLocationPtr);
    var requestId = UTF8ToString(requestIdPtr);

    var cb = __getWasmFunction(callbackPtr);

    var args = { filePath: filePath, downloadToLocation: downloadToLocation };

    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.downloadRemoteFile) {
          return Promise.reject('WavedashJS.downloadRemoteFile not available');
        }
        return window.WavedashJS.downloadRemoteFile(filePath, downloadToLocation);
      },
      cb,
      requestId,
      args
    );
  },

  WavedashJS_DownloadRemoteDirectory__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_DownloadRemoteDirectory: function (pathPtr, callbackPtr, requestIdPtr) {
    var path = UTF8ToString(pathPtr);
    var requestId = UTF8ToString(requestIdPtr);

    var cb = __getWasmFunction(callbackPtr);

    var args = { path: path };

    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.downloadRemoteDirectory) {
          return Promise.reject('WavedashJS.downloadRemoteDirectory not available');
        }
        return window.WavedashJS.downloadRemoteDirectory(path);
      },
      cb,
      requestId,
      args
    );
  },

  WavedashJS_ListRemoteDirectory__deps: ['$WVD_Helpers', '$__getWasmFunction'],
  WavedashJS_ListRemoteDirectory: function (pathPtr, callbackPtr, requestIdPtr) {
    var path = UTF8ToString(pathPtr);
    var requestId = UTF8ToString(requestIdPtr);

    var cb = __getWasmFunction(callbackPtr);

    var args = { path: path };

    WVD_Helpers.run(
      function () {
        if (typeof window === 'undefined' || !window.WavedashJS || !window.WavedashJS.listRemoteDirectory) {
          return Promise.reject('WavedashJS.listRemoteDirectory not available');
        }
        return window.WavedashJS.listRemoteDirectory(path);
      },
      cb,
      requestId,
      args
    );
  },
});
