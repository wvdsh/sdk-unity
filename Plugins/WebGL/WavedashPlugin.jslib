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
      var user = window.WavedashJS.getUser();
      if (user) {
        var userJson = JSON.stringify(user);
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
      // Pass the Unity instance and GameObject name directly to WavedashJS
      // This way WavedashJS can store them internally without window exposure
      if (typeof window.WavedashJS.setUnityInstance === 'function') {
        // Get Unity instance from the Module (Unity's internal reference)
        var _unityInstance = Module.unityInstance || unityInstance;
        
        window.WavedashJS.setUnityInstance(_unityInstance, gameObjectName);
      }
    }
  }
}); 