/// Credit glennpow, Zarlang
/// Sourced from - http://forum.unity3d.com/threads/free-script-particle-systems-in-ui-screen-space-overlay.406862/
/// Updated by Zarlang with a more robust implementation, including TextureSheet animation support

namespace UnityEngine.UI.Extensions.CasualGame
{
#if UNITY_5_3_OR_NEWER

    [ExecuteInEditMode]
    [RequireComponent(typeof(CanvasRenderer), typeof(ParticleSystem))]
    [AddComponentMenu("UI/Effects/Extensions/UIParticleSystem")]
    public class UIParticleSystem : MaskableGraphic
    {
        [Tooltip("Having this enabled runs the system in LateUpdate rather than in Update, making it faster but less precise (more clunky)")]
        public bool fixedTime = true;

        [Tooltip("Enables 3D rotation for the particles")]
        public bool use3dRotation = false;

        private Transform _transform;
        private ParticleSystem pSystem;
        private ParticleSystem.Particle[] particles;

        private UIVertex[] _quad = new UIVertex[4];

        private Vector4 imageUV = Vector4.zero;

        private ParticleSystem.TextureSheetAnimationModule textureSheetAnimation;
        private int textureSheetAnimationFrames;
        private Vector2 textureSheetAnimationFrameSize;

        private ParticleSystemRenderer pRenderer;

        private bool isInitialised = false;

        private Material currentMaterial;
        private Texture currentTexture;

#if UNITY_5_5_OR_NEWER
        private ParticleSystem.MainModule mainModule;
#endif

        public override Texture mainTexture
        {
            get
            {
                return currentTexture;
            }
        }

        protected bool Initialize()
        {
            // Initialize Transform
            if (_transform == null)
            {
                _transform = transform;
            }

            // Initialize Particle System
            if (pSystem == null)
            {
                pSystem = GetComponent<ParticleSystem>();

                if (pSystem == null)
                {
                    return false;
                }

#if UNITY_5_5_OR_NEWER

                mainModule = pSystem.main;

                if (mainModule.maxParticles > 14000)
                {
                    mainModule.maxParticles = 14000;
                }

#else

                if (pSystem.maxParticles > 14000)
                {
                    pSystem.maxParticles = 14000;
                }

#endif

                // Disable normal ParticleSystemRenderer
                pRenderer = pSystem.GetComponent<ParticleSystemRenderer>();

                if (pRenderer != null)
                {
                    pRenderer.enabled = false;
                }

                // Create UI particle material if needed
                if (material == null)
                {
                    Shader foundShader = Shader.Find("UI Extensions/Particles/Additive");

                    if (foundShader != null)
                    {
                        material = new Material(foundShader);
                    }
                }

                currentMaterial = material;

                if (currentMaterial != null && currentMaterial.HasProperty("_MainTex"))
                {
                    currentTexture = currentMaterial.mainTexture;

                    if (currentTexture == null)
                    {
                        currentTexture = Texture2D.whiteTexture;
                    }
                }

                material = currentMaterial;

                // Automatically set scaling
#if UNITY_5_5_OR_NEWER
                mainModule.scalingMode = ParticleSystemScalingMode.Hierarchy;
#else
                pSystem.scalingMode = ParticleSystemScalingMode.Hierarchy;
#endif
            }

            // Make absolutely sure particle array exists
#if UNITY_5_5_OR_NEWER
            int maxParticles = pSystem.main.maxParticles;
#else
            int maxParticles = pSystem.maxParticles;
#endif

            if (maxParticles < 1)
            {
                maxParticles = 1;
            }

            if (particles == null || particles.Length < maxParticles)
            {
                particles = new ParticleSystem.Particle[maxParticles];
            }

            // Prepare UV
            imageUV = new Vector4(0, 0, 1, 1);

            // Prepare texture sheet animation
            textureSheetAnimation = pSystem.textureSheetAnimation;

            textureSheetAnimationFrames = 0;
            textureSheetAnimationFrameSize = Vector2.zero;

            if (textureSheetAnimation.enabled)
            {
                textureSheetAnimationFrames =
                    textureSheetAnimation.numTilesX *
                    textureSheetAnimation.numTilesY;

                if (textureSheetAnimation.numTilesX > 0 &&
                    textureSheetAnimation.numTilesY > 0)
                {
                    textureSheetAnimationFrameSize =
                        new Vector2(
                            1f / textureSheetAnimation.numTilesX,
                            1f / textureSheetAnimation.numTilesY
                        );
                }
            }

            return particles != null;
        }

