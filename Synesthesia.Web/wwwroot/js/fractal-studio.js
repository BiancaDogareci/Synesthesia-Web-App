window.addEventListener("DOMContentLoaded", () => {
    const audio = document.getElementById("audio");
    const fractalSelect = document.getElementById("fractalType");

    window.hexToRgb01 = function (hex) {
        const v = parseInt(hex.slice(1), 16);
        return [((v >> 16) & 255) / 255, ((v >> 8) & 255) / 255, (v & 255) / 255];
    }

    function clamp(n, min, max) {
        return Math.min(max, Math.max(min, n));
    }

    function applySavedSettingsToUI(saved) {
        const ft = saved?.fractalType || "julia";

        if (fractalSelect) fractalSelect.value = ft;

        try { window.updateInfoVisibility?.(); } catch (_) { }

        try {
            if (typeof window.updateFractalSettingsVisibility === "function") {
                window.updateFractalSettingsVisibility();
            }
        } catch (_) { }

        if (ft === "julia") {
            const iter = document.getElementById("iterSlider");
            const bass = document.getElementById("bassSlider");
            const treble = document.getElementById("trebleSlider");
            const pcol = document.getElementById("primaryColor");
            const scol = document.getElementById("secondaryColor");
            const rainbow = document.getElementById("rainbowToggle");
            const presetSelect = document.getElementById("juliaPreset");
            if (presetSelect && saved.juliaPreset) presetSelect.value = saved.juliaPreset;

            if (iter && saved.iterations != null) iter.value = clamp(saved.iterations, 50, 800);
            if (bass && saved.bassStrength != null) bass.value = clamp(saved.bassStrength, 0, 5);
            if (treble && saved.trebleStrength != null) treble.value = clamp(saved.trebleStrength, 0, 5);

            if (pcol && saved.primaryColor) pcol.value = saved.primaryColor;
            if (scol && saved.secondaryColor) scol.value = saved.secondaryColor;
            if (rainbow && saved.rainbow != null) rainbow.checked = !!saved.rainbow;
        }

        if (ft === "mandelbrot") {
            const iter = document.getElementById("mandelIterSlider");
            const bass = document.getElementById("mandelBassSlider");
            const pcol = document.getElementById("mandelPrimaryColor");
            const scol = document.getElementById("mandelSecondaryColor");
            const rainbow = document.getElementById("mandelRainbowToggle");

            if (iter && saved.iterations != null) iter.value = clamp(saved.iterations, 10, 350);
            if (bass && saved.bassStrength != null) bass.value = clamp(saved.bassStrength, 0, 1.2);

            if (pcol && saved.primaryColor) pcol.value = saved.primaryColor;
            if (scol && saved.secondaryColor) scol.value = saved.secondaryColor;
            if (rainbow && saved.rainbow != null) rainbow.checked = !!saved.rainbow;
        }

        if (ft === "mandelbulb") {
            const ray = document.getElementById("bulbRayStepsSlider");
            const bass = document.getElementById("bulbBassSlider");
            const treble = document.getElementById("bulbTrebleSlider");
            const rot = document.getElementById("bulbRotSlider");
            const zoom = document.getElementById("bulbZoomPulseSlider");
            const pcol = document.getElementById("bulbPrimaryColor");
            const scol = document.getElementById("bulbSecondaryColor");
            const rainbow = document.getElementById("bulbRainbowToggle");

            if (ray && saved.raySteps != null) ray.value = clamp(saved.raySteps, 30, 200);
            if (bass && saved.bassStrength != null) bass.value = clamp(saved.bassStrength, 0, 5);
            if (treble && saved.trebleStrength != null) treble.value = clamp(saved.trebleStrength, 0, 5);
            if (rot && saved.rotationSpeed != null) rot.value = clamp(saved.rotationSpeed, 0, 2);
            if (zoom && saved.zoomPulse != null) zoom.value = clamp(saved.zoomPulse, 0, 0.4);

            if (pcol && saved.primaryColor) pcol.value = saved.primaryColor;
            if (scol && saved.secondaryColor) scol.value = saved.secondaryColor;
            if (rainbow && saved.rainbow != null) rainbow.checked = !!saved.rainbow;
        }

    }

    function savedToPartialConfig(saved) {
        const ft = saved?.fractalType || "julia";

        const partial = { fractalType: ft };

        if (ft === "julia") {
            const key = saved?.juliaPreset;
            if (key && window.juliaPresets?.[key]) {
                partial.julia = window.juliaPresets[key];
            }
        }

        if (ft === "mandelbulb") {
            if (saved.raySteps != null) partial.quality = { ...(partial.quality || {}), raySteps: parseInt(saved.raySteps, 10) };
            if (saved.rotationSpeed != null) partial.motion = { ...(partial.motion || {}), rotationSpeed: parseFloat(saved.rotationSpeed) };
            if (saved.zoomPulse != null) partial.motion = { ...(partial.motion || {}), zoomPulse: parseFloat(saved.zoomPulse) };
        }


        if (saved.iterations != null) {
            partial.quality = { iterations: parseInt(saved.iterations, 10) };
        }

        partial.motion = {};
        if (saved.bassStrength != null) partial.motion.bassStrength = parseFloat(saved.bassStrength);
        if (saved.trebleStrength != null) partial.motion.trebleStrength = parseFloat(saved.trebleStrength);

        partial.colors = {};
        if (saved.primaryColor) partial.colors.primary = hexToRgb01(saved.primaryColor);
        if (saved.secondaryColor) partial.colors.secondary = hexToRgb01(saved.secondaryColor);

        if (saved.rainbow != null) {
            partial.colorMode = { rainbow: !!saved.rainbow };
        }

        return partial;
    }


    let cleanup = null;

    let activeUniforms = null;
    let audioContext = null;

    let currentConfig = {
        fractalType: "julia",
        colors: {
            primary: [0.1608, 0.1176, 0.4902],
            secondary: [0.5294, 0.1765, 0.3255] 
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

    window.getFractalConfigSnapshot = function () {
        return JSON.parse(JSON.stringify(currentConfig));
    };

    window.getFractalType = function () {
        const s = document.getElementById("fractalType");
        return s ? s.value : "julia";
    };

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
            julia: { ...currentConfig.julia, ...partialConfig.julia },
            colorMode: { ...currentConfig.colorMode, ...(partialConfig.colorMode || {}) }
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

    window.__isRecordingVideo = false;

    window.initVisualizer = function (fractalType = "julia", containerId = "fractal-container") {
        const container = document.getElementById(containerId);
        if (!container) {
            console.warn("No container found:", containerId);
            return () => { };
        }

        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }

        container.innerHTML = "";

        const renderer = new THREE.WebGLRenderer({
            antialias: true,
            preserveDrawingBuffer: true
        });
        renderer.setSize(container.clientWidth, container.clientHeight);
        container.appendChild(renderer.domElement);

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

            juliaC: { value: new THREE.Vector2(currentConfig.julia.cx, currentConfig.julia.cy) },

            rainbowMode: { value: currentConfig.colorMode?.rainbow ? 1.0 : 0.0 },

            // mandelbulb
            lfo: { value: 0.0 },
            iBoom: { value: 0.0 },
            speed: { value: 1.0 },
        };

        activeUniforms = uniforms;
        window.updateFractalConfig({});
        // vertex shader
        const vertexShader = `
            void main() { 
                gl_Position = vec4(position, 1.0); 
            }
        `;

        // Each shader uses GLSL and the uniforms to draw a fractal that reacts to music
        let fragmentShader = "";
        if (fractalType === "mandelbulb") {
            fragmentShader = `
                precision highp float;

                uniform float iTime, bassLevel, trebleLevel, lfo, zoom, iBoom, speed, pulse, rotation;
                uniform vec3 iResolution;

                // palette
                uniform vec3 primaryColor;
                uniform vec3 secondaryColor;
                uniform float rainbowMode;

                // quality controls
                uniform float raySteps;

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

                float rayMarch(vec3 ro, vec3 rd, out float stepsUsed) {
                  float t = 0.0;
                  stepsUsed = 0.0;

                  int maxSteps = int(clamp(raySteps, 10.0, 200.0));

                  for (int i = 0; i < 200; i++) {
                    if (i >= maxSteps) break;

                    vec3 p = ro + rd * t;
                    float d = mandelbulbDE(p);

                    stepsUsed = float(i);

                    if (d < 0.001) break;
                    t += d;
                    if (t > 50.0) break;
                  }
                  return t;
                }

                vec3 rainbow(float t) {
                  float p = 6.28318 * (t + iTime * 0.05);
                  return vec3(
                    0.5 + 0.5 * sin(p + 0.0),
                    0.5 + 0.5 * sin(p + 2.094),
                    0.5 + 0.5 * sin(p + 4.188)
                  );
                }

                void main() {
                  vec2 uv = (gl_FragCoord.xy - 0.5 * iResolution.xy) / iResolution.y;

                  float smoothZoom = 4.0 / (1.0 + 0.2 * bassLevel + 0.1 * iBoom);
                  vec3 ro = vec3(0.0, 0.0, smoothZoom);
                  vec3 rd = normalize(vec3(uv, -1.5));

                  float ang = iTime * 0.1 + trebleLevel * 0.5 + iBoom * 0.3 + rotation * 0.5;

                  mat3 rotY = mat3(
                    cos(ang), 0.0, sin(ang),
                    0.0, 1.0, 0.0,
                    -sin(ang), 0.0, cos(ang)
                  );

                  ro = rotY * ro;
                  rd = rotY * rd;

                  float stepsUsed;
                  float t = rayMarch(ro, rd, stepsUsed);

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

                  vec3 lightDir = normalize(vec3(1.0, 1.0, 1.0));
                  float diff = clamp(dot(nor, lightDir), 0.0, 1.0);

                  // "depth" factor (closer surfaces a bit brighter)
                  float depth = clamp(1.0 - t / 12.0, 0.0, 1.0);

                  vec3 baseCol = mix(primaryColor, secondaryColor, smoothstep(0.0, 1.0, diff * 0.9 + depth * 0.6));

                  if (rainbowMode > 0.5) {
                    vec3 rb = rainbow(diff * 0.8 + depth * 0.4);
                    baseCol = mix(baseCol, rb, 0.85);
                  }

                  // contrast + gamma-ish
                  vec3 col = baseCol * (0.25 + 1.25 * diff);
                  col *= (0.85 + 0.25 * depth);
                  col = pow(col, vec3(0.4545));

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

                void main() {
                    /* static viewport */
                    vec2 uv = (gl_FragCoord.xy - 0.5 * iResolution.xy) / iResolution.y;

                    /* classic Mandelbrot framing */
                    uv *= 2.8;
                    uv.x -= 0.5;

                    /* bass influence */
                    float bass = clamp(bassLevel * bassStrength, 0.0, 1.0);

                    float minIter = 4.0;                // very blobby
                    float maxIter = max(iterations, 6.0);

                    int maxIterI = int(mix(maxIter, minIter, bass));

                    /* Mandelbrot iteration (fixed c) */
                    vec2 z = vec2(0.0);
                    vec2 cplx = uv;

                    int i;
                    for (i = 0; i < 1000; i++) {
                        if (i >= maxIterI) break;

                        float x = z.x * z.x - z.y * z.y;
                        float y = 2.0 * z.x * z.y;
                        z = vec2(x, y) + cplx;

                        if (dot(z, z) > 4.0) break;
                    }

                    float t = float(i) / float(maxIterI);
                    t = smoothstep(0.0, 1.0, t);
                    t = pow(t, mix(1.2, 0.7, bass));

                    /* coloring */
                    vec3 col;

                    if (rainbowMode > 0.5) {
                        float p =
                            6.28318 * t +
                            bass * 0.6 +
                            iTime * 0.25;

                        col = vec3(
                            0.5 + 0.5 * sin(p + 0.0),
                            0.5 + 0.5 * sin(p + 2.094),
                            0.5 + 0.5 * sin(p + 4.188)
                        );

                        col = mix(primaryColor, col, 0.85);
                    } else {
                        col = mix(primaryColor, secondaryColor, t);
                    }

                    /* depth & pulse */
                    float shadow = pow(t, 0.4);
                    col *= shadow;
                    col *= (0.9 + 0.35 * bass * pulse);

                    /* soft vignette */
                    float vignette = exp(-2.4 * dot(uv, uv));
                    col *= (0.9 + 0.25 * vignette);

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

        if (!window.__studioAudio) window.__studioAudio = {};

        const analyser = audioContext.createAnalyser();
        analyser.fftSize = 2048;

        if (!window.__studioAudio.source) {
            window.__studioAudio.source = audioContext.createMediaElementSource(audio);

            window.__studioAudio.source.connect(audioContext.destination);
        }

        try { window.__studioAudio.source.disconnect(analyser); } catch (_) { }
        window.__studioAudio.source.connect(analyser);

        const freqData = new Uint8Array(analyser.frequencyBinCount);

        window.__studioAudio.ctx = audioContext;
        window.__studioAudio.analyser = analyser;


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



    if (!window.__initialProject?.settingsJson) {
        updateFractalConfig({
            motion: { bassStrength: 2.5 },
        });
    }


    if (window.__initialProject?.settingsJson) {
        let saved = null;

        try {
            saved = (typeof window.__initialProject.settingsJson === "string")
                ? JSON.parse(window.__initialProject.settingsJson)
                : window.__initialProject.settingsJson;
        } catch (e) {
            console.error("Failed to parse initial settingsJson:", e, window.__initialProject.settingsJson);
        }

        if (saved) {
            applySavedSettingsToUI(saved);

            updateFractalConfig(savedToPartialConfig(saved));
        }
    }



    // Render fractal immediately
    cleanup = window.initVisualizer(fractalSelect.value, "fractal-container");

    //   When the audio starts playing:
    // Any old visualizer is cleaned up
    // initVisualizer is called with the selected fractal type
    if (audio) {
        audio.addEventListener("play", () => {
            if (window.__isRecordingVideo) return;
            if (cleanup) cleanup();
            cleanup = window.initVisualizer(fractalSelect.value, "fractal-container");
        });
    }

    //   When the fractal type dropdown changes:
    // Old visualizer is cleaned up
    // New visualizer is initialized
    fractalSelect?.addEventListener("change", (ev) => {
        const type = ev.target.value;
        updateFractalConfig({ fractalType: type }); // keep state in sync

        if (cleanup) cleanup();
        cleanup = window.initVisualizer(type, "fractal-container");
    });



    // Before leaving the page, cleanup the visualizer
    window.addEventListener("beforeunload", () => {
        if (cleanup) cleanup();
    });
});
