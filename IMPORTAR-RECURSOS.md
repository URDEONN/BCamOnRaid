# Recursos y preparación en Unity

La versión descargable ya contiene el SWAT clásico, sus texturas, las diez animaciones tácticas y los tres modelos del simbionte en FBX. No necesitas volver a importar assets.zip para probar esta versión.

## Preparación inicial

Al abrir el proyecto por primera vez, el menú Symbiote crea la escena, configura URP y prepara los recursos. El importador mantiene la jerarquía de huesos, crea un Avatar Humanoid para el SWAT y las animaciones, y deja los modelos del simbionte como Generic.

Si el personaje queda en T-pose o Console muestra un aviso del Avatar, sal de Play y ejecuta **Symbiote → Integrar modelos y animaciones FBX**. Revisa Rig → Configure si Unity no reconoce algún hueso. Este proceso todavía requiere validación con el editor de Unity.

Los recursos están en:

- `Assets/External/SWAT`: modelo clásico y texturas.
- `Assets/External/Animations`: diez clips tácticos.
- `Assets/External/Symbiote`: núcleo, tentáculo individual y cuerpo completo.

El núcleo de siete huesos y los tentáculos procedurales son la forma activa. El rig completo de 42 huesos y la cadena individual de ocho huesos están incluidos, pero no sustituyen automáticamente ese sistema de locomoción visual.

## Añadir otros recursos del ZIP original

1. Detén Play.
2. Usa **Symbiote → Importar assets.zip (modelos y texturas)** y selecciona tu ZIP local.
3. El importador copia modelos y texturas a `Assets/External` y después intenta preparar los FBX conocidos. Conserva los controladores de animación existentes.

Este menú no importa los scripts, shaders o HTML originales como código activo. Los originales revisados se conservan bajo `Reference`, fuera de `Assets`.

Los GLB adicionales necesitan un importador GLTF compatible o una conversión a FBX. Los tres modelos del simbionte incluidos ya están convertidos, así que no hace falta instalar otro paquete para ellos.

Swat_Guy supera 100 MB por archivo en ambos formatos. La primera versión usa el SWAT clásico para mantener la descarga y el proyecto más ligeros. Los otros modelos y Soldier_Animated se conservan en el ZIP original del usuario.
