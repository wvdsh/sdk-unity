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

  WavedashJS_GetLeaderboard: function (leaderboardNamePtr, sortMethod, displayType, gameObjectNamePtr, methodNamePtr, requestIdPtr) {
    var lbName    = UTF8ToString(leaderboardNamePtr);
    var goName    = UTF8ToString(gameObjectNamePtr);
    var method    = UTF8ToString(methodNamePtr);
    var requestId = UTF8ToString(requestIdPtr);
  
    if (!window.WavedashJS || typeof window.WavedashJS.getOrCreateLeaderboard !== 'function') {
      console.error('WavedashJS.getOrCreateLeaderboard not available');
      return;
    }
  
    window.WavedashJS.getOrCreateLeaderboard(lbName, sortMethod, displayType)
      .then(function (response) {
        var parsed = (typeof response === "string") ? JSON.parse(response) : response;

        var payload = { requestId: requestId, response: parsed };
        var json = JSON.stringify(payload);
        console.log("WavedashJS_GetLeaderboard JSON: " + json);

        (typeof SendMessage === 'function'
          ? SendMessage
          : unityInstance.SendMessage)(goName, method, json);
      })
      .catch(function (err) {
        var payload = { requestId: requestId, error: String(err) };
        var json = JSON.stringify(payload);
        (typeof SendMessage === 'function'
          ? SendMessage
          : unityInstance.SendMessage)(goName, method, json);
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