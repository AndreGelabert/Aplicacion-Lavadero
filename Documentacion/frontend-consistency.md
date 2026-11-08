# Tema y CSS

La preferencia «Sistema», «Claro» u «Oscuro» se comparte entre el acceso y la aplicación mediante `localStorage` (`theme`). Sin preferencia válida se sigue el sistema; si el almacenamiento está bloqueado, la selección dura hasta recargar. El script local se ejecuta antes de las hojas para aplicar el tema inicial.

Para regenerar la hoja distribuida después de cambiar vistas o clases literales de JavaScript, ejecutar `npm run build:css` con las dependencias existentes. Flowbite CSS y JavaScript siguen cargándose desde CDN en la aplicación; no se ha verificado equivalencia para retirarlos. La auditoría visual del resto de las vistas queda para T6.
