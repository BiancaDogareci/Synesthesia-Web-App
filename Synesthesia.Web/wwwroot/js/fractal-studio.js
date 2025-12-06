window.addEventListener("DOMContentLoaded", () => {
    const audio = document.getElementById("audio");
    const fractalSelect = document.getElementById("fractalType");

    // placeholder visualizer initializer
    window.initVisualizer = function (fractalType = "mandelbulb", containerId = "fractal-container") {
        console.log("initVisualizer placeholder:", fractalType, containerId);

        // placeholder canvas
        const container = document.getElementById(containerId);
        if (!container) return () => { };
        // cleanup any previous placeholder
        const existing = container.querySelector(".fractal-placeholder");
        if (existing) existing.remove();

        const placeholder = document.createElement("div");
        placeholder.className = "fractal-placeholder";
        placeholder.style.width = "100%";
        placeholder.style.height = "100%";
        placeholder.style.display = "flex";
        placeholder.style.alignItems = "center";
        placeholder.style.justifyContent = "center";
        placeholder.style.color = "white";
        placeholder.style.fontSize = "18px";
        placeholder.textContent = `Visualizer: ${fractalType} (placeholder)`;

        container.appendChild(placeholder);

        return function cleanup() {
            try {
                placeholder.remove();
            } catch (e) { }
        };
    };

    let cleanup = null;

    if (audio) {
        audio.addEventListener("play", () => {
            if (cleanup) cleanup();
            cleanup = window.initVisualizer(fractalSelect?.value || "mandelbulb", "fractal-container");
        });

        // start visualizer if already playing
        if (!audio.paused && !audio.ended) {
            if (cleanup) cleanup();
            cleanup = window.initVisualizer(fractalSelect?.value || "mandelbulb", "fractal-container");
        }
    }

    fractalSelect?.addEventListener("change", (ev) => {
        if (cleanup) cleanup();
        cleanup = window.initVisualizer(ev.target.value, "fractal-container");
    });

    window.addEventListener("beforeunload", () => {
        if (cleanup) cleanup();
    });
});
