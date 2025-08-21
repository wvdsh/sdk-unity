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