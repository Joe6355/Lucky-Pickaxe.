# Материалы карточки 0.31.1

Материалы отрисованы из текущего проекта Unity 2022.3.47f1. Временная вкладка выдачи ресурсов отключена. Состояние загрузки — [YandexDraft.md](YandexDraft.md).

| Файл | Формат | Содержание |
|---|---|---|
| [icon.png](store/icon.png) | RGB PNG, 512×512 | Иллюстрация кирки и руды без внешней рамки |
| [cover.png](store/cover.png) | RGB PNG, 800×470 | Текущий игровой мир и название |
| [desktop-mine.png](store/desktop-mine.png) | RGB PNG, 1280×720 | Шахта и улучшения |
| [desktop-roulette.png](store/desktop-roulette.png) | RGB PNG, 1280×720 | Оплаченный выбор кирок |
| [mobile-mine.png](store/mobile-mine.png) | RGB PNG, 720×1280 | Телефонная шахта |
| [mobile-roulette.png](store/mobile-roulette.png) | RGB PNG, 720×1280 | Телефонная рулетка |
| [mobile-enchanting.png](store/mobile-enchanting.png) | RGB PNG, 720×1280 | Зачарование без тестовой вкладки |
| [gameplay.mp4](store/gameplay.mp4) | H.264, yuv420p, BT.709, 1280×720, 30 кадров/с, около 18 с | Добыча, обмен кирок, результат и покупка шахтёра |

Все пять кадров показывают настоящий интерфейс плеера. Прогресс подготовлен проверочным режимом, чтобы показать доступные действия; рулетка использует фиксированную последовательность инструментов для демонстрации обмена. Покупки, расчёт результата и анимации выполняет существующая игра. Это демонстрация механик, а не темпа прохождения с нуля.

Windows ограничивает высоту окна размером монитора. Телефонные кадры поэтому рендерятся непосредственно в RenderTexture 720×1280: тот же Canvas и `ApplyResponsiveLayout`, без увеличения или растягивания готового изображения. Затем обычная раскладка восстанавливается. Иконка и обложка — иллюстрации из `VoxelStage` и `PixelArt`, не снимки интерфейса.

Ролик собран по реальным временным меткам 357 исходных JPEG. Видео без звука; цветовой диапазон переводится из полного JPEG в ограниченный BT.709 для стандартного H.264. Размер исходного MP4 — 1 181 068 байт. SHA-256: `cbd157f1575273390e85ccd2e8d75db1d7b83f243471286342dcd7825aa62c01`. Полное декодирование FFmpeg прошло. Семь PNG проверены по размерам, режиму RGB и непустому изображению, снимки просмотрены. Отчёты: `Logs/store-capture-check.json`, `store-video-check.json`, `store-media-v0311.json`.

## Повторение

1. Собрать Windows-плеер меню **Mine Loot → Build Windows demo**. Иконку/обложку экспортировать через **Mine Loot → Export store images** с графическим устройством, без `-nographics`.
2. Запустить из корня проекта:

   ```powershell
   $taskPlayer = Start-Process '.\Builds\Windows\MineLoot.exe' -ArgumentList '-demoSmokeTest -demoStoreCapture -demoOutput "D:\YniUni\_YandexGame\_MineLootka\Logs" -logFile "D:\YniUni\_YandexGame\_MineLootka\Logs\media-v0311.log"' -WindowStyle Hidden -PassThru
   $taskPlayer.WaitForExit()
   ```

   Используется изолированный проверочный прогресс. PNG: `Logs/store-*-v0311.png`; кадры ролика и `times.csv`: `Logs/store-frames-v0311/`.
3. Перенести нужные PNG в `Docs/store/`, сохраняя исходные размеры. Затем закодировать ролик:

   ```powershell
   python Tools/encode-store-video.py Logs/store-frames-v0311 --ffmpeg '_test-concept/media-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe' --output Docs/store/gameplay.mp4
   ```

   Нужны Pillow и FFmpeg. В рабочей среде использован существующий официальный бинарник из пакета `imageio-ffmpeg`; зависимости и промежуточные кадры не входят в Git.

Новые материалы соответствуют [размерам полей Яндекса](https://yandex.ru/dev/games/doc/ru/console/add-new-game/draft): два снимка ПК 16:9 и три мобильных 9:16, длинная сторона 1280, RGB PNG; ролик 16:9 короче 28 секунд. Окончательную оценку контента выполняет модерация.

## Обработанный файл Яндекса

Видео 14413136 обработано и скачано штатной кнопкой консоли 4 ноября 2026. Полученный MP4: H.264 High, yuv420p(tv, bt709), 1280×720, 30 кадров/с, 1 290 730 байт. FFmpeg декодировал весь файл с кодом 0. SHA-256: `a54d993ac4e26aed5fbd6268dac673f4839a28647a20063f9b0a6bc341106e02`. Отчёт — `Logs/yandex-video-v0311-check.json`. Исходный ролик в `Docs/store` сохранён для повторяемого экспорта.
