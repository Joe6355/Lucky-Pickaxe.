mergeInto(LibraryManager.library, {
  LP_Attach: function (receiver) { window.LuckyPickaxe.attachReceiver(UTF8ToString(receiver)); },
  LP_Ready: function () { window.LuckyPickaxe.markReady(); },
  LP_ReadProgress: function (key) {
    var value = window.LuckyPickaxe.readProgress(UTF8ToString(key));
    if (!value) return 0;
    var size = lengthBytesUTF8(value) + 1;
    var pointer = _malloc(size);
    stringToUTF8(value, pointer, size);
    return pointer;
  },
  LP_WriteProgress: function (key, json) {
    window.LuckyPickaxe.writeProgress(UTF8ToString(key), UTF8ToString(json));
  }
});
