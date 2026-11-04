# Материалы карточки 0.32.0

Кадры получены из самой игры на Unity 2022.3.47f1, без рисованного геймплея. Вертикальная запись использует тот же Canvas и адаптивную раскладку в RenderTexture 720×1280. Временная выдача ресурсов отключена в релизе; для записи используется изолированный подготовленный прогресс.

## Ролики

| Файл | Формат | Содержание |
|---|---|---|
| [gameplay.mp4](store/gameplay.mp4) | 1280×720, 24,018 с, 1 405 456 байт | Добыча, чары, обмен кирок, результат, покупка шахтёра |
| [gameplay-vertical.mp4](store/gameplay-vertical.mp4) | 720×1280, 24,013 с, 1 617 558 байт | Та же последовательность в телефонном интерфейсе |
| [trailer.mp4](store/trailer.mp4) | 1280×720, 24 с, 1 798 252 байт | Горизонтальный рекламный трейлер |
| [trailer-vertical.mp4](store/trailer-vertical.mp4) | 720×1280, 24 с, 1 944 006 байт | Вертикальный рекламный трейлер |

Все видео — MP4/H.264, yuv420p, BT.709, 30 кадров/с, полностью декодированы FFmpeg. Геймплей без звука; в трейлерах AAC 48 кГц, оригинальная музыка, синтезированная `make-store-trailer.py`, и титры шрифтом проекта PT Sans. Проверенная пиковая громкость вертикального трейлера — −13,3 dBFS, средняя — −25,8 dBFS.

Запись сохраняет реальный темп по временным меткам: 394 исходных кадра горизонтальной и 419 вертикальной сессии. Прогресс подготовлен для демонстрации механик, последовательность кирок зафиксирована для показа обмена; это не обещание такого выигрыша или темпа прохождения с нуля. Валюту, награды, зачарования и анимации рассчитывает игра.

## Изображения

В карточке сохранены [иконка](store/icon.png) 512×512, [обложка](store/cover.png) 800×470, два снимка ПК 1280×720 и три телефонных 720×1280. Дополнительно подготовлены [альбомная шахта](store/mobile-landscape-mine.png) и [альбомная рулетка](store/mobile-landscape-roulette.png) 1280×720. Они сняты в браузере с мобильным UA; в карточку их не добавляли, потому что один мобильный набор допускает одну ориентацию. Основной набор остаётся портретным.

Девять изображений проверены как RGB PNG, по размерам и наличию изображения; новые кадры просмотрены. Точные размеры, вес и SHA-256 всех 13 медиафайлов — в [manifest-v0320.json](store/manifest-v0320.json), тексты и параметры карточки — в [card-ru.json](store/card-ru.json).

## Повторение

1. Собрать Windows-плеер через `DemoTools.VerifyAndBuild`.
2. Запустить плеер с `-demoSmokeTest -demoStoreCapture -demoOutput "D:/YniUni/_YandexGame/_MineLootka/Logs"`. Папки кадров — `Logs/store-frames-v0320` и `Logs/store-frames-v0320-vertical`, в каждой `times.csv`.
3. Из корня проекта выполнить:

   ```powershell
   python Tools/encode-store-video.py Logs/store-frames-v0320 --ffmpeg _test-concept/media-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe --output Docs/store/gameplay.mp4
   python Tools/encode-store-video.py Logs/store-frames-v0320-vertical --ffmpeg _test-concept/media-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe --output Docs/store/gameplay-vertical.mp4
   python Tools/make-store-trailer.py Docs/store/gameplay.mp4 --ffmpeg _test-concept/media-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe --font Assets/MineLoot/Resources/Fonts/PTSans-Bold.ttf --output Docs/store/trailer.mp4
   python Tools/make-store-trailer.py Docs/store/gameplay-vertical.mp4 --ffmpeg _test-concept/media-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe --font Assets/MineLoot/Resources/Fonts/PTSans-Bold.ttf --output Docs/store/trailer-vertical.mp4
   ```

Нужны Python, Pillow и FFmpeg. Использован уже установленный официальный бинарник из `imageio-ffmpeg`; промежуточные кадры и зависимости в Git не входят. Телефонные альбомные снимки сохраняет `Tools/web-browser-check.cjs` в `Logs/store-mobile-landscape-*-v0320.png`; доступен отдельный прогон `--phones-only`.

## Состояние на площадке

Все четыре ролика сохранены в черновике 639502. Консоль пока не подтверждает завершение обработки, поэтому проверка скачанных серверных перекодировок не выполнена. Исходные файлы проверены локально; статус и ID — [YandexDraft.md](YandexDraft.md). Форматы сверены с [документацией Яндекса](https://yandex.ru/dev/games/doc/ru/console/add-new-game/draft).
