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

        // shader uniforms
        const uniforms = {
            iTime: { value: 0 },
            iResolution: { value: new THREE.Vector3(container.clientWidth, container.clientHeight, 1) },
            bassLevel: { value: 0 },
            trebleLevel: { value: 0 },
            lfo: { value: 0 },
            zoom: { value: 1.0 },
            iBoom: { value: 0 },
            speed: { value: 1.0 },
            pulse: { value: 1.0 },
            rotation: { value: 0.0 }
        };

        // quad vertex shader
        const vertexShader = `
            void main() { 
                gl_Position = vec4(position, 1.0); 
            }
        `;

        // fragment shader
        const fragmentShader = `

        precision highp float;
        uniform float iTime, bassLevel, trebleLevel, lfo, zoom, iBoom, speed, pulse, rotation;
        uniform vec3 iResolution;

        float mandelbulbDE(vec3 pos) {
            vec3 z = pos, c = pos;
            float dr = 1.0, r = 0.0;
            float bassImpact = pow(bassLevel, 0.3) * 4.0;
            float power = 8.0 + bassImpact + iBoom * 5.0 + sin(iTime * 0.2 + lfo) * 2.0 + trebleLevel * 1.5;
            for (int i = 0; i < 12; i++) {
                r = length(z);
                if (r > 4.0) break;
                float th = acos(z.z / r), ph = atan(z.y, z.x);
                float zr = pow(r, power - 1.0);
                dr = pow(r, power - 1.0) * power * dr + 1.0;
                float nt = th * power, np = ph * power;
                z = zr * vec3(sin(nt) * cos(np), sin(nt) * sin(np), cos(nt)) + c;
            }
            return 0.5 * log(r) * r / dr;
        }

        float rayMarch(vec3 ro, vec3 rd) {
            float t = 0.0;
            for (int i = 0; i < 100; i++) {
                vec3 p = ro + rd * t;
                float d = mandelbulbDE(p);
                if (d < 0.001) break;
                t += d;
                if (t > 50.0) break;
            }
            return t;
        }

        void main() {
            vec2 uv = (gl_FragCoord.xy - 0.5 * iResolution.xy) / iResolution.y;
            vec3 ro = vec3(0.0, 0.0, 4.0 / zoom);
            vec3 rd = normalize(vec3(uv, -1.5));

            float ang = iTime * 0.15 + trebleLevel * 3.0 + iBoom * 2.0 + rotation;
            mat3 rotY = mat3(
                cos(ang), 0.0, sin(ang),
                0.0, 1.0, 0.0,
                -sin(ang), 0.0, cos(ang)
            );
            ro = rotY * ro;
            rd = rotY * rd;

            float t = rayMarch(ro, rd);
            if (t > 49.9) {
                gl_FragColor = vec4(0.0, 0.0, 0.0, 1.0);
                return;
            }

            vec3 p = ro + rd * t;
            vec3 eps = vec3(0.001, 0, 0);
            vec3 nor = normalize(vec3(
                mandelbulbDE(p + eps.xyy) - mandelbulbDE(p - eps.xyy),
                mandelbulbDE(p + eps.yxy) - mandelbulbDE(p - eps.yxy),
                mandelbulbDE(p + eps.yyx) - mandelbulbDE(p - eps.yyx)
            ));

            vec3 light = normalize(vec3(1.0, 1.0, 1.0));
            float diff = clamp(dot(nor, light), 0.0, 1.0);

            vec3 col = vec3(
                0.5 + 0.5 * sin(iTime + p.x * 2.0 + iBoom * 3.0),
                0.5 + 0.5 * cos(iTime * 1.2 + p.y * 3.0 + bassLevel * 4.0),
                diff
            );
            col *= (diff * 1.3 + 0.2);
            col = pow(col, vec3(0.4545));
            gl_FragColor = vec4(col * pulse, 1.0);
        }
        `;

        // quad
        const quad = new THREE.Mesh(
            new THREE.PlaneGeometry(2, 2),
            new THREE.ShaderMaterial({
                vertexShader,
                fragmentShader,
                uniforms
            })
        );
        scene.add(quad);


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

            // Get audio data
            analyser.getByteFrequencyData(freqData);

            // Calculate bass (0–10)
            let bassSum = 0;
            for (let i = 0; i < 10; i++) bassSum += freqData[i];
            let bass = bassSum / 10 / 255;

            // Calculate treble
            let trebleSum = 0;
            for (let i = 11; i < freqData.length; i++) trebleSum += freqData[i];
            let treble = trebleSum / (freqData.length - 11) / 255;

            uniforms.bassLevel.value = bass;
            uniforms.trebleLevel.value = treble;
            uniforms.iTime.value += 0.016;
            uniforms.zoom.value = 1.0 + Math.pow(bass, 0.3);

            renderer.render(scene, camera);
        }

        animate();

        return function cleanupVisualizer() {
            running = false;
            window.removeEventListener("resize", onResize);

            try {
                renderer.dispose();
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
