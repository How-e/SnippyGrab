# Third-party notices

.NET / WPF: MIT, https://github.com/dotnet/wpf and https://github.com/dotnet/runtime.
Tesseract .NET wrapper 5.2.0: Apache-2.0, Copyright Charles Weld, https://github.com/charlesw/tesseract.
Tesseract OCR: Apache-2.0, https://github.com/tesseract-ocr/tesseract.
English tessdata_fast model: Apache-2.0, https://github.com/tesseract-ocr/tessdata_fast.
Leptonica: BSD-2-Clause, https://github.com/DanBloomberg/leptonica.

Release packages include full dependency license texts under licenses/. No dependency signing keys or downloaded models are stored in Git.

Native binaries are now compiled from immutable Tesseract commit db20f322d03664d1e878e2fbf6e904f5da755594 and Leptonica commit 8ad618f103972eed12f195499b92cff1ab37067b. The managed wrapper still looks up legacy DLL filenames; those names do not identify the compiled library versions. External GIF/JPEG/PNG/TIFF/WebP/OpenJPEG/zlib libraries, Tesseract curl/archive/TIFF integration, training tools, graphics and OpenMP are disabled. Microsoft Visual C++ runtime remains a system prerequisite. Full Tesseract/Leptonica licenses are the texts from these pinned sources. See docs/NATIVE-OCR.md for source/build provenance and reviewed upstream security fix commits.