        protected override void Awake()
        {
            base.Awake();

            if (!Initialize())
            {
                enabled = false;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            Initialize();

            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!Initialize())
                {
                    return;
                }
            }
#endif

            vh.Clear();

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            // Safety check
            if (pSystem == null)
            {
                if (!Initialize())
                {
                    return;
                }
            }

            // Safety check for particle array
#if UNITY_5_5_OR_NEWER
            int maxParticles = pSystem.main.maxParticles;
#else
            int maxParticles = pSystem.maxParticles;
#endif

            if (maxParticles < 1)
            {
                return;
            }

            if (particles == null || particles.Length < maxParticles)
            {
                particles = new ParticleSystem.Particle[maxParticles];
            }

            if (particles == null || particles.Length == 0)
            {
                return;
            }

            if (!isInitialised)
            {
#if UNITY_5_5_OR_NEWER
                if (!pSystem.main.playOnAwake)
#else
                if (!pSystem.playOnAwake)
#endif
                {
                    pSystem.Stop(
                        false,
                        ParticleSystemStopBehavior.StopEmittingAndClear
                    );

                    isInitialised = true;
                }
            }

            // Get particles
            int count = pSystem.GetParticles(particles);

            for (int i = 0; i < count; ++i)
            {
                ParticleSystem.Particle particle = particles[i];

                // Get particle position
                Vector2 position;

#if UNITY_5_5_OR_NEWER
                position =
                    (mainModule.simulationSpace == ParticleSystemSimulationSpace.Local
                        ? particle.position
                        : _transform.InverseTransformPoint(particle.position));
#else
                position =
                    (pSystem.simulationSpace == ParticleSystemSimulationSpace.Local
                        ? particle.position
                        : _transform.InverseTransformPoint(particle.position));
#endif

                float rotation = -particle.rotation * Mathf.Deg2Rad;
                float rotation90 = rotation + Mathf.PI / 2f;

                Color32 color = particle.GetCurrentColor(pSystem);

                float size = particle.GetCurrentSize(pSystem) * 0.5f;

                // Apply scale
#if UNITY_5_5_OR_NEWER
                if (mainModule.scalingMode == ParticleSystemScalingMode.Shape)
                {
                    position /= canvas.scaleFactor;
                }
#else
                if (pSystem.scalingMode == ParticleSystemScalingMode.Shape)
                {
                    position /= canvas.scaleFactor;
                }
#endif

                // Texture sheet animation
                Vector4 particleUV = imageUV;

                if (textureSheetAnimation.enabled &&
                    textureSheetAnimationFrames > 0 &&
                    textureSheetAnimation.numTilesX > 0 &&
                    textureSheetAnimation.numTilesY > 0)
                {
                    float frameProgress;

                    if (particle.startLifetime > 0f)
                    {
                        frameProgress =
                            1f -
                            (particle.remainingLifetime /
                             particle.startLifetime);
                    }
                    else
                    {
                        frameProgress = 0f;
                    }

#if UNITY_5_5_OR_NEWER

                    if (textureSheetAnimation.frameOverTime.curveMin != null)
                    {
                        frameProgress =
                            textureSheetAnimation.frameOverTime.curveMin.Evaluate(
                                frameProgress
                            );
                    }
                    else if (textureSheetAnimation.frameOverTime.curve != null)
                    {
                        frameProgress =
                            textureSheetAnimation.frameOverTime.curve.Evaluate(
                                frameProgress
                            );
                    }
                    else if (textureSheetAnimation.frameOverTime.constant > 0)
                    {
                        frameProgress =
                            textureSheetAnimation.frameOverTime.constant -
                            (particle.remainingLifetime /
                             particle.startLifetime);
                    }

#else

                    frameProgress =
                        1f -
                        (particle.lifetime /
                         particle.startLifetime);

#endif

                    frameProgress = Mathf.Repeat(
                        frameProgress *
                        textureSheetAnimation.cycleCount,
                        1f
                    );

                    int frame = 0;

                    switch (textureSheetAnimation.animation)
                    {
                        case ParticleSystemAnimationType.WholeSheet:

                            frame = Mathf.FloorToInt(
                                frameProgress *
                                textureSheetAnimationFrames
                            );

                            break;

                        case ParticleSystemAnimationType.SingleRow:

                            frame = Mathf.FloorToInt(
                                frameProgress *
                                textureSheetAnimation.numTilesX
                            );

                            int row = textureSheetAnimation.rowIndex;

                            frame +=
                                row *
                                textureSheetAnimation.numTilesX;

                            break;
                    }

                    frame %= textureSheetAnimationFrames;

                    particleUV.x =
                        (frame % textureSheetAnimation.numTilesX) *
                        textureSheetAnimationFrameSize.x;

                    particleUV.y =
                        1f -
                        Mathf.FloorToInt(
                            frame /
                            textureSheetAnimation.numTilesX
                        ) *
                        textureSheetAnimationFrameSize.y;

                    particleUV.z =
                        particleUV.x +
                        textureSheetAnimationFrameSize.x;

                    particleUV.w =
                        particleUV.y +
                        textureSheetAnimationFrameSize.y;
                }

                Vector2 temp = Vector2.zero;

                // Vertex 0
                temp.x = particleUV.x;
                temp.y = particleUV.y;

                _quad[0] = UIVertex.simpleVert;
                _quad[0].color = color;
                _quad[0].uv0 = temp;

                // Vertex 1
                temp.x = particleUV.x;
                temp.y = particleUV.w;

                _quad[1] = UIVertex.simpleVert;
                _quad[1].color = color;
                _quad[1].uv0 = temp;

                // Vertex 2
                temp.x = particleUV.z;
                temp.y = particleUV.w;

                _quad[2] = UIVertex.simpleVert;
                _quad[2].color = color;
                _quad[2].uv0 = temp;

                // Vertex 3
                temp.x = particleUV.z;
                temp.y = particleUV.y;

                _quad[3] = UIVertex.simpleVert;
                _quad[3].color = color;
                _quad[3].uv0 = temp;

                // Position vertices
                if (rotation == 0f)
                {
                    Vector2 corner1 = Vector2.zero;
                    Vector2 corner2 = Vector2.zero;

                    corner1.x = position.x - size;
                    corner1.y = position.y - size;

                    corner2.x = position.x + size;
                    corner2.y = position.y + size;

                    temp.x = corner1.x;
                    temp.y = corner1.y;

                    _quad[0].position = temp;

                    temp.x = corner1.x;
                    temp.y = corner2.y;

                    _quad[1].position = temp;

                    temp.x = corner2.x;
                    temp.y = corner2.y;

                    _quad[2].position = temp;

                    temp.x = corner2.x;
                    temp.y = corner1.y;

                    _quad[3].position = temp;
                }
                else
                {
                    if (use3dRotation)
                    {
#if UNITY_5_5_OR_NEWER
                        Vector3 pos3d =
                            (mainModule.simulationSpace ==
                             ParticleSystemSimulationSpace.Local
                                ? particle.position
                                : _transform.InverseTransformPoint(
                                    particle.position
                                ));
#else
                        Vector3 pos3d =
                            (pSystem.simulationSpace ==
                             ParticleSystemSimulationSpace.Local
                                ? particle.position
                                : _transform.InverseTransformPoint(
                                    particle.position
                                ));
#endif

#if UNITY_5_5_OR_NEWER
                        if (mainModule.scalingMode ==
                            ParticleSystemScalingMode.Shape)
                        {
                            pos3d /= canvas.scaleFactor;
                        }
#else
                        if (pSystem.scalingMode ==
                            ParticleSystemScalingMode.Shape)
                        {
                            pos3d /= canvas.scaleFactor;
                        }
#endif

                        Vector3[] verts =
                        {
                            new Vector3(-size, -size, 0),
                            new Vector3(-size, size, 0),
                            new Vector3(size, size, 0),
                            new Vector3(size, -size, 0)
                        };

                        Quaternion particleRotation =
                            Quaternion.Euler(particle.rotation3D);

                        _quad[0].position =
                            pos3d +
                            particleRotation *
                            verts[0];

                        _quad[1].position =
                            pos3d +
                            particleRotation *
                            verts[1];

                        _quad[2].position =
                            pos3d +
                            particleRotation *
                            verts[2];

                        _quad[3].position =
                            pos3d +
                            particleRotation *
                            verts[3];
                    }
                    else
                    {
                        Vector2 right =
                            new Vector2(
                                Mathf.Cos(rotation),
                                Mathf.Sin(rotation)
                            ) *
                            size;

                        Vector2 up =
                            new Vector2(
                                Mathf.Cos(rotation90),
                                Mathf.Sin(rotation90)
                            ) *
                            size;

                        _quad[0].position =
                            position -
                            right -
                            up;

                        _quad[1].position =
                            position -
                            right +
                            up;

                        _quad[2].position =
                            position +
                            right +
                            up;

                        _quad[3].position =
                            position +
                            right -
                            up;
                    }
                }

                vh.AddUIVertexQuad(_quad);
            }
        }

