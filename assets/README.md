# Ресурсы

- `ball.ico` — иконка приложения, используемая компилятором при сборке `.exe`.
- `skins.png` — галерея четырёх скинов для главного README.
- `tennis-ball.png` — отдельный прозрачный предпросмотр теннисного мяча.

Изображения созданы процедурным рендерером приложения, без внешних изображений и сетевых запросов. Исходная отрисовка находится в [src/Graphics/BallArt.cs](../src/Graphics/BallArt.cs) и [src/Graphics/GameOverlay.cs](../src/Graphics/GameOverlay.cs).

После сборки галерею можно обновить из корня репозитория:

```powershell
Start-Process .\bin\WindowBall.exe -ArgumentList '--render-gallery assets/skins.png' -Wait
```

Изображения, нужные только для отладки, сохраняй в [artifacts](../artifacts/README.md).
