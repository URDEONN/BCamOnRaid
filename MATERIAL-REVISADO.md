# Material recibido y revisión

El archivo original `assets.zip` se descargó desde el enlace de Google Drive facilitado por el usuario: 254.890.725 bytes, 28 archivos más entradas de carpetas, aproximadamente 251,4 MiB extraídos.

## Inspeccionado

- `SymbioteController.cs`: locomoción superficial, tentáculos y salto de posesión.
- `SwatHostController.cs`: control del huésped, habilidades y expulsión; la función de IA original solo aplicaba gravedad.
- Ambos shaders originales: son surface shaders del Built-in Render Pipeline, por lo que se implementaron shaders URP. El shader de infección original también tenía una declaración sin tipo en su estructura Input.
- `prototipo_simbionte_swat.html`: referencia de controles y comportamiento, conservada fuera del código activo de Unity.
- Estructura GLB: SWAT clásico 69 huesos, Swat_Guy y Vanguard 65, Soldier_Animated 49 con Idle/Run/TPose/Walk.
- Modelos del simbionte: Core 7 huesos, Tentacle 8, Full 42.

## Conversión incluida

Blender 4.3.2 se utilizó para convertir los tres modelos del simbionte y el SWAT clásico a FBX nativo, conservando mallas, pesos y huesos. La reimportación de los FBX en Blender confirmó las cuentas 7/8/42/69 y un SWAT de aproximadamente 1,779 m de altura antes de normalizarlo a 1,8 m en el juego. Se excluyeron las mallas auxiliares ocultas de visualización de huesos.

Las texturas difusas y normales se extrajeron directamente de los buffers del GLB; las dos texturas especulares se recuperaron del FBX original. Los diez archivos de animación se copiaron sin sustituir sus datos.

## Pendiente de prueba en Unity

La conversión en Blender no verifica el Avatar Humanoid de Unity, el retargeting, la compilación de shaders, las colisiones o la calidad visual en Play. Deben comprobarse en el editor. La versión se entrega como prototipo, con navegación local y recursos provisionales para la instalación, el rifle y los efectos.
