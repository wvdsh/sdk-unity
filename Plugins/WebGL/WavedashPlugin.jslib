mergeInto(LibraryManager.library, {
  // Unity -> JavaScript calls
  WavedashJS_Init: function(configPtr) {
    var configJson = UTF8ToString(configPtr);
    
    if (typeof window !== 'undefined' && 
        window.WavedashJS && 
        typeof window.WavedashJS.init === 'function') {
      try {
        var configObject = JSON.parse(configJson);
        window.WavedashJS.init(configObject);
      } catch (e) {
        console.error('Failed to parse WavedashJS config:', e);
      }
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
  
  WavedashJS_GetUser: function () {
    if (typeof window !== 'undefined' && 
        window.WavedashJS && 
        typeof window.WavedashJS.getUser === 'function') {
      var userJson = window.WavedashJS.getUser();
      if (userJson) {
        var bufferSize = lengthBytesUTF8(userJson) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(userJson, buffer, bufferSize);
        return buffer;
      }
    }
    return 0;
  },

  WavedashJS_GetOrCreateLeaderboard: function (leaderboardNamePtr, sortMethod, displayType, callbackPtr, requestIdPtr) {
    var lbName = UTF8ToString(leaderboardNamePtr);
    var requestId = UTF8ToString(requestIdPtr);

    // helper defined right here, visible in this scope
    function getWasmFunction(ptr) {
      // Unity 2022+ / 6.x (Emscripten 3.x)
      if (Module && Module["wasmTable"]) {
        return Module["wasmTable"].get(ptr);
      }
      // Sometimes wasmTable is exposed as a global
      if (typeof wasmTable !== "undefined") {
        return wasmTable.get(ptr);
      }
      // Unity 2021.3 (Emscripten 2.x) still has dynCall helpers
      if (typeof dynCall_vi !== "undefined") {
        return function (arg) {
          dynCall_vi(ptr, arg);
        };
      }
      throw new Error("Cannot resolve wasm function pointer");
    }

    var cb = getWasmFunction(callbackPtr);

    window.WavedashJS.getOrCreateLeaderboard(lbName, sortMethod, displayType)
      .then(function (response) {
        var payload = { requestId: requestId, response: response };
        var json = JSON.stringify(payload);
        var size = lengthBytesUTF8(json) + 1;
        var buffer = _malloc(size);
        stringToUTF8(json, buffer, size);
        cb(buffer);
        _free(buffer);
      })
      .catch(function (err) {
        var payload = { requestId: requestId, error: String(err) };
        var json = JSON.stringify(payload);
        var size = lengthBytesUTF8(json) + 1;
        var buffer = _malloc(size);
        stringToUTF8(json, buffer, size);
        cb(buffer);
        _free(buffer);
      });
  },
  
  // JS -> Unity callbacks
  WavedashJS_RegisterUnityCallbacks: function (gameObjectNamePtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    
    if (typeof window !== 'undefined' && window.WavedashJS) {
      // Register the callback GameObject name with WavedashJS
      // The Unity instance should be provided by the hosting page
      if (typeof window.WavedashJS.registerUnityCallbackReceiver === 'function') {
        window.WavedashJS.registerUnityCallbackReceiver(gameObjectName);
      } else {
        console.warn("[Unity] WavedashJS.registerUnityCallbackReceiver not found. Make sure WavedashJS is properly initialized.");
      }
    }
  }
}); 