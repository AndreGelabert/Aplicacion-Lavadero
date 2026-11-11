---
name: LavaFácil
description: Sistema visual para la administración diaria del lavadero.
colors:
  azul-operativo: "rgb(26 86 219)"
  superficie: "rgb(255 255 255)"
  neutro-suave: "rgb(243 244 246)"
  borde: "rgb(229 231 235)"
  texto: "rgb(17 24 39)"
  texto-secundario: "rgb(107 114 128)"
  aviso-error: "#991b1b"
  aviso-exito: "#166534"
  aviso-advertencia: "#854d0e"
typography:
  body:
    fontFamily: 'ui-sans-serif, system-ui, sans-serif, "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol", "Noto Color Emoji"'
    fontSize: "0.875rem"
    lineHeight: "1.25rem"
  title:
    fontSize: "1.5rem"
    fontWeight: 600
    lineHeight: "2rem"
rounded:
  lg: "0.5rem"
  default: "0.25rem"
spacing:
  2: "0.5rem"
  3: "0.75rem"
  4: "1rem"
components:
  button-primary:
    backgroundColor: "{colors.azul-operativo}"
    textColor: "{colors.superficie}"
    rounded: "{rounded.lg}"
    padding: "0.5rem"
  button-secondary:
    backgroundColor: "{colors.superficie}"
    textColor: "{colors.texto}"
    rounded: "{rounded.lg}"
    padding: "0.5rem 1rem"
  input:
    textColor: "{colors.texto}"
    rounded: "{rounded.lg}"
    padding: "0.625rem"
  nav-active:
    rounded: "{rounded.lg}"
    padding: "0.5rem"
  chip:
    rounded: "{rounded.default}"
    padding: "0.125rem 0.625rem"
  card:
    backgroundColor: "{colors.superficie}"
    rounded: "{rounded.lg}"
---

# Design System: LavaFácil

## Overview

**Creative North Star: "El libro diario del lavadero"**

Una interfaz accesible, cercana y ordenada para el trabajo cotidiano: información fácil de recorrer y controles familiares para personas con distinta experiencia digital.

Las superficies, bordes y sombras organizan la operación sin cambiar la identidad existente. La descripción combina código con la evidencia acotada de navegador indicada abajo; no es una auditoría completa de accesibilidad ni una revisión visual píxel por píxel.

**Key Characteristics:**
- Azul operativo y grises neutros.
- Capas para organizar.
- Componentes familiares, claros y amables.

## Colors

El azul operativo acompaña acciones y navegación; los grises separan fondo, superficies, texto y bordes.

### Primary
- **Azul operativo:** acción destacada y señalización de navegación; el azul del botón de ayuda está presente en el CSS emitido.

### Neutral
- **Superficie y neutro suave:** superficies blancas y fondos o estados grises claros.
- **Texto, texto secundario y borde:** jerarquía de lectura y separación de controles.

Los estados usan nombres funcionales y texto: por ejemplo, pagado, pendiente y cancelado. Los avisos personalizados distinguen éxito, error y advertencia; el significado de un color puede variar según el contexto de pago, lavado o retiro.

La hoja emitida contiene valores concretos de azul que no coinciden necesariamente con la escala declarada aparte en `tailwind.config.js`. Su `module.exports` tiene `theme.extend` vacío y la asignación posterior a `tailwind.config` es separada: no se asume que `primary` esté disponible. Algunas clases usadas por las vistas no aparecen en `app.min.css`; Flowbite se carga después y puede afectar la cascada. Las rampas del sidecar son vistas previas sintetizadas, no tokens de producción.

## Typography

La fuente base emitida es una pila sans del sistema; Inter figura en la asignación separada de configuración, pero no se afirma como fuente efectiva. El texto pequeño de controles y tabla usa 0.875rem con interlineado 1.25rem; el título de Lavados usa 1.5rem, peso 600 e interlineado 2rem. La tabla emplea encabezados pequeños en mayúsculas.

## Layout

La aplicación combina barra superior fija, navegación lateral y contenido principal. La barra lateral mide 16rem, permanece cerrada inicialmente en todos los tamaños y se despliega sobre el contenido sin reservar margen lateral. El contenido usa 1rem de relleno, con separación superior para la barra. En Lavados los filtros pasan de columna a fila en `md`; la cuadrícula de filtros pasa de una a dos columnas en `md` y cuatro en `lg` (1024px).

## Elevation & Depth

Capas para organizar: fondos y bordes separan áreas; sombras distinguen contenedores, menús y diálogos. El CSS emitido define la sombra de menú `0 10px 15px -3px rgb(0 0 0 / 0.1), 0 4px 6px -4px rgb(0 0 0 / 0.1)` y la sombra de diálogo `0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1)`. El aviso personalizado tiene su propia sombra de una capa: `0 10px 15px -3px rgb(0 0 0 / .1)`.

## Shapes

Los controles y paneles recurren a esquinas suavemente redondeadas de 0.5rem; las etiquetas de estado usan 0.25rem. Los campos y botones secundarios se delimitan con bordes finos. Las tarjetas de Lavados adquieren esquinas redondeadas desde `sm` (640px).

## Components

- **Botones:** el botón azul de ayuda usa texto blanco, radio 0.5rem y relleno 0.5rem; las acciones secundarias combinan blanco, borde gris y cambio de fondo al pasar el puntero. Se observan anillos de foco en las vistas; su resultado efectivo depende de las hojas cargadas.
- **Campos:** superficies grises claras, borde, texto oscuro y radio 0.5rem; los campos de formulario usan 0.625rem de relleno. Algunas clases de foco `primary` no están en el CSS emitido inspeccionado.
- **Navegación:** elementos con icono y etiqueta; el módulo actual se distingue con fondo azul claro y texto azul. El menú lateral se despliega sobre el contenido en todos los tamaños.
- **Etiquetas:** estado legible en texto pequeño, con fondo y texto coloreados; por ejemplo, «Pagado» en verde y «En Proceso» en azul.
- **Contenedores y tablas:** panel blanco con encabezado separado y tabla compacta; filas y columnas presentan datos de trabajo y acciones. Las tablas anchas usan una región de desplazamiento horizontal enfocada con teclado, con paginación fuera. T20 verificó 39/39 casos sintéticos y 36/36 casos reales, con auditoría independiente del alcance y evidencia; no implica revisión píxel por píxel de todas las pantallas.
- **Avisos:** tarjetas apiladas arriba a la derecha, con borde lateral acentuado, texto de estado y botón de cierre con foco visible.

La disposición principal carga `app.min.css`, `Firebase.styles.css`, `notifications.css` y después Flowbite por CDN. El acceso usa otra disposición y carga `Firebase.styles.css` antes de `app.min.css`. El selector real de tema se comprobó en nueve vistas a 1440/390 en claro y oscuro; también se verificaron 12 aperturas y cierres reales de tres modales en esas combinaciones. Es evidencia acotada, no una revisión píxel por píxel de todas las páginas ni prueba completa de la cascada remota. Persisten advertencias conocidas de inicialización de Flowbite.

## Do's and Don'ts

### Do:
- **Do** conservar etiquetas textuales para identificar estados.
- **Do** organizar controles y datos con superficies, bordes y sombras existentes.
- **Do** mantener los componentes familiares, claros y amables.

### Don't:
- **Don't** tratar la configuración separada de `primary` o Inter como prueba de que está activa.
- **Don't** presentar las rampas sintetizadas del sidecar como tokens de producción.
