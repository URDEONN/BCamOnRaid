# SIMBIOSIS / instalación de contención

Nuevo enfoque de **BCamOnRaid**: acción y sigilo inverso en tercera persona. Controlas una biomasa que se adhiere a superficies y posee operarios para sabotear una instalación y escapar.

Proyecto fuente para **Unity 6.6 / 6000.6.8f1**, **Universal Render Pipeline 17.6.0**, PC, teclado y ratón.

## Abrir

1. Descarga el ZIP de esta rama y extráelo en una **carpeta nueva**.
2. En Unity Hub, agrega esa carpeta como proyecto y ábrela con 6000.6.8f1.
3. Espera la importación. Se crea y abre `Containment.unity`; si no aparece, usa el menú **Symbiote** para abrir/crear el prototipo.
4. Pulsa **Play**, entra a **Game** y haz clic dentro de la imagen.

Esta versión cambia el controlador, la escena y la cámara del juego anterior. Ábrela como proyecto completo; no actualices únicamente sus scripts dentro del prototipo bodycam.

## La misión

Infiltra la instalación, toma un huésped con **E**, sabotea **tres terminales con G**, abandona el cuerpo con **E** y escapa como biomasa por la salida verde. Si destruyes todos los huéspedes antes de abrir la contención, tendrás que reintentar.

## Controles

| Entrada | Acción |
| --- | --- |
| WASD | Movimiento relativo a la cámara |
| Ratón / rueda | Mirar / ajustar distancia de cámara |
| Shift | Desplazarse más rápido |
| Ctrl izquierdo | Movimiento lento / agacharse como huésped |
| Espacio | Saltar o desprenderse de la superficie |
| E | Poseer un operario cercano o abandonar el huésped |
| Click izquierdo | Disparar como huésped |
| Click derecho / Q | Ataque de tentáculo, aturdir y atraer |
| F | Explosión radial como huésped |
| G | Sabotear un terminal usando un huésped |
| Tab | Mostrar u ocultar ayuda |
| Esc | Pausa y ratón libre |
| R | Reiniciar la misión |

Para ampliar Game: libera el ratón con Esc, sitúa el cursor en la pestaña Game y pulsa **Shift + Espacio**; pulsa Esc para reanudar.

## Sistemas de esta base

- Movimiento adherido a suelo, paredes y techos mediante consultas físicas y orientación de la superficie.
- Núcleo deformable y tentáculos procedurales con puntos de apoyo.
- Posesión, infección visual del huésped y expulsión al perder el cuerpo.
- Rifle, tentáculo y explosión radial con recursos y enfriamientos.
- Operarios con estados de patrulla, investigación, combate, cobertura, aturdimiento y muerte; visión, ruido y alarma compartida.
- HUD de biomasa, huésped, escudo, alerta y objetivo; pausa, derrota, victoria y reinicio.
- Instalación geométrica de prueba con paredes, techo, zonas de sombra y conductos.

Es una primera implementación de estos sistemas, con recursos importados y elementos geométricos provisionales y navegación local para el mapa de prueba. El acabado, los cruces de esquinas complejos, la navegación de los enemigos y el equilibrio requieren pruebas en Unity.

## Recursos incorporados

Se descargó e inspeccionó el ZIP completo facilitado por el usuario en Google Drive, incluidos los dos controladores originales, los dos shaders y el prototipo HTML.

Esta versión incluye:

- SWAT clásico con sus **69 huesos Mixamo**, mallas y texturas originales. Se convirtió a FBX sin texturas embebidas para reducir el archivo de 5,4 MB a 1,28 MB.
- Las **diez animaciones FBX** tácticas proporcionadas.
- Los tres modelos del simbionte convertidos de GLB a FBX, conservando los rigs de **7, 8 y 42 huesos**, comprobados mediante reimportación en Blender.
- El núcleo de siete huesos como forma activa, deformación de sus huesos y ocho tentáculos procedurales anclados al entorno. Los modelos de tentáculo individual y cuerpo completo se incluyen como recursos adicionales; no se presenta el rig de 42 huesos como si ya tuviera una animación final.

La primera configuración prepara los rigs Generic/Humanoid y genera el controlador táctico. La importación final, los avatares y el retargeting aún deben validarse dentro de Unity. Si hace falta repetir la preparación, usa **Symbiote → Integrar modelos y animaciones FBX**.

Los modelos pesados Swat_Guy, Vanguard y Soldier_Animated quedan en el ZIP original para integración posterior. Consulta `IMPORTAR-RECURSOS.md` y `MATERIAL-REVISADO.md`.

## Código

`Assets/Symbiote/Scripts` separa la partida (`SymbioteGame`), el movimiento (`SymbioteMotor`), la cámara (`SymbioteCamera`), los operarios (`SwatAgent`), la forma orgánica (`SymbioteVisual`), los efectos (`SymbioteVfx`), el mapa (`SymbioteFacility`), la interfaz (`SymbioteHUD`) y la configuración (`SymbioteAssets`).

Los shaders están en `Assets/Symbiote/Shaders`. La configuración del editor y el importador opcional están en `Assets/Symbiote/Editor`.

## Validación y límites

Se revisaron la sintaxis de los once archivos C#, las referencias entre componentes, las dimensiones y los accesos del mapa, y las conversiones de los rigs en Blender. El entorno de desarrollo no dispone del editor Unity: **no se ha compilado ni probado esta nueva versión dentro de Unity**. No es una entrega final pulida ni un ejecutable Windows ya generado.

La visión usa conos y obstáculos; la oscuridad se representa mediante zonas de sigilo definidas en el mapa, no mediante una medición física de iluminación. La navegación es local y no equivale a una IA táctica de producción. Las animaciones importadas necesitan una prueba de retargeting en Unity. El rifle añadido es geométrico y esta base todavía no incorpora audio de combate.

Si aparece un error, comparte el **primer mensaje rojo completo** de Window → General → Console.