        private void Update()
        {
            if (!fixedTime && Application.isPlaying)
            {
                if (pSystem == null)
                {
                    if (!Initialize())
                    {
                        return;
                    }
                }

                pSystem.Simulate(
                    Time.unscaledDeltaTime,
                    false,
                    false,
                    true
                );

                SetAllDirty();

                CheckMaterial();
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                SetAllDirty();
            }
            else
            {
                if (fixedTime)
                {
                    if (pSystem == null)
                    {
                        if (!Initialize())
                        {
                            return;
                        }
                    }

                    pSystem.Simulate(
                        Time.unscaledDeltaTime,
                        false,
                        false,
                        true
                    );

                    SetAllDirty();

                    CheckMaterial();
                }
            }

            // Material may have changed
            if (material != currentMaterial)
            {
                Initialize();
            }
        }

        private void CheckMaterial()
        {
            if (pSystem == null)
            {
                return;
            }

            if (currentMaterial != null &&
                currentTexture != currentMaterial.mainTexture)
            {
                Initialize();
                return;
            }

            if (material != null &&
                currentMaterial != null &&
                material.shader != currentMaterial.shader)
            {
                Initialize();
            }
        }

        protected override void OnDestroy()
        {
            currentMaterial = null;
            currentTexture = null;
            particles = null;
            pSystem = null;

            base.OnDestroy();
        }

        public void StartParticleEmission()
        {
            if (pSystem == null)
            {
                if (!Initialize())
                {
                    return;
                }
            }

            pSystem.Play();
        }

        public void StopParticleEmission()
        {
            if (pSystem == null)
            {
                return;
            }

            pSystem.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        public void PauseParticleEmission()
        {
            if (pSystem == null)
            {
                return;
            }

            pSystem.Stop(
                false,
                ParticleSystemStopBehavior.StopEmitting
            );
        }
    }

#endif
}