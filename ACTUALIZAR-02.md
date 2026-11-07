# Actualizar a la iteración 02

Esta iteración añade cuerpo visible, manos y equipo provisionales, sincronización de los pasos con las piernas y la cámara, y una pequeña respuesta al aterrizar. Los modelos y las animaciones son procedurales y geométricos.

## Si ya tienes el proyecto abierto y funcionando

1. Detén **Play** y cierra el editor de Unity.
2. Descarga el ZIP de la rama `prototype/bodycam-presence` y extráelo completo.
3. Dentro del ZIP extraído, abre `Assets/Bodycam` y copia la carpeta **Scripts**.
4. Pégala dentro de **Assets/Bodycam** de tu proyecto actual. Acepta reemplazar los archivos existentes e incluir los nuevos archivos `.cs` y `.meta`.
5. Abre de nuevo tu proyecto desde Unity Hub, espera la compilación y pulsa **Play**.

La carpeta que debes actualizar es `Assets/Bodycam/Scripts`. No necesitas copiar `Packages`, `ProjectSettings`, `Library` ni tus escenas generadas. El menú debe mostrar **PROTOTIPO 02** dentro del juego.

También puedes abrir el ZIP extraído como un proyecto independiente desde Unity Hub.

## Qué probar

- Mira hacia abajo: comprueba que se vean las botas y las piernas.
- Camina y corre: escucha los pasos y observa si el cuerpo acompaña el movimiento.
- Agáchate y levántate; prueba Q/E junto a una pared.
- Salta, aterriza y supera la barrera amarilla; comprueba que las manos no tapen el centro de la pantalla.
- Pulsa **F3** para comparar el juego con y sin cuerpo y manos.
- Usa **Esc → Movimiento reducido** si prefieres menos balanceo.
- Para ampliar Game: pulsa Esc, coloca el cursor sobre la pestaña Game y pulsa **Shift + Espacio**; vuelve a pulsar Esc para continuar.

Esta versión todavía requiere compilación y prueba jugable en Unity. Si aparece un error, comparte el primer mensaje rojo completo de Console.
