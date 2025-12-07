window.addEventListener("DOMContentLoaded", () => {
    const audio = document.getElementById("audio");
    const fractalSelect = document.getElementById("fractalType");

    let cleanup = null;

    window.initVisualizer = function (fractalType = "mandelbulb", containerId = "fractal-container") {
        const container = document.getElementById(containerId);
        if (!container) {
            console.warn("No container found:", containerId);
            return () => { };
        }

        container.innerHTML = "";

        const renderer = new THREE.WebGLRenderer({ antialias: true });
        renderer.setSize(container.clientWidth, container.clientHeight);
        container.appendChild(renderer.domElement);

        const scene = new THREE.Scene();
        const camera = new THREE.PerspectiveCamera(
            60,
            container.clientWidth / container.clientHeight,
            0.1,
            1000
        );
        camera.position.z = 3;

        // test geometry
        const geometry = new THREE.IcosahedronGeometry(1, 4);
        const material = new THREE.MeshStandardMaterial({
            color: 0xffffff,
            roughness: 0.3,
            metalness: 0.6,
        });
        const mesh = new THREE.Mesh(geometry, material);
        scene.add(mesh);

        const light = new THREE.PointLight(0xffffff, 1.2);
        light.position.set(3, 3, 5);
        scene.add(light);

        const audioContext = new (window.AudioContext || window.webkitAudioContext)();
        const analyser = audioContext.createAnalyser();
        analyser.fftSize = 2048;

        const source = audioContext.createMediaElementSource(audio);
        source.connect(analyser);
        analyser.connect(audioContext.destination);

        const freqData = new Uint8Array(analyser.frequencyBinCount);

        function onResize() {
            const w = container.clientWidth;
            const h = container.clientHeight;
            renderer.setSize(w, h);
            camera.aspect = w / h;
            camera.updateProjectionMatrix();
        }
        window.addEventListener("resize", onResize);

        let running = true;

        function animate() {
            if (!running) return;

            requestAnimationFrame(animate);

            analyser.getByteFrequencyData(freqData);
            let bass = freqData[1] / 255;

            mesh.rotation.x += 0.003 + bass * 0.02;
            mesh.rotation.y += 0.002 + bass * 0.03;

            renderer.render(scene, camera);
        }

        animate();

        return function cleanupVisualizer() {
            running = false;
            window.removeEventListener("resize", onResize);

            try {
                renderer.dispose();
                geometry.dispose();
                material.dispose();
                container.innerHTML = "";
            } catch (err) {
                console.warn("Cleanup issue:", err);
            }
        };
    };

    if (audio) {
        audio.addEventListener("play", () => {
            if (cleanup) cleanup();
            cleanup = window.initVisualizer(fractalSelect.value, "fractal-container");
        });
    }

    fractalSelect?.addEventListener("change", (ev) => {
        if (cleanup) cleanup();
        cleanup = window.initVisualizer(ev.target.value, "fractal-container");
    });

    window.addEventListener("beforeunload", () => {
        if (cleanup) cleanup();
    });
});