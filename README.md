# BCamOnRaid

Prototipo de un juego policial para PC con cámara corporal, movimiento con peso y parkour contextual. Proyecto fuente para **Unity 6.6 (6000.6.8f1)** y **URP 17.6.0**.

## Descargar y abrir

1. En este repositorio, pulsa **Code → Download ZIP** y extrae el archivo completo en una carpeta local.
2. En **Unity Hub → Proyectos → Agregar proyecto desde disco**, selecciona la carpeta extraída que contiene `Assets`, `Packages` y `ProjectSettings`.
3. Ábrela con **6000.6.8f1**. Espera a que Unity importe el proyecto y resuelva los paquetes oficiales; la primera apertura puede requerir Internet.
4. La configuración inicial crea y abre `Training.unity`. Si no aparece, usa **Bodycam → Crear escena de entrenamiento**; si ya existe, **Bodycam → Abrir entrenamiento**.
5. Pulsa **Play** y haz clic en la pestaña **Game**. El mapa y el jugador se generan al entrar en Play.

No hace falta crear otro proyecto ni instalar Visual Studio para probarlo. El contenido inicial de la escena es un generador; el edificio aparece durante la ejecución.

## Incluye

- Edificio de entrenamiento de seis habitaciones con puertas que se abren y cierran.
- Caminar, correr con resistencia, agacharse, saltar e inclinarse a ambos lados.
- Superar obstáculos y trepar superficies válidas de aproximadamente 0,35 a 1,45 m.
- Cámara a altura del pecho, balanceo, cambios sutiles de campo de visión y comprobaciones de espacio al inclinarse.
- Distorsión de lente, viñeta, grano y aberración cromática mediante URP.
- Linterna, sonidos provisionales sintetizados y HUD en español.
- Pausa con sensibilidad, intensidad del movimiento de cámara y modo de movimiento reducido.

## Controles

| Tecla | Acción |
| --- | --- |
| WASD / ratón | Moverse / mirar |
| Shift izquierdo | Correr |
| Ctrl izquierdo | Agacharse mientras se mantiene |
| C | Activar o desactivar agacharse |
| Q / E | Inclinarse a izquierda / derecha |
| Espacio | Saltar o superar un obstáculo cercano válido |
| G | Abrir o cerrar la puerta que estás mirando |
| F | Linterna |
| F2 | Activar o desactivar efectos de lente |
| Tab | Mostrar u ocultar controles |
| Esc | Pausa y ajustes |
| R | Volver a la posición inicial |

Para probar el parkour, avanza hasta la barrera amarilla, acércate y pulsa Espacio. En la primera habitación izquierda hay una caja baja y una plataforma para trepar.

## Modificar el prototipo

El código está en `Assets/Bodycam`:

- `Scripts/Player/RaidPlayer.cs`: movimiento, postura, cámara y parkour.
- `Scripts/Player/RaidAudio.cs`: audio provisional.
- `Scripts/World/RaidWorld.cs`: distribución, objetos, materiales y luces.
- `Scripts/World/RaidDoor.cs`: apertura de puertas.
- `Scripts/Presentation/BodycamLens.cs`: efectos URP.
- `Scripts/Presentation/RaidHUD.cs`: interfaz y ajustes.
- `Editor/BodycamSetup.cs`: creación inicial de escena y configuración.

Los ajustes del menú se aplican a la sesión actual. Los cambios hechos a los objetos durante Play no se guardan al salir de Play. Consulta `LEEME.txt` para detalles y solución de problemas.

## Estado y validación

Es una primera base con geometría simple y recursos originales. Todavía no incluye armas, sospechosos, compañeros, IA, misiones, cuerpo animado ni recursos fotorrealistas. El parkour es contextual; no permite correr por paredes o agarrarse de cualquier superficie.

Se revisaron la sintaxis de los siete archivos C#, la integración entre scripts, la estructura del proyecto y las API de configuración de URP contra el código fuente oficial de Unity para la rama `6000.6`. **La compilación y la prueba jugable en Unity están pendientes**: el entorno donde se preparó no dispone del editor.

Si aparecen errores, abre **Window → General → Console** y comparte el primer mensaje rojo completo.

## Licencia

Los scripts y la documentación originales se distribuyen bajo MIT; consulta `LICENSE.txt`. Unity y sus paquetes oficiales tienen sus propias licencias y se resuelven mediante el administrador de paquetes.
