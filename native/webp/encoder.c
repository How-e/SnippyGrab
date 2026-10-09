#include <stdint.h>
#include <string.h>
#include "webp/encode.h"
typedef int (__cdecl *CancelCheck)(void);
static int Progress(int percent, const WebPPicture* picture) {
  CancelCheck cancel = (CancelCheck)picture->user_data;
  (void)percent;
  return !cancel || !cancel();
}

// Owned ABI: pixels only, never filenames or encoded input. Caller frees output.
__declspec(dllexport) int __cdecl SnippyWebpEncode(const uint8_t* bgra,
    int width, int height, int lossless, int quality, CancelCheck cancel, uint8_t** output, size_t* size) {
  WebPConfig config;
  WebPPicture picture;
  WebPMemoryWriter writer;
  int ok;
  if (!output || !size) return 0;
  *output = NULL; *size = 0;
  if (!bgra || width < 1 || height < 1 || width > 16383 || height > 16383 ||
      (int64_t)width * height > 16000000 || quality < 1 || quality > 100 ||
      (lossless != 0 && lossless != 1)) return 0;
  if (!WebPConfigInit(&config) || !WebPPictureInit(&picture)) return 0;
  config.lossless = lossless; config.quality = (float)quality;
  config.method = 4; config.exact = 1; config.thread_level = 0;
  if (!WebPValidateConfig(&config)) return 0;
  picture.width = width; picture.height = height; picture.use_argb = 1;
  picture.progress_hook = Progress; picture.user_data = (void*)cancel;
  WebPMemoryWriterInit(&writer);
  picture.writer = WebPMemoryWrite; picture.custom_ptr = &writer;
  ok = WebPPictureImportBGRA(&picture, bgra, width * 4) && WebPEncode(&config, &picture);
  WebPPictureFree(&picture);
  if (!ok || writer.size > 100 * 1024 * 1024) { WebPMemoryWriterClear(&writer); return 0; }
  *output = writer.mem; *size = writer.size;
  return 1;
}
__declspec(dllexport) void __cdecl SnippyWebpFree(void* output) { WebPFree(output); }
