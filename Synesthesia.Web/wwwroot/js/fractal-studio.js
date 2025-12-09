window.addEventListener("DOMContentLoaded", () => {
    const audio = document.getElementById("audio");
    const fractalSelect = document.getElementById("fractalType");

    let cleanup = null;

    window.initVisualizer = function (fractalType = "julia", containerId = "fractal-container") {
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

        let fragmentShader = "";

        if (fractalType === "mandelbulb") {
            fragmentShader = `
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

                    // --- Adjusted camera zoom ---
                    float smoothZoom = 4.0 / (1.0 + 0.2 * bassLevel + 0.1 * iBoom); // smaller music influence
                    vec3 ro = vec3(0.0, 0.0, smoothZoom);
                    vec3 rd = normalize(vec3(uv, -1.5));

                    // --- Adjusted rotation ---
                    float ang = iTime * 0.1                 // slower base rotation
                              + trebleLevel * 0.5          // scaled down treble influence
                              + iBoom * 0.3                // scaled down iBoom influence
                              + rotation * 0.5;            // scaled down rotation parameter

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
        } else if (fractalType === "julia") {
            fragmentShader = `
                precision highp float;

                uniform float iTime;
                uniform float bassLevel;
                uniform float trebleLevel;
                uniform float pulse;
                uniform vec3 iResolution;

                // Animated Julia constant with light music influence
                vec2 getC() {
                    float moveX = 0.02 * sin(iTime*0.15);
                    float moveY = 0.02 * cos(iTime*0.11);

                    moveX += 0.01 * bassLevel;    // slight push
                    moveY += 0.005 * trebleLevel; // small shimmer

                    return vec2(-0.7 + moveX, 0.27015 + moveY);
                }

                // Soft rainbow with softer shimmer
                vec3 rainbow(float t) {
                    float shift = trebleLevel * 0.1; // mild color change

                    float r = 0.5 + 0.5 * sin(6.2831*(t+shift) + 0.0);
                    float g = 0.5 + 0.5 * sin(6.2831*(t+shift) + 2.1);
                    float b = 0.5 + 0.5 * sin(6.2831*(t+shift) + 4.2);

                    float brightness = 0.8 + 0.15 * sin(iTime*0.5 + t*10.0 + trebleLevel);
                    return vec3(r, g, b) * brightness;
                }

                void main() {
                    vec2 uv = (gl_FragCoord.xy - 0.5*iResolution.xy) / iResolution.y;

                    // Base Julia size
                    uv *= 1.6;

                    // Subtle wobble from bass
                    uv += 0.015 * bassLevel * vec2(sin(iTime), cos(iTime*0.7));

                    // Small treble shimmer
                    uv += 0.003 * trebleLevel * vec2(sin(iTime*20.0), cos(iTime*18.0));

                    // Gentle zoom breathing
                    float zoom = 1.0 +
                                 0.03 * sin(iTime*0.3) +
                                 0.02 * bassLevel;   // stronger than before but still clean

                    uv *= zoom;

                    // Soft swirl
                    float angle = 0.07 * bassLevel;
                    float s = sin(angle), c = cos(angle);
                    uv = vec2(uv.x*c - uv.y*s, uv.x*s + uv.y*c);

                    vec2 z = uv;
                    vec2 C = getC();

                    int maxIter = 300;
                    int i;
                    for (i = 0; i < maxIter; i++) {
                        float x = z.x*z.x - z.y*z.y + C.x;
                        float y = 2.0*z.x*z.y + C.y;
                        z = vec2(x, y);
                        if (dot(z,z) > 4.0) break;
                    }

                    float t = float(i) / float(maxIter);

                    // Pink blob core
                    vec3 blob = vec3(1.0, 0.3, 0.6);
                    float blobMix = smoothstep(0.0, 0.18, t);
                    vec3 base = mix(blob, rainbow(t + 0.05*trebleLevel), blobMix);

                    float shadow = pow(t, 0.3);
                    vec3 col = base * shadow * pulse;

                    // Soft edge glow
                    float glow = exp(-20.0 * dot(uv, uv));
                    col += glow * 0.12 * vec3(0.2, 0.3, 0.6);

                    gl_FragColor = vec4(col, 1.0);
                }

            `;
        }
        else if (fractalType === "mandelbrot") {
            fragmentShader = `
                precision highp float;

                uniform float iTime;
                uniform float bassLevel;
                uniform float trebleLevel;  // ignored
                uniform float pulse;
                uniform vec3 iResolution;

                // ------------------------------------------------------------
                // Simple rainbow color
                // ------------------------------------------------------------
                vec3 rainbow(float t) {
                    float r = 0.5 + 0.5 * sin(6.28318 * t + 0.0);
                    float g = 0.5 + 0.5 * sin(6.28318 * t + 2.1);
                    float b = 0.5 + 0.5 * sin(6.28318 * t + 4.2);
                    return vec3(r, g, b);
                }

                void main() {

                    // --------------------------------------------------------
                    // Viewport — static, zoomed out, centered
                    // --------------------------------------------------------
                    vec2 uv = (gl_FragCoord.xy - 0.5 * iResolution.xy) / iResolution.y;
                    uv *= 2.8;
                    uv.x -= 0.5;

                    // --------------------------------------------------------
                    // Classic Mandelbrot (power = 2.0)
                    // --------------------------------------------------------
                    float power = 2.0;

                    // --------------------------------------------------------
                    // Inverted bass-only iteration control
                    //
                    // bass = 0 → ~50 iterations (sharp)
                    // bass = 1 → ~5 iterations (blobby)
                    //
                    // Mapping: 50 - bass * 45
                    // --------------------------------------------------------
                    float bass = clamp(bassLevel, 0.0, 1.0);
                    int maxIter = int(50.0 - bass * 45.0);

                    // --------------------------------------------------------
                    // Tiny parameter offset from bass for gentle breathing
                    // --------------------------------------------------------
                    vec2 paramK = 0.002 * bass * vec2(
                        sin(iTime * 0.8),
                        cos(iTime * 0.7)
                    );

                    vec2 c = uv;
                    vec2 z = vec2(0.0);
                    float escapedR = 0.0;

                    int i;

                    // --------------------------------------------------------
                    // Mandelbrot iteration loop
                    // --------------------------------------------------------
                    for (i = 0; i < maxIter; i++) {
                        float x = z.x*z.x - z.y*z.y;
                        float y = 2.0*z.x*z.y;
                        z = vec2(x, y) + c + paramK;

                        float r2 = dot(z, z);
                        if (r2 > 4.0) {
                            escapedR = r2;
                            break;
                        }
                    }

                    // Escape time normalized
                    float t = float(i) / float(maxIter);

                    // --------------------------------------------------------
                    // Color shaping
                    // --------------------------------------------------------
                    vec3 base = mix(vec3(1.0, 0.3, 0.6), rainbow(t), smoothstep(0.0, 0.15, t));

                    float shadow = pow(t, 0.4);
                    float bassFlash = 0.9 + 0.4 * bass * sin(iTime * 3.0 + pulse * 1.5);

                    vec3 col = base * shadow * bassFlash;

                    // Gentle static vignette
                    float vign = exp(-2.5 * dot(uv, uv));
                    col *= (0.95 + 0.2 * vign);

                    gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
                }
    `;
}


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
