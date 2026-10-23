# Арт и типографика интерфейса 0.3

Три изображения подготовлены встроенным инструментом image_gen и включены в Unity-проект. Фон и интерфейсные элементы разделены: все подписи, цены, кнопки, прогресс и состояния отрисовываются настоящим uGUI, не зашиты в картинку.

| Ресурс | Файл |
|---|---|
| Иллюстрация шахты | `Assets/MineLoot/Resources/Art/mine-cavern-v3.png` |
| Руда с прозрачностью | `Assets/MineLoot/Resources/Art/ore-hero-v3.png` |
| Атлас предметов 4 × 2 | `Assets/MineLoot/Resources/Art/items-atlas-v3.png` |

Атлас: сверху дерево/железо/алмаз/закрытый сундук; снизу открытый сундук/монеты/жетон/наковальня. Он нарезается движком на восемь спрайтов. Прозрачность сохранена в исходных PNG.

Шрифт Manrope взят из [официального репозитория Google Fonts](https://github.com/google/fonts/tree/main/ofl/manrope). В проекте статические начертания 400 и 650, подготовленные fonttools из переменного исходника для рендера Unity 2022. Лицензия SIL OFL включена в `Assets/MineLoot/Resources/Fonts/OFL.txt`.

## Исходные запросы генерации

### Фон

Use case: stylized-concept. Asset type: finished environment background for an actual portrait mobile mining clicker, no UI. Primary request: create a premium hand-painted 2.5D game illustration of an abandoned fantasy mine chamber, atmospheric, sophisticated shapes, tactile stone and dark timber with warm hanging lanterns and turquoise crystal outcrops, deep navy and petrol shadows with small amber highlights. Composition: portrait 2:3, inward framing cave walls at the edges, dark quiet empty space in the middle 50% for a separate clickable ore object, a low central stone pedestal at 68% image height, receding wood supports, subtle mist and motes, unobtrusive rails in the bottom foreground, depth and cinematic bounced light. Soft stylized game render with deliberate large silhouettes and hand-painted details, polished indie game art, NOT pixel art, NOT realistic photo. NO central ore boulder, NO pickaxe, NO interface, NO lettering, NO buttons, NO border, NO watermark. Not a UI mockup; this image is the actual environment sprite.

### Руда

Use case: stylized-concept. Asset type: isolated clickable ore object sprite for a polished fantasy mining mobile game. Primary request: one compact chunky faceted charcoal rock cluster with large luminous aqua and teal crystal veins protruding, asymmetric silhouette, thick tactile bevels, hand-painted 2.5D high quality game asset, gentle isometric perspective from slightly above, amber rim lighting on upper left and cool teal bounce underneath, strong light dark separation, satisfying collectible style, deliberate medium large surfaces, not noisy. Composition: square with one centered rock occupying 75% of frame, all silhouette fully visible and generous transparent margin on all sides. Genuine transparent background, no floor, no base, no environment, no tool or pickaxe, no text, no icon border, no logo, no watermark. NOT pixel art, not photorealistic.

### Атлас

Use case: stylized-concept. Asset type: a single production sprite atlas for a fantasy mining mobile game, genuine transparent background. Exactly 4 columns by 2 rows, 8 equally sized cells in a landscape 2:1 rectangle. Each object fully centered in its own cell, all objects with large transparent padding and no overlap across cell boundaries. Match hand-painted 2.5D game illustrations, refined bevels, tactile wood and metal, warm amber highlights and cool teal shadows, polished mobile game collectible items, NOT pixel art, no photorealism. Row 1 from left to right: wooden pickaxe, silver iron pickaxe, glowing turquoise diamond pickaxe, CLOSED dark wooden treasure chest with gold metal trim. Row 2 left to right: OPEN matching treasure chest glowing with gold coins, small stack of chunky gold coins, a purple and gold admission ticket, dark iron blacksmith anvil with a small glowing ember. There are precisely 8 objects and no other objects. Plain true transparent background throughout the complete sheet. NO text, NO labels, NO numbers, NO drawn cell borders, NO grid, NO logo, NO watermark. Strict 4x2 placement is essential for game engine slicing.
