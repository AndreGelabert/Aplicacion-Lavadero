# Theme and CSS

The System, Light, or Dark preference is shared between login and the application using `localStorage` (`theme`). Without a valid preference, the system setting is followed; if storage is blocked, the selection lasts until reload. The local script runs before stylesheets to apply the initial theme.

To regenerate the distributed stylesheet after changing views or literal JavaScript classes, run `npm run build:css` with existing dependencies. Flowbite CSS and JavaScript still load from CDN; equivalence without them has not been verified. The remaining visual audit is deferred to T6.

## Table empty states

Eight table partials share `_TableEmptyState` inside their existing cells. Each retains its own message and colspan, including filtered-result wording and the audit filter hint. Errors remain separate from empty results. Empty cells use zero outer padding; the shared inner state owns its spacing.

## Form actions

Main form actions use `app-button` with primary, secondary, danger or success variants. Compact controls, icons, dropdown actions and modal-specific controls retain their contextual styling. Modal backdrops, focus management, scrolling and lifecycle remain T4 work; authenticated visual coverage remains T6.
