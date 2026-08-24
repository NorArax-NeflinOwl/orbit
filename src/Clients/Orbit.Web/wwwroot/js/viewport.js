// Plain classic script (not an ES module) so a single global function is available for interop calls
// that don't need a whole module import - mirrors the mobile breakpoint already used throughout
// app.css (@media (max-width: 680px)), kept here as the one place both CSS and Blazor code check it.
window.OrbitViewport = {
    isMobile: () => window.matchMedia('(max-width: 680px)').matches
};
