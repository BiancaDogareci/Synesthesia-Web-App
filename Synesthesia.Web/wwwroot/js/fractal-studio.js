window.addEventListener("DOMContentLoaded", () => {
    const audio = document.getElementById("audio");
    const fractalSelect = document.getElementById("fractalType");

    // will hold a function to remove/stop the visualizer when switching fractals or stopping audio
    let cleanup = null;

    let activeUniforms = null;
    let audioContext = null;

    let currentConfig = {
        fractalType: "julia",
        colors: {
            primary: [1.0, 0.3, 0.6],
            secondary: [0.2, 0.6, 1.0]
        },
        motion: {
            bassStrength: 1.0,
            trebleStrength: 1.0,
            rotationSpeed: 0.2,
            zoomPulse: 0.05
        },
        quality: {
            iterations: 300,
            raySteps: 100
        },
        julia: {
            cx: -0.4,
            cy: -0.59
        },
        colorMode: {
            rainbow: false
        }
    };

    // Julia preset definitions (GLOBAL so HTML can access them)
    window.juliaPresets = {
        classic: { cx: 0.0, cy: 0.8 },
        dragon: { cx: 0.37, cy: 0.1 },
        snowflake: { cx: 0.355, cy: 0.355 },
        spiral: { cx: 0.34, cy: -0.05 },
        lotus: { cx: -0.54, cy: 0.54 },
        chaos: { cx: -0.4, cy: -0.59 }
    };

    window.updateFractalConfig = function (partialConfig) {
        currentConfig = {
            ...currentConfig,
            ...partialConfig,
            colors: { ...currentConfig.colors, ...partialConfig.colors },
            motion: { ...currentConfig.motion, ...partialConfig.motion },
            quality: { ...currentConfig.quality, ...partialConfig.quality },
            julia: { ...currentConfig.julia, ...partialConfig.julia }
        };

        if (!activeUniforms) return;

        activeUniforms.primaryColor.value.set(...currentConfig.colors.primary);
        activeUniforms.secondaryColor.value.set(...currentConfig.colors.secondary);

        activeUniforms.bassStrength.value = currentConfig.motion.bassStrength;
        activeUniforms.trebleStrength.value = currentConfig.motion.trebleStrength;
        activeUniforms.rotationSpeed.value = currentConfig.motion.rotationSpeed;
        activeUniforms.zoomPulse.value = currentConfig.motion.zoomPulse;

        activeUniforms.iterations.value = currentConfig.quality.iterations;
        activeUniforms.raySteps.value = currentConfig.quality.raySteps;

        if (partialConfig.julia) {
            activeUniforms.juliaC.value.set(
                partialConfig.julia.cx,
                partialConfig.julia.cy
            );
        }

        if (partialConfig.colorMode?.rainbow !== undefined) {
            activeUniforms.rainbowMode.value =
                partialConfig.colorMode.rainbow ? 1.0 : 0.0;
        }
    };

    window.initVisualizer = function (fractalType = "julia", containerId = "fractal-container") {
        const container = document.getElementById(containerId);
        if (!container) {
            console.warn("No container found:", containerId);
            return () => { };
        }

        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }

        // clean container so old visualizations are removed
        container.innerHTML = "";

        // Creates a WebGL renderer
        const renderer = new THREE.WebGLRenderer({ antialias: true });
        // Sets renderer size to match container and appends its canvas to the DOM
        renderer.setSize(container.clientWidth, container.clientHeight);
        container.appendChild(renderer.domElement);

        // Creates a Three.js scene
        const scene = new THREE.Scene();
        const camera = new THREE.PerspectiveCamera(
            60, // FOV
            container.clientWidth / container.clientHeight,
            0.1, // near plane
            1000 // far plane
        );
        camera.position.z = 3;

        // shader uniforms
        const uniforms = {
            iTime: { value: 0 }, // elapsed time (seconds) used for animation
            iResolution: { value: new THREE.Vector3(container.clientWidth, container.clientHeight, 1) }, // container size
            bassLevel: { value: 0 },
            trebleLevel: { value: 0 },

            //lfo: { value: 0 }, // low-frequency oscillation
            zoom: { value: 1.0 },
            //iBoom: { value: 0 }, // strong beat/bass kick
            //speed: { value: 1.0 },

            // user controls
            primaryColor: { value: new THREE.Vector3(...currentConfig.colors.primary) },
            secondaryColor: { value: new THREE.Vector3(...currentConfig.colors.secondary) },

            bassStrength: { value: currentConfig.motion.bassStrength },
            trebleStrength: { value: currentConfig.motion.trebleStrength },
            rotationSpeed: { value: currentConfig.motion.rotationSpeed },
            zoomPulse: { value: currentConfig.motion.zoomPulse },

            iterations: { value: currentConfig.quality.iterations },
            raySteps: { value: currentConfig.quality.raySteps },

            pulse: { value: 1.0 },
            rotation: { value: 0.0 },

            juliaC: { value: new THREE.Vector2(-0.4, -0.59) },
            rainbowMode: { value: currentConfig.colorMode?.rainbow ? 1.0 : 0.0 },

            // mandelbulb
            lfo: { value: 0.0 },
            iBoom: { value: 0.0 },
            speed: { value: 1.0 },
        };

        activeUniforms = uniforms;

        // vertex shader
        const vertexShader = `
            void main() { 
                gl_Position = vec4(position, 1.0); 
            }
        `;

        // Each shader uses GLSL and the uniforms to draw a fractal that reacts to music
        let fragmentShader = "";
        if (fractalType === "mandelbulb") {
            // Mandelbulb GLSL fragment shader
            fragmentShader = `
                precision highp float;

                uniform float iTime, bassLevel, trebleLevel, lfo, zoom, iBoom, speed, pulse, rotation;
                uniform vec3 iResolution;

                // Mandelbulb distance estimator
                float mandelbulbDE(vec3 pos) {

                    // z -> Current point in fractal iteration
                    // c -> Original position (constant for Mandelbulb formula)
                    // dr -> Distance estimator derivative. Used for ray marching
                    // bassImpact = pow(bassLevel, 0.3) * 4.0 -> non-linear bass influence; softens low levels
                    // power = 8.0 + bassImpact + iBoom*5.0 + sin(...) *2.0 + trebleLevel*1.5 -> controls Mandelbulb exponent dynamically:
                    //      Base 8.0 -> standard Mandelbulb
                    //      bassImpact -> makes fractal “puffier” on bass
                    //      iBoom*5.0 -> emphasizes strong beat hits
                    //      sin(iTime*0.2 + lfo)*2.0 -> slow oscillation over time
                    //      trebleLevel*1.5 -> subtle high-frequency influence
                    vec3 z = pos, c = pos;
                    float dr = 1.0, r = 0.0;
                    float bassImpact = pow(bassLevel, 0.3) * 4.0;
                    float power = 8.0 + bassImpact + iBoom * 5.0 + sin(iTime * 0.2 + lfo) * 2.0 + trebleLevel * 1.5;

                    // Mandelbulb iteration
                    // Convert z to spherical coords: r (radius), th (theta), ph (phi)
                    // zr = pow(r, power-1) -> radius raised to fractal power
                    // dr -> derivative update for distance estimation
                    // z = zr * spherical + c -> Mandelbulb iteration
                    // Iterates 12 times -> moderate detail, balancing performance and quality
                    for (int i = 0; i < 12; i++) {
                        r = length(z);
                        if (r > 4.0) break;
                        float th = acos(z.z / r), ph = atan(z.y, z.x);
                        float zr = pow(r, power - 1.0);
                        dr = pow(r, power - 1.0) * power * dr + 1.0;
                        float nt = th * power, np = ph * power;
                        z = zr * vec3(sin(nt) * cos(np), sin(nt) * sin(np), cos(nt)) + c;
                    }

                    // distance estimator used for ray marching
                    return 0.5 * log(r) * r / dr;
                }

                // Marches a ray rd from camera ro into the fractal scene
                float rayMarch(vec3 ro, vec3 rd) {
                    float t = 0.0;
                    for (int i = 0; i < 100; i++) {
                        vec3 p = ro + rd * t;
                        float d = mandelbulbDE(p);
                        if (d < 0.001) break; // hit threshold (close enough to surface)
                        t += d;
                        if (t > 50.0) break; // far clip distance
                    }
                    return t;
                }

                void main() {
                    // Normalized screen coordinates uv
                    vec2 uv = (gl_FragCoord.xy - 0.5 * iResolution.xy) / iResolution.y;

                    // Adjusted camera zoom
                    float smoothZoom = 4.0 / (1.0 + 0.2 * bassLevel + 0.1 * iBoom); // smaller music influence
                    vec3 ro = vec3(0.0, 0.0, smoothZoom);
                    vec3 rd = normalize(vec3(uv, -1.5));

                    // Adjusted rotation
                    float ang = iTime * 0.1                // slower base rotation
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

                    // If the ray misses the fractal -> black background
                    float t = rayMarch(ro, rd);
                    if (t > 49.9) {
                        gl_FragColor = vec4(0.0, 0.0, 0.0, 1.0);
                        return;
                    }

                    // Approximates surface normal via finite differences
                    // eps = 0.001 -> small offset to compute gradient
                    vec3 p = ro + rd * t;
                    vec3 eps = vec3(0.001, 0, 0);
                    vec3 nor = normalize(vec3(
                        mandelbulbDE(p + eps.xyy) - mandelbulbDE(p - eps.xyy),
                        mandelbulbDE(p + eps.yxy) - mandelbulbDE(p - eps.yxy),
                        mandelbulbDE(p + eps.yyx) - mandelbulbDE(p - eps.yyx)
                    ));

                    // Simple diffuse lighting using a directional light
                    vec3 light = normalize(vec3(1.0, 1.0, 1.0));
                    // diff = Lambertian shading
                    float diff = clamp(dot(nor, light), 0.0, 1.0);

                    vec3 col = vec3(
                        0.5 + 0.5 * sin(iTime + p.x * 2.0 + iBoom * 3.0),
                        0.5 + 0.5 * cos(iTime * 1.2 + p.y * 3.0 + bassLevel * 4.0),
                        diff
                    );
                    col *= (diff * 1.3 + 0.2);
                    col = pow(col, vec3(0.4545)); // gamma correction (≈ 1/2.2)
                    gl_FragColor = vec4(col * pulse, 1.0);
                }
                `;
        } else if (fractalType === "julia") {
            // Julia GLSL fragment shader
            fragmentShader = `
                precision highp float;

                uniform float iTime;
                uniform float bassLevel;
                uniform float trebleLevel;

                uniform float bassStrength;
                uniform float trebleStrength;
                uniform float iterations;

                uniform vec3 primaryColor;
                uniform vec3 secondaryColor;

                uniform vec3 iResolution;
                uniform float pulse;
                uniform float rainbowMode; // 0 = off, 1 = on

                uniform vec2 juliaC;

                vec2 getC() {
                    vec2 C = juliaC;

                    // gentle musical motion around chosen Julia
                    C.x += 0.01 * bassLevel * bassStrength * sin(iTime * 0.15);
                    C.y += 0.01 * trebleLevel * trebleStrength * cos(iTime * 0.11);

                    return C;
                }

                /* for rainbow */
                vec3 colorRamp(float t) {
                    float phase = trebleLevel * trebleStrength * 0.15;

                    vec3 rainbow = vec3(
                        0.5 + 0.5 * sin(6.2831 * (t + phase) + 0.0),
                        0.5 + 0.5 * sin(6.2831 * (t + phase) + 2.1),
                        0.5 + 0.5 * sin(6.2831 * (t + phase) + 4.2)
                    );

                    return mix(primaryColor, rainbow, smoothstep(0.0, 0.25, t));
                }

                /* for other colors */
                vec3 palette(float t) {
                    // sharpen banding
                    float bands = pow(t, 0.6);

                    // audio modulated phase
                    float phase =
                        bassLevel * bassStrength * 0.4 +
                        trebleLevel * trebleStrength * 0.3;

                    // oscillating weights
                    float w1 = sin(6.2831 * (bands * 1.0 + phase));
                    float w2 = sin(6.2831 * (bands * 2.3 - phase));
                    float w3 = sin(6.2831 * (bands * 4.7 + iTime * 0.1));

                    // hard color separation
                    vec3 col =
                        primaryColor   * (0.6 + 0.4 * w1) +
                        secondaryColor * (0.6 + 0.4 * w2) +
                        mix(primaryColor, secondaryColor, 0.5) * (0.3 + 0.3 * w3);

                    return clamp(col, 0.0, 1.0);
                }

                float huePhase(vec3 c) {
                    float maxC = max(c.r, max(c.g, c.b));
                    float minC = min(c.r, min(c.g, c.b));
                    float delta = maxC - minC;

                    float hue = 0.0;
                    if (delta > 0.0001) {
                        if (maxC == c.r) hue = mod((c.g - c.b) / delta, 6.0);
                        else if (maxC == c.g) hue = (c.b - c.r) / delta + 2.0;
                        else hue = (c.r - c.g) / delta + 4.0;
                        hue /= 6.0;
                    }
                    return hue * 6.28318;
                }

                void main() {
                    /* stable viewport */
                    vec2 uv = (gl_FragCoord.xy - 0.5 * iResolution.xy) / iResolution.y;
                    uv *= 1.6;

                    /* subtle space breathing (not sliding) */
                    float zoom =
                        1.0 +
                        0.03 * sin(iTime * 0.3) +
                        0.02 * bassLevel * bassStrength;
                    uv *= zoom;

                    /* soft rotation */
                    float angle = 0.07 * bassLevel * bassStrength;
                    float s = sin(angle);
                    float c = cos(angle);
                    uv = vec2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);

                    /* Julia iteration */
                    vec2 z = uv;
                    vec2 C = getC();

                    int maxIter = int(iterations);
                    int i;

                    for (i = 0; i < 1000; i++) {
                        if (i >= maxIter) break;

                        float x = z.x * z.x - z.y * z.y + C.x;
                        float y = 2.0 * z.x * z.y + C.y;
                        z = vec2(x, y);

                        if (dot(z, z) > 4.0) break;
                    }

                    float t = float(i) / float(maxIter);

                    t = smoothstep(0.0, 1.0, t);
                    t = pow(t, 0.85);

                    /* coloring */
                    vec3 col;

                    if (rainbowMode > 0.5) {
                        float phase = huePhase(secondaryColor);

                        float p =
                            6.28318 * t +
                            phase +
                            bassLevel * bassStrength * 0.6 +
                            trebleLevel * trebleStrength * 0.4 +
                            iTime * 0.25;

                        col = vec3(
                            0.5 + 0.5 * sin(p + 0.0),
                            0.5 + 0.5 * sin(p + 2.094),
                            0.5 + 0.5 * sin(p + 4.188)
                        );

                        // anchor rainbow with primary color
                        col = mix(primaryColor, col, 0.85);
                    } else {
                        float rings = sin(30.0 * t + iTime * 0.3);
                        col = mix(
                            primaryColor,
                            secondaryColor,
                            smoothstep(-0.2, 0.2, rings)
                        );
                    }

                    float shadow = pow(t, 0.35);
                    col *= shadow * pulse;

                    /* inner glow */
                    float glow = exp(-20.0 * dot(uv, uv));
                    col += glow * 0.12 * secondaryColor;

                    gl_FragColor = vec4(col, 1.0);
                }
            `;
        }
        else if (fractalType === "mandelbrot") {
            // Mandelbrot GLSL fragment shader
            fragmentShader = `
                precision highp float; // high precision floats, important for fractals where small differences matter

                uniform float iTime;
                uniform float bassLevel; // (0-1 normalised)
                uniform float trebleLevel;  // (0-1 normalised) - ignored here for now
                uniform float pulse; // global effect multiplier
                uniform vec3 iResolution; // screen resolution (x = width, y = height, z = depth), used to normalize coordinates

                //   Simple rainbow color
                // Converts a normalized value t (0–1) into a smooth RGB rainbow gradient
                // 6.28318 ≈ 2π -> full sine wave cycle
                // 0.5 + 0.5 * sin(...) -> remaps sin(-1..1) to [0,1]
                vec3 rainbow(float t) {
                    float r = 0.5 + 0.5 * sin(6.28318 * t + 0.0);
                    float g = 0.5 + 0.5 * sin(6.28318 * t + 2.1);
                    float b = 0.5 + 0.5 * sin(6.28318 * t + 4.2);
                    return vec3(r, g, b);
                }

                void main() {
                    //   Viewport — static, zoomed out, centered
                    // gl_FragCoord.xy -> pixel coordinates on screen
                    // 0.5 * iResolution.xy -> shifts origin to center of screen
                    // / iResolution.y -> normalizes coordinates so aspect ratio is preserved
                    vec2 uv = (gl_FragCoord.xy - 0.5 * iResolution.xy) / iResolution.y;

                    uv *= 2.8; // zoom out so we see more of the Mandelbrot set
                    uv.x -= 0.5; // shifts horizontally to center the fractal visually

                    // Classic Mandelbrot (power = 2.0)
                    float power = 2.0;

                    // iterations between 5-50
                    // bass = 0 -> 50 iterations (sharp)
                    // bass = 1 -> 5 iterations (blobby)
                    // Mapping: 50 - bass * 45
                    float bass = clamp(bassLevel, 0.0, 1.0); // clamps bassLevel between 0–1
                    int maxIter = int(50.0 - bass * 45.0);

                    // Tiny parameter offset (to c) from bass for gentle breathing
                    vec2 paramK = 0.002 * bass * vec2(
                        sin(iTime * 0.8),
                        cos(iTime * 0.7)
                    );

                    // Initialize fractal iteration
                    vec2 c = uv;
                    vec2 z = vec2(0.0);
                    float escapedR = 0.0; // stores squared magnitude when the point escapes
                    int i;

                    // Mandelbrot iteration loop
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

                    // Color shaping
                    // mix(a,b,f) -> linear interpolation between a and b by factor f
                    // a = vec3(1.0,0.3,0.6) -> pinkish base color
                    // b = rainbow(t) -> rainbow gradient based on escape time
                    // smoothstep(0.0,0.15,t) -> smooth interpolation for t in [0,0.15], keeps inner fractal pink and outer fractal colorful
                    vec3 base = mix(vec3(1.0, 0.3, 0.6), rainbow(t), smoothstep(0.0, 0.15, t));

                    // soft darkening for points closer to the center
                    float shadow = pow(t, 0.4);

                    // pulsating brightness controlled by bass and pulse
                    // 0.9 -> minimum brightness
                    // 0.4 * bass * sin(...) -> oscillating brightness, adds music response
                    float bassFlash = 0.9 + 0.4 * bass * sin(iTime * 3.0 + pulse * 1.5);

                    // Final color = base * shadow * bass effect
                    vec3 col = base * shadow * bassFlash;

                    // Gentle static vignette
                    // dot(uv, uv) -> distance² from center
                    // exp(-2.5 * distance²) -> Gaussian falloff for vignette
                    float vign = exp(-2.5 * dot(uv, uv));
                    col *= (0.95 + 0.2 * vign);

                    // clamp ensures RGB stays in [0,1] and the second parameter 1 means fully opaque
                    gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);
                }
            `;
        }


        // Creates a 2x2 plane covering the viewport
        // Applies the shader material(vertex + fragment shader)
        // Adds it to the Three.js scene
        const quad = new THREE.Mesh(
            new THREE.PlaneGeometry(2, 2),
            new THREE.ShaderMaterial({
                vertexShader,
                fragmentShader,
                uniforms
            })
        );
        scene.add(quad);

        // Adds a point light to illuminate the scene
        // Useful mainly for 3D fractals (lighting gives depth)
        const light = new THREE.PointLight(0xffffff, 1.2);
        light.position.set(3, 3, 5);
        scene.add(light);

        //   Audio context & analyser
        // Connects <audio> element to an analyser node to get frequency data
        // fftSize = 2048 determines the resolution of frequency data
        // freqData stores the analyzed spectrum for each frame
        const analyser = audioContext.createAnalyser();
        analyser.fftSize = 2048;

        const source = audioContext.createMediaElementSource(audio);
        source.connect(analyser);
        analyser.connect(audioContext.destination);

        const freqData = new Uint8Array(analyser.frequencyBinCount);

        // Updates renderer and camera when the browser window resizes
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

            uniforms.bassLevel.value = bass * currentConfig.motion.bassStrength;
            uniforms.trebleLevel.value = treble * currentConfig.motion.trebleStrength;
            uniforms.iTime.value += 0.016;
            uniforms.zoom.value = 1.0 + Math.pow(bass, 0.3) * currentConfig.motion.zoomPulse;
            uniforms.rotation.value += currentConfig.motion.rotationSpeed * 0.01;

            console.log(bass, treble);

            renderer.render(scene, camera);
        }

        animate();

        // stops animation and removes the visualizer from the DOM
        // called when switching fractal types or stopping audio
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



    updateFractalConfig({
        motion: { bassStrength: 2.5 },
        colors: { primary: [0.2, 0.9, 0.7] }
    });

    // Render fractal immediately
    cleanup = window.initVisualizer(fractalSelect.value, "fractal-container");

    //   When the audio starts playing:
    // Any old visualizer is cleaned up
    // initVisualizer is called with the selected fractal type
    if (audio) {
        audio.addEventListener("play", () => {
            if (cleanup) cleanup();
            cleanup = window.initVisualizer(fractalSelect.value, "fractal-container");
        });
    }

    //   When the fractal type dropdown changes:
    // Old visualizer is cleaned up
    // New visualizer is initialized
    fractalSelect?.addEventListener("change", (ev) => {
        if (cleanup) cleanup();
        cleanup = window.initVisualizer(ev.target.value, "fractal-container");
    });

    // Before leaving the page, cleanup the visualizer
    window.addEventListener("beforeunload", () => {
        if (cleanup) cleanup();
    });
});
