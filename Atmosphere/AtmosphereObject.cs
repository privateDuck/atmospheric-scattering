using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StormAtmosphere
{

    [CreateAssetMenu(fileName = "Atmosphere Settings", menuName = "Effects/Atmosphere Settings")]
    public class AtmosphereObject : ScriptableObject
    {
        public bool EnableAtmosphere = false;
        public SkyType skyType = SkyType.SkyBox;

        [Space]
        [Header("Planet Properties")]
        public float planetRadius;
        public float atmosphereThickness;
        public Vector3 planetCenter;
        public bool includePlanetShadow = false;
        public Color nightSkyTint;
        [Range(0, 1)] public float nightSkyTintWeight;
        [Space]
        [Header("Rayleigh Scattering")]
        [HideInInspector] public Vector3 rayleighCoef;
        public Vector3 wavelengthsRGB = new Vector3(700, 530, 460);
        public float wavelengthScale = 300f;
        [Range(0, 1)] public float rayleighDensityAvg = 0.1f;

        [Space]
        [Header("Mie Scattering")]
        [Range(0, 1)] public float mieDensityAvg = 0.1f;
        public float mieCoef;
        public float mieAbsorption;

        [Space]
        [Header("Ozone")]
        [Range(0, 1)] public float ozonePeakAltitude = 0.25f;
        [Range(0, 10)] public float ozoneDensityFalloff = 4;
        [Range(0, 5)] public float ozoneStrenght = 1;
        public Vector3 ozoneAbsorption;

        [Space]
        [Header("Sun")]
        public float sunSize;
        public float sunBlurA;
        public float sunBlurB;

        [Space]
        [Header("Compute Shaders")]
        public ComputeShader transmittanceCompute;
        public ComputeShader raymarchCompute;

        [Space]

        [Tooltip("Resolution of the sky texture: (Default: 256,256)")]
        public Vector2Int skyTextureResolution;

        [Tooltip("Number of raymarching steps used to compute the sky texture: (Default: 100)")]
        public int skyScatteringSteps;

        [Space]

        [Tooltip("Resolution of the fog texture: (Default: 32)")]
        public int fogTextureResolution = 32;

        [Tooltip("Number of raymarching steps used to compute the fog texture: (Default: 20)")]
        public int fogScatteringSteps = 20;

        [Range(0, 1)] public float fogStrength = 0.5f;

        [Space]

        [Tooltip("Resolution of the sun transmittance texture: (Default: 128)")]
        public int sunTransmittanceResolution = 128;

        [Tooltip("Number of raymarching steps used to compute the sun transmittance texture: (Default: 50)")]
        public int sunTransmittanceSteps;

        [Space]
        [Header("Extra")]
        public Texture2D ditherTexture;
        [Range(0, 1)] public float ditherStrength;
        [HideInInspector] public RenderTexture transmittanceLUT;
        [HideInInspector] public RenderTexture skyLUT;
        [HideInInspector] public RenderTexture aerialLuminance;
        [HideInInspector] public RenderTexture aerialTransmittance;
        private ComputeShader raymarchComputeInstance;
        public ComputeShader transmittanceComputeInstance;
        public bool UpdateRequired { get; set; }

        private Vector3 sunDirection;
        public bool autoUpdateShaderParameters;
        public bool requiresInitialization = true;
        private int SkyFrustrumKernel, SkySphereKernel, FogFrustrumKernel;
        private int SkyKernel;

        #region Public Accessors
        public Transform Sun { get; set; }
        public Vector2Int SkyTextureSize
        {
            get
            {
                return skyTextureResolution;
            }
            set
            {
                skyTextureResolution = value;
                InitSkyTexture();
            }
        }

        public int FogTextureSize
        {
            get
            {
                return fogTextureResolution;
            }
            set
            {
                fogTextureResolution = value;
                InitSlicedTextures();
            }
        }

        public int TransmittanceTextureSize
        {
            get
            {
                return sunTransmittanceResolution;
            }
            set
            {
                sunTransmittanceResolution = value;
                InitAndComputeTransmittance();
            }
        }
        #endregion

        #region Public Methods

        /// <summary> Complete Initialization </summary>
        /// <summary> Creates textures and initializes the compute shaders </summary>
        public void InitAtmosphere()
        {
            raymarchComputeInstance = Instantiate(raymarchCompute);
            transmittanceComputeInstance = Instantiate(transmittanceCompute);
            SkyFrustrumKernel = raymarchComputeInstance.FindKernel("SkyFrustrum");
            SkySphereKernel = raymarchComputeInstance.FindKernel("SkySphere");
            FogFrustrumKernel = raymarchComputeInstance.FindKernel("FogFrustrum");
            SkyKernel = (skyType == SkyType.SkyBox) ? SkySphereKernel : SkyFrustrumKernel;
            UpdateShaderParams();

            InitAndComputeTransmittance();
            InitSlicedTextures();
            InitSkyTexture();
        }

        /// <summary> Partial Initialization. Initializes the compute shaders</summary>
        private void PartialInit()
        {
            SkyKernel = (skyType == SkyType.SkyBox) ? SkySphereKernel : SkyFrustrumKernel;
            UpdateShaderParams();
            transmittanceComputeInstance.Dispatch(0, sunTransmittanceResolution / 8, sunTransmittanceResolution / 8, 1);
            requiresInitialization = false;
        }

        public void CleanUp()
        {
            DisposeRTs();
        }

        public void UpdateAtmosphere(ref CommandBuffer cmb, Camera camera)
        {
            sunDirection = -Sun.forward;

            if (requiresInitialization)
            {
                PartialInit();
            }

            if (EnableAtmosphere)
            {
                RayMarchParams(ref raymarchComputeInstance, camera);
                raymarchComputeInstance.SetVector(ShaderID.sunDirID, sunDirection);
                UpdateSkyTexture(ref cmb);
                UpdateAerialPerspective(ref cmb, camera);
            }
        }

        public void UpdateAtmosphere(Camera camera)
        {
            sunDirection = -Sun.forward;

            if (requiresInitialization)
            {
                PartialInit();
            }

            if (EnableAtmosphere)
            {

                RayMarchParams(ref raymarchComputeInstance, camera);
                raymarchComputeInstance.SetVector(ShaderID.sunDirID, sunDirection);
                UpdateSkyTexture();
                UpdateAerialPerspective(camera);
            }
        }

        public void UpdateShaderParams()
        {
            ApplyShaderParams(raymarchComputeInstance);
            ApplyShaderParams(transmittanceComputeInstance);
        }

        public void InitAndComputeTransmittance()
        {
            CreateRT(ref transmittanceLUT, GraphicsFormat.R16G16B16A16_SFloat, sunTransmittanceResolution, sunTransmittanceResolution);

            transmittanceComputeInstance.SetTexture(0, "transmittanceLUT", transmittanceLUT);
            transmittanceComputeInstance.SetInt("size", sunTransmittanceResolution);
            transmittanceComputeInstance.Dispatch(0, sunTransmittanceResolution / 8, sunTransmittanceResolution / 8, 1);
        }


        public void InitSkyTexture()
        {
            CreateRT(ref skyLUT, GraphicsFormat.R32G32B32A32_SFloat, skyTextureResolution.x, skyTextureResolution.y, 1);

            raymarchComputeInstance.SetTexture(SkyKernel, "transmittanceLUT", transmittanceLUT);
            raymarchComputeInstance.SetTexture(SkyKernel, "sky", skyLUT);
            raymarchComputeInstance.SetInt("skyScatteringSteps", skyScatteringSteps);
            raymarchComputeInstance.SetInts("skyTexSize", skyTextureResolution.x, skyTextureResolution.y);
            raymarchComputeInstance.SetVector("dirToSun", sunDirection);
        }

        public void InitSlicedTextures()
        {
            CreateRT3D(ref aerialLuminance, GraphicsFormat.R16G16B16A16_SFloat, fogTextureResolution, fogTextureResolution, fogTextureResolution);
            CreateRT3D(ref aerialTransmittance, GraphicsFormat.R16G16B16A16_UNorm, fogTextureResolution, fogTextureResolution, fogTextureResolution);

            raymarchComputeInstance.SetTexture(FogFrustrumKernel, "luminanceAtDepth", aerialLuminance);
            raymarchComputeInstance.SetTexture(FogFrustrumKernel, "transmittanceAtDepth", aerialTransmittance);
            raymarchComputeInstance.SetTexture(FogFrustrumKernel, ShaderID.transmittanceLUTID, transmittanceLUT);

            raymarchComputeInstance.SetInt("fogTexSize", fogTextureResolution);
            raymarchComputeInstance.SetInt("fogScatteringSteps", fogScatteringSteps);
        }

        public void UpdateAerialPerspective(ref CommandBuffer cmb, Camera camera)
        {
            raymarchComputeInstance.SetFloat(ShaderID.nearClipID, camera.nearClipPlane);
            raymarchComputeInstance.SetFloat(ShaderID.farClipID, camera.farClipPlane);
            cmb.DispatchCompute(raymarchComputeInstance, FogFrustrumKernel, fogTextureResolution / 8, fogTextureResolution / 8, fogTextureResolution / 8);
        }

        public void UpdateSkyTexture(ref CommandBuffer cmb)
        {
            cmb.DispatchCompute(raymarchComputeInstance, SkyKernel, skyTextureResolution.x / 8, skyTextureResolution.y / 8, 1);
        }

        public void UpdateAerialPerspective(Camera camera)
        {
            raymarchComputeInstance.SetFloat(ShaderID.nearClipID, camera.nearClipPlane);
            raymarchComputeInstance.SetFloat(ShaderID.farClipID, camera.farClipPlane);
            raymarchComputeInstance.Dispatch(FogFrustrumKernel, fogTextureResolution / 4, fogTextureResolution / 4, fogTextureResolution / 4);
        }

        public void UpdateSkyTexture()
        {
            raymarchComputeInstance.Dispatch(SkyKernel, skyTextureResolution.x / 8, skyTextureResolution.y / 8, 1);
        }

        public void InitMaterial(Material material)
        {
            material.SetTexture(ShaderID.transmittanceLUTID, transmittanceLUT);
            material.SetFloat("sunDiscSize", sunSize);
            material.SetFloat("sunDiscBlurA", sunBlurA);
            material.SetFloat("sunDiscBlurB", sunBlurB);
            material.SetFloat("planetRadius", planetRadius);
            material.SetFloat("atmosphereThickness", atmosphereThickness);
            material.SetVector("planetCenter", planetCenter);
            material.SetFloat("ditherStrength", ditherStrength);
            material.SetTexture("blueNoise", ditherTexture);
            material.SetVector("nightColor", nightSkyTint);
            material.SetFloat("nightColorWeight", nightSkyTintWeight);
        }

        #endregion

        #region Private Methods
        private void InitRenderTextures()
        {
            CreateRT(ref transmittanceLUT, GraphicsFormat.R16G16B16A16_SFloat, sunTransmittanceResolution, sunTransmittanceResolution);
            CreateRT(ref skyLUT, GraphicsFormat.R32G32B32A32_SFloat, skyTextureResolution.x, skyTextureResolution.y, 3);
            CreateRT3D(ref aerialLuminance, GraphicsFormat.R16G16B16A16_SFloat, fogTextureResolution, fogTextureResolution, fogTextureResolution);
            CreateRT3D(ref aerialTransmittance, GraphicsFormat.R16G16B16A16_UNorm, fogTextureResolution, fogTextureResolution, fogTextureResolution);
        }


        private void RayMarchParams(ref ComputeShader compute, Camera camera)
        {
            Vector3 topLeftDir = CalculateViewDirection(camera, new Vector2(0, 1));
            Vector3 topRightDir = CalculateViewDirection(camera, new Vector2(1, 1));
            Vector3 bottomLeftDir = CalculateViewDirection(camera, new Vector2(0, 0));
            Vector3 bottomRightDir = CalculateViewDirection(camera, new Vector2(1, 0));

            compute.SetVector(ShaderID.topLeftDirID, topLeftDir);
            compute.SetVector(ShaderID.topRightDirID, topRightDir);
            compute.SetVector(ShaderID.bottomLeftDirID, bottomLeftDir);
            compute.SetVector(ShaderID.bottomRightDirID, bottomRightDir);
            compute.SetVector(ShaderID.cameraPositionID, camera.transform.position);
        }

        private Vector3 CalculateViewDirection(Camera camera, in Vector2 texcoord)
        {
            Matrix4x4 camInvMat = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true).inverse;
            Matrix4x4 locToWolMat = camera.transform.localToWorldMatrix;

            Vector3 viewVec = camInvMat * new Vector4(texcoord.x * 2 - 1, texcoord.y * 2 - 1, 0, -1);
            viewVec = locToWolMat * new Vector4(viewVec.x, viewVec.y, viewVec.z, 0);
            return viewVec.normalized;
        }

        private void ApplyShaderParams(ComputeShader computeShader)
        {
            computeShader.SetVector("planetCenter", planetCenter);
            computeShader.SetFloat("atmosphereThickness", atmosphereThickness);
            computeShader.SetFloat("atmosphereRadius", (planetRadius + atmosphereThickness));
            computeShader.SetFloat("planetRadius", planetRadius);

            computeShader.SetFloat("rayleighDensityAvg", rayleighDensityAvg);

            Vector3 invWavelengths = new Vector3(1 / wavelengthsRGB.x, 1 / wavelengthsRGB.y, 1 / wavelengthsRGB.z);
            rayleighCoef = Pow(invWavelengths * wavelengthScale, 4);

            computeShader.SetVector("rayleighCoefficients", rayleighCoef);

            computeShader.SetFloat("mieCoefficient", mieCoef);
            computeShader.SetFloat("mieDensityAvg", mieDensityAvg);
            computeShader.SetFloat("mieAbsorption", mieAbsorption);

            computeShader.SetFloat("ozonePeakDensityAltitude", ozonePeakAltitude);
            computeShader.SetFloat("ozoneDensityFalloff", ozoneDensityFalloff);
            computeShader.SetVector("ozoneAbsorption", (ozoneAbsorption * ozoneStrenght * 0.1f));
            computeShader.SetBool(ShaderID.includePlanetShadowID, includePlanetShadow);
        }

        private void CreateRT(ref RenderTexture rt, GraphicsFormat format, int w, int h, int mips = 1)
        {
            rt = new RenderTexture(w, h, 0, format, mips);
            rt.enableRandomWrite = true;

            rt.useMipMap = true;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Trilinear;
            rt.Create();
        }

        private void CreateRT3D(ref RenderTexture rt, GraphicsFormat format, int width, int height, int depth)
        {
            rt = new RenderTexture(width, height, 0, format);
            rt.enableRandomWrite = true;
            rt.volumeDepth = depth;
            rt.dimension = TextureDimension.Tex3D;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.filterMode = FilterMode.Bilinear;
            rt.Create();
        }
        private void DisposeRTs()
        {
            transmittanceLUT?.Release();
            skyLUT?.Release();
            aerialLuminance?.Release();
            aerialTransmittance?.Release();
        }

        private Vector3 Pow(Vector3 vector, float power)
        {
            return new Vector3(Mathf.Pow(vector.x, power), Mathf.Pow(vector.y, power), Mathf.Pow(vector.z, power));
        }

        private void OnValidate()
        {
            UpdateRequired = true;
            requiresInitialization = true;
        }
        #endregion

        public enum SkyType
        {
            SkyBox = 0, Frustrum
        }
        private static class ShaderID
        {
            public static int transmittanceLUTID = Shader.PropertyToID("transmittanceLUT");
            public static int skyViewID = Shader.PropertyToID("sky");
            public static int widthID = Shader.PropertyToID("width");
            public static int heightID = Shader.PropertyToID("height");
            public static int sunDirID = Shader.PropertyToID("dirToSun");
            public static int numScatteringStepsID = Shader.PropertyToID("numScatteringSteps");
            public static int topLeftDirID = Shader.PropertyToID("topLeftDir");
            public static int topRightDirID = Shader.PropertyToID("topRightDir");
            public static int bottomLeftDirID = Shader.PropertyToID("bottomLeftDir");
            public static int bottomRightDirID = Shader.PropertyToID("bottomRightDir");
            public static int cameraPositionID = Shader.PropertyToID("camPos");
            public static int nearClipID = Shader.PropertyToID("nearClip");
            public static int farClipID = Shader.PropertyToID("farClip");
            public static int includePlanetShadowID = Shader.PropertyToID("includePlanetShadow");
        }

#if UNITY_EDITOR

        [CustomEditor(typeof(AtmosphereObject))]
        public class AtmosphereObjectEditor : Editor
        {
            public enum TextureType { Transmittance, SkyTexture, AerialLuminance, AerialTransmittance }

            public override void OnInspectorGUI()
            {
                AtmosphereObject settings = (AtmosphereObject)target;

                if (GUILayout.Button("Init"))
                {
                    settings.Sun = GameObject.FindGameObjectWithTag("Sun").transform;
                    settings.InitAtmosphere();
                }

                if (GUILayout.Button("Update"))
                {
                    settings.UpdateAtmosphere(Camera.main);
                }

                EditorGUILayout.Space();
                if (GUILayout.Button("Show Texture Viewer"))
                {
                    var window = EditorWindow.GetWindow<AtmTexViewerWindow>();
                    window.titleContent = new GUIContent("Texture Viewer");
                    window.settings = settings;
                    window.maxSize = new Vector2(400, 400);
                    window.Show();
                }

                EditorGUILayout.Space();
                DrawDefaultInspector();
            }
        }
        public class AtmTexViewerWindow : EditorWindow
        {
            private RenderTexture m_temp;
            private AtmosphereObjectEditor.TextureType textureType;
            private int slice3D;
            public AtmosphereObject settings;
            void OnGUI()
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Texture Type", GUILayout.Width(120));
                textureType = (AtmosphereObjectEditor.TextureType)EditorGUILayout.EnumPopup(textureType);
                EditorGUILayout.EndHorizontal();

                if (textureType == AtmosphereObjectEditor.TextureType.Transmittance)
                {
                    m_temp = settings.transmittanceLUT;
                }
                else if (textureType == AtmosphereObjectEditor.TextureType.SkyTexture)
                {
                    m_temp = settings.skyLUT;
                }
                else if (textureType == AtmosphereObjectEditor.TextureType.AerialLuminance)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Texture Slice", GUILayout.Width(120));
                    slice3D = (int)EditorGUILayout.Slider((int)slice3D, 0f, settings.fogTextureResolution - 1);
                    EditorGUILayout.EndHorizontal();

                    if (settings.aerialLuminance)
                    {
                        m_temp = new RenderTexture(settings.fogTextureResolution, settings.fogTextureResolution, 0, settings.aerialLuminance.format);
                        Graphics.CopyTexture(settings.aerialLuminance, slice3D, m_temp, 0);
                    }
                    else
                        EditorGUILayout.HelpBox("Texture is not generated!", MessageType.Warning);
                }
                else if (textureType == AtmosphereObjectEditor.TextureType.AerialTransmittance)
                {

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Texture Slice", GUILayout.Width(120));
                    slice3D = (int)EditorGUILayout.Slider((int)slice3D, 0f, settings.fogTextureResolution - 1);
                    EditorGUILayout.EndHorizontal();

                    if (settings.aerialTransmittance)
                    {
                        m_temp = new RenderTexture(settings.fogTextureResolution, settings.fogTextureResolution, 0, settings.aerialTransmittance.format);
                        Graphics.CopyTexture(settings.aerialTransmittance, slice3D, m_temp, 0);
                    }
                    else
                        EditorGUILayout.HelpBox("Texture is not generated!", MessageType.Warning);

                }
                if (m_temp)
                {
                    EditorGUI.DrawPreviewTexture(
                        new Rect(new Vector2(10, 50), Vector2.one * 300),
                        m_temp);
                }
                else
                {
                    EditorGUILayout.HelpBox("Texture is not generated!", MessageType.Warning);
                }
            }
        }
#endif
    }
}