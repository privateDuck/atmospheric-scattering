using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Fleet Commander/Atmosphere Settings", fileName = "New Atmosphere")]
public class AtmosphereSettings : ScriptableObject
{
    public Vector3 rayleighCoef;
    public float planetRadius;
    public float atmosphereThickness;
    public Vector3 planetCenter;

    int aerialScatteringSteps = 20;

    [Header("Rayleigh Scattering")]

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
    [Header("Transmittance LUT")]
    public ComputeShader transmittanceCompute;
    public Vector2Int transmittanceLUTSize;

    [Space]
    [Header("Aerial Perspective LUT")]
    public ComputeShader aerialCompute;
    public int aerialLUTSize;
    [Range(0, 1)] public float aerialPerspectiveStrength = 1;

    [Space]
    [Header("Sky Texture")]
    public int skyViewScattingSteps = 100;
    public ComputeShader skyCompute;
    public Vector2Int skySize;
    [Range(0, 1)] public float skyTransmittanceWeight = 1.0f;

    [Space]
    [Header("Materials")]
    public Material skyMaterial;
    public Material AerialPostMaterial;

    [Header("Test")]
    [HideInInspector] public RenderTexture transmittanceLUT;
    [HideInInspector] public RenderTexture skyLUT;
    [HideInInspector] public RenderTexture aerialLuminance;
    [HideInInspector] public RenderTexture aerialTransmittance;
    public ShaderValues shaderValues;
    public Transform Sun { get; set; }

    public Color GetSunColor
    {
        get
        {
            return GetSunTransmittance(camera.transform.position, sunDirection);
        }
    }
    private Vector3 sunDirection;
    private Camera camera;
    private bool autoUpdateValues;
    private Light sunLight;
    public Texture2D cpuTransmittanceTexture;

    public bool requiresInitialization = true;

    void OnEnable()
    {
        requiresInitialization = true;
    }
    public void OnInitialize()
    {
        ShaderIDs.InitIDs();
        Sun = GameObject.FindGameObjectWithTag("Sun").transform;
        sunLight = Sun.GetComponent<Light>();
        cpuTransmittanceTexture = new Texture2D(transmittanceLUTSize.x, transmittanceLUTSize.y, GraphicsFormat.R16G16B16A16_UNorm, 1, 0);
        camera = Camera.main;
        //camera = Camera.current;
        shaderValues = new ShaderValues();
        UpdateStaticValues();
        InitAndRenderTransmittance();
        InitAearialPerspective();
        InitSkyTexture();
        InitSkyMaterial(skyMaterial);
        InitSkyMaterial(AerialPostMaterial);
        requiresInitialization = false;
    }

    public void UpdateAll(ref CommandBuffer cmb)
    {
        // sunLight.color = GetSunColor;
        if (autoUpdateValues)
        {
            UpdateStaticValues();
        }
        sunDirection = -Sun.forward;
        UpdateAerialPerspective(ref cmb);
        UpdateSkyTexture(ref cmb);
        UpdateMaterials(skyMaterial);
        UpdateMaterials(AerialPostMaterial);
        skyMaterial.SetTexture(ShaderIDs.skyLUTID, skyLUT);
    }

    private void OnDisable()
    {

    }

    private void DebugInit()
    {
        OnInitialize();
    }


    public void UpdateStaticValues()
    {
        sunDirection = -Sun.forward;
        InitStaticShaderValues(shaderValues);
        shaderValues.Apply(skyCompute);
        shaderValues.Apply(transmittanceCompute);
        shaderValues.Apply(aerialCompute);
        shaderValues.Apply(skyMaterial);
        shaderValues.Apply(AerialPostMaterial);
        skyMaterial.mainTexture = skyLUT;
    }

    public Color GetSunTransmittance(Vector3 position, Vector3 sunDir)
    {
        Vector3 sunT = AtmosphericCalculations.getSunTransmittance(position, sunDir, this);
        return new Color(sunT.x, sunT.y, sunT.z);
    }

    public void InitAndRenderTransmittance()
    {
        GraphicsFormat transmittanceLUTFormat = GraphicsFormat.R16G16B16A16_UNorm;
        transmittanceLUT = new RenderTexture(transmittanceLUTSize.x, transmittanceLUTSize.y, 0, transmittanceLUTFormat);
        transmittanceLUT.enableRandomWrite = true;
        transmittanceLUT.wrapMode = TextureWrapMode.Clamp;
        transmittanceLUT.filterMode = FilterMode.Bilinear;
        transmittanceLUT.Create();

        //RayMarchParams(transmittanceCompute);
        transmittanceCompute.SetTexture(0, "transmittanceLUT", transmittanceLUT);
        transmittanceCompute.SetInt("width", transmittanceLUTSize.x);
        transmittanceCompute.SetInt("height", transmittanceLUTSize.y);
        transmittanceCompute.Dispatch(0, transmittanceLUTSize.x / 8, transmittanceLUTSize.y / 8, 1);
    }

    public void InitSkyTexture()
    {
        GraphicsFormat skyFormat = GraphicsFormat.R32G32B32A32_SFloat;
        skyLUT = new RenderTexture(skySize.x, skySize.y, 0, skyFormat);
        skyLUT.enableRandomWrite = true;
        skyLUT.wrapMode = TextureWrapMode.Clamp;
        skyLUT.filterMode = FilterMode.Bilinear;
        skyLUT.Create();

        skyCompute.SetTexture(0, "transmittanceLUT", transmittanceLUT);
        skyCompute.SetTexture(0, "sky", skyLUT);
        skyCompute.SetInt("numScatteringSteps", skyViewScattingSteps);
        skyCompute.SetInts("size", skySize.x, skySize.y);
        skyCompute.SetVector("dirToSun", sunDirection);
        skyCompute.Dispatch(0, skySize.x / 8, skySize.y / 8, 1);
    }

    public void InitAearialPerspective()
    {
        GraphicsFormat LumFormat = GraphicsFormat.R16G16B16A16_SFloat;
        GraphicsFormat transFormat = GraphicsFormat.R16G16B16A16_UNorm;

        aerialLuminance = new RenderTexture(aerialLUTSize, aerialLUTSize, 0, LumFormat);
        aerialLuminance.enableRandomWrite = true;
        aerialLuminance.volumeDepth = aerialLUTSize;
        aerialLuminance.dimension = TextureDimension.Tex3D;
        aerialLuminance.wrapMode = TextureWrapMode.Clamp;
        aerialLuminance.filterMode = FilterMode.Bilinear;
        aerialLuminance.Create();

        aerialTransmittance = new RenderTexture(aerialLUTSize, aerialLUTSize, 0, transFormat);
        aerialTransmittance.enableRandomWrite = true;
        aerialTransmittance.volumeDepth = aerialLUTSize;
        aerialTransmittance.dimension = TextureDimension.Tex3D;
        aerialTransmittance.wrapMode = TextureWrapMode.Clamp;
        aerialTransmittance.filterMode = FilterMode.Bilinear;
        aerialTransmittance.Create();

        aerialCompute.SetTexture(0, "AerialPerspectiveLuminance", aerialLuminance);
        aerialCompute.SetTexture(0, "AerialPerspectiveTransmittance", aerialTransmittance);
        aerialCompute.SetTexture(0, "transmittanceLUT", transmittanceLUT);

        aerialCompute.SetInt("size", aerialLUTSize);
        aerialCompute.SetInt("numScatteringSteps", aerialScatteringSteps);
    }

    public void UpdateAerialPerspective(ref CommandBuffer cmb)
    {
        RayMarchParams(aerialCompute);
        aerialCompute.SetFloat(ShaderIDs.nearClipID, camera.nearClipPlane);
        aerialCompute.SetFloat(ShaderIDs.farClipID, camera.farClipPlane);
        aerialCompute.SetVector(ShaderIDs.dirToSunID, sunDirection);
        cmb.DispatchCompute(aerialCompute, 0, aerialLUTSize / 8, aerialLUTSize / 8, aerialLUTSize / 8);
    }

    public void UpdateSkyTexture(ref CommandBuffer cmb)
    {
        RayMarchParams(skyCompute);
        skyCompute.SetVector(ShaderIDs.dirToSunID, sunDirection);
        cmb.DispatchCompute(skyCompute, 0, skySize.x / 8, skySize.y / 8, 1);
    }

    public void UpdateAerialPerspective()
    {
        RayMarchParams(aerialCompute);
        aerialCompute.SetFloat(ShaderIDs.nearClipID, camera.nearClipPlane);
        aerialCompute.SetFloat(ShaderIDs.farClipID, camera.farClipPlane);
        aerialCompute.SetVector(ShaderIDs.dirToSunID, sunDirection);
        aerialCompute.Dispatch(0, aerialLUTSize / 8, aerialLUTSize / 8, aerialLUTSize / 8);
    }

    public void UpdateSkyTexture()
    {
        RayMarchParams(skyCompute);
        skyCompute.SetVector(ShaderIDs.dirToSunID, sunDirection);
        skyCompute.Dispatch(0, skySize.x / 8, skySize.y / 8, 1);
    }

    public void InitSkyMaterial(Material material)
    {
        material.SetTexture(ShaderIDs.transmittanceLUTID, transmittanceLUT);
        material.SetFloat("sunDiscSize", sunSize);
        material.SetFloat("sunDiscBlurA", sunBlurA);
        material.SetFloat("sunDiscBlurB", sunBlurB);
        //material.SetVector(ShaderIDs.dirToSunID, sunDirection);
        //material.SetVector(ShaderIDs.camPosID, camera.transform.position);
    }

    private void UpdateMaterials(Material material)
    {
        //material.SetVector(ShaderIDs.dirToSunID, sunDirection);
        //material.SetVector(ShaderIDs.camPosID, camera.transform.position);
        material.SetFloat(ShaderIDs.nearClipID, camera.nearClipPlane);
        material.SetFloat(ShaderIDs.farClipID, camera.farClipPlane);
    }

    private void RayMarchParams(ComputeShader compute)
    {
        Vector3 topLeftDir = CalculateViewDirection(camera, new Vector2(0, 1));
        Vector3 topRightDir = CalculateViewDirection(camera, new Vector2(1, 1));
        Vector3 bottomLeftDir = CalculateViewDirection(camera, new Vector2(0, 0));
        Vector3 bottomRightDir = CalculateViewDirection(camera, new Vector2(1, 0));

        compute.SetVector(ShaderIDs.topLeftDirID, topLeftDir);
        compute.SetVector(ShaderIDs.topRightDirID, topRightDir);
        compute.SetVector(ShaderIDs.bottomLeftDirID, bottomLeftDir);
        compute.SetVector(ShaderIDs.bottomRightDirID, bottomRightDir);
        compute.SetVector(ShaderIDs.camPosID, camera.transform.position);
    }

    private Vector3 CalculateViewDirection(Camera cam, in Vector2 texcoord)
    {
        Matrix4x4 camInvMat = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true).inverse;
        Matrix4x4 locToWolMat = cam.transform.localToWorldMatrix;

        Vector3 viewVec = camInvMat * new Vector4(texcoord.x * 2 - 1, texcoord.y * 2 - 1, 0, -1);
        viewVec = locToWolMat * new Vector4(viewVec.x, viewVec.y, viewVec.z, 0);
        return viewVec.normalized;
    }

    private void InitStaticShaderValues(ShaderValues shaderValues)
    {
        shaderValues.floats.Clear();
        shaderValues.ints.Clear();
        shaderValues.vectors.Clear();
        shaderValues.floats.Add(("atmosphereThickness", atmosphereThickness));
        shaderValues.floats.Add(("atmosphereRadius", planetRadius + atmosphereThickness));
        shaderValues.floats.Add(("planetRadius", planetRadius));
        shaderValues.floats.Add(("terrestrialClipDst", planetRadius));

        shaderValues.floats.Add(("rayleighDensityAvg", rayleighDensityAvg));

        Vector3 invWavelengths = new Vector3(1 / wavelengthsRGB.x, 1 / wavelengthsRGB.y, 1 / wavelengthsRGB.z);
        rayleighCoef = Pow(invWavelengths * wavelengthScale, 4);

        shaderValues.vectors.Add(("rayleighCoefficients", rayleighCoef));
        shaderValues.vectors.Add(("planetCenter", planetCenter));

        shaderValues.floats.Add(("mieCoefficient", mieCoef));
        shaderValues.floats.Add(("mieDensityAvg", mieDensityAvg));
        shaderValues.floats.Add(("mieAbsorption", mieAbsorption));

        shaderValues.floats.Add(("ozonePeakDensityAltitude", ozonePeakAltitude));
        shaderValues.floats.Add(("ozoneDensityFalloff", ozoneDensityFalloff));
        shaderValues.vectors.Add(("ozoneAbsorption", ozoneAbsorption * ozoneStrenght * 0.1f));
    }

    private Vector3 Pow(Vector3 vector, float power)
    {
        return new Vector3(Mathf.Pow(vector.x, power), Mathf.Pow(vector.y, power), Mathf.Pow(vector.z, power));
    }

    private static class ShaderIDs
    {
        public static int topLeftDirID;
        public static int topRightDirID;
        public static int bottomLeftDirID;
        public static int bottomRightDirID;
        public static int camPosID;
        public static int dirToSunID;
        public static int transmittanceLUTID;
        public static int skyLUTID;
        public static int nearClipID = Shader.PropertyToID("nearClip");
        public static int farClipID = Shader.PropertyToID("farClip");

        public static void InitIDs()
        {
            topLeftDirID = Shader.PropertyToID("topLeftDir");
            topRightDirID = Shader.PropertyToID("topRightDir");
            bottomLeftDirID = Shader.PropertyToID("bottomLeftDir");
            bottomRightDirID = Shader.PropertyToID("bottomRightDir");
            camPosID = Shader.PropertyToID("camPos");
            dirToSunID = Shader.PropertyToID("dirToSun");
            transmittanceLUTID = Shader.PropertyToID("transmittanceLUT");
            skyLUTID = Shader.PropertyToID("sky");
        }

    }

    public class ShaderValues
    {
        public List<(string name, float value)> floats;
        public List<(string name, int value)> ints;
        public List<(string name, Vector4 value)> vectors;

        public ShaderValues()
        {
            this.floats = new List<(string name, float value)>();
            this.ints = new List<(string name, int value)>();
            this.vectors = new List<(string name, Vector4 value)>();
        }

        public void Apply(Material material)
        {
            foreach (var data in floats)
            {
                material.SetFloat(data.name, data.value);
            }
            foreach (var data in ints)
            {
                material.SetInt(data.name, data.value);
            }
            foreach (var data in vectors)
            {
                material.SetVector(data.name, data.value);
            }
        }

        public void Apply(ComputeShader computeShader)
        {
            foreach (var data in floats)
            {
                computeShader.SetFloat(data.name, data.value);
            }
            foreach (var data in ints)
            {
                computeShader.SetInt(data.name, data.value);
            }
            foreach (var data in vectors)
            {
                computeShader.SetVector(data.name, data.value);
            }
        }
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(AtmosphereSettings))]
    public class AtmosphereSettingsEditor : Editor
    {
        public enum TextureType { Transmittance, SkyTexture, AerialLuminance, AerialTransmittance }

        public override void OnInspectorGUI()
        {
            AtmosphereSettings settings = (AtmosphereSettings)target;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Debug Controls", GUILayout.Width(120));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Auto Update", GUILayout.Width(120));
            settings.autoUpdateValues = EditorGUILayout.Toggle(settings.autoUpdateValues);
            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Manually Initialize"))
            {
                settings.DebugInit();
            }

            if (GUILayout.Button("Manually Update Params"))
            {
                settings.UpdateStaticValues();

                settings.UpdateAerialPerspective();

                settings.UpdateSkyTexture();
            }
            EditorGUILayout.EndHorizontal();



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
        private AtmosphereSettingsEditor.TextureType textureType;
        private int tex;
        private int slice3D;
        public AtmosphereSettings settings;
        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Texture Type", GUILayout.Width(120));
            textureType = (AtmosphereSettingsEditor.TextureType)EditorGUILayout.EnumPopup(textureType);
            EditorGUILayout.EndHorizontal();

            if (textureType == AtmosphereSettingsEditor.TextureType.Transmittance)
            {
                m_temp = settings.transmittanceLUT;
            }
            else if (textureType == AtmosphereSettingsEditor.TextureType.SkyTexture)
            {
                m_temp = settings.skyLUT;
            }
            else if (textureType == AtmosphereSettingsEditor.TextureType.AerialLuminance)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Texture Slice", GUILayout.Width(120));
                slice3D = (int)EditorGUILayout.Slider((int)slice3D, 0f, settings.aerialLUTSize - 1);
                EditorGUILayout.EndHorizontal();

                if (settings.aerialLuminance)
                {
                    m_temp = new RenderTexture(settings.aerialLUTSize, settings.aerialLUTSize, 0, settings.aerialLuminance.format);
                    Graphics.CopyTexture(settings.aerialLuminance, slice3D, m_temp, 0);
                }
                else
                    EditorGUILayout.HelpBox("Texture is not generated!", MessageType.Warning);
            }
            else if (textureType == AtmosphereSettingsEditor.TextureType.AerialTransmittance)
            {

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Texture Slice", GUILayout.Width(120));
                slice3D = (int)EditorGUILayout.Slider((int)slice3D, 0f, settings.aerialLUTSize - 1);
                EditorGUILayout.EndHorizontal();

                if (settings.aerialTransmittance)
                {
                    m_temp = new RenderTexture(settings.aerialLUTSize, settings.aerialLUTSize, 0, settings.aerialTransmittance.format);
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


public static class AtmosphericCalculations
{
    public static Vector3 GetExtinction(Vector3 position, AtmosphereSettings settings)
    {
        float height = Vector3.Magnitude(position - settings.planetCenter) - settings.planetRadius;
        float height01 = Mathf.Clamp01(height / settings.atmosphereThickness);

        float rayleighDensity = Mathf.Exp(-height01 / settings.rayleighDensityAvg);
        float mieDensity = Mathf.Exp(-height01 / settings.mieDensityAvg);
        float ozoneDensity = Mathf.Clamp01(1 - Mathf.Abs(settings.ozonePeakAltitude - height01) * settings.ozoneDensityFalloff);

        float mie = settings.mieCoef * mieDensity;
        Vector3 rayleigh = settings.rayleighCoef * rayleighDensity;

        Vector3 extinction = (Vector3.one * mie) + Vector3.one * (settings.mieAbsorption * mieDensity) + rayleigh + settings.ozoneAbsorption * ozoneDensity;
        return extinction;
    }

    // Returns vector (dstToSphere, dstThroughSphere)
    // If ray origin is inside sphere, dstToSphere = 0
    // If ray misses sphere, dstToSphere = infinity; dstThroughSphere = 0
    public static Vector2 raySphere(Vector3 sphereCentre, float sphereRadius, Vector3 rayOrigin, Vector3 rayDir)
    {
        Vector3 offset = rayOrigin - sphereCentre;
        float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
        float b = 2 * Vector3.Dot(offset, rayDir);
        float c = Vector3.Dot(offset, offset) - sphereRadius * sphereRadius;
        float d = b * b - 4 * a * c; // Discriminant from quadratic formula

        // Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
        if (d > 0)
        {
            float s = Mathf.Sqrt(d);
            float dstToSphereNear = Mathf.Max(0, (-b - s) / (2 * a));
            float dstToSphereFar = (-b + s) / (2 * a);

            // Ignore intersections that occur behind the ray
            if (dstToSphereFar >= 0)
            {
                return new Vector2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
            }
        }
        // Ray did not intersect sphere
        return new Vector2(float.MaxValue, 0);
    }

    public static Vector3 getSunTransmittance(Vector3 pos, Vector3 sunDir, AtmosphereSettings settings)
    {
        const int sunTransmittanceSteps = 40;

        Vector2 atmoHitInfo = raySphere(settings.planetCenter, settings.atmosphereThickness + settings.planetRadius, pos, sunDir);
        float rayLength = atmoHitInfo.y;

        float stepSize = rayLength / sunTransmittanceSteps;
        Vector3 opticalDepth = Vector3.zero;

        for (int i = 0; i < sunTransmittanceSteps; i++)
        {
            pos += sunDir * stepSize;
            Vector3 extinction = GetExtinction(pos, settings);

            opticalDepth += extinction;

        }
        return exp(-(opticalDepth / settings.atmosphereThickness * stepSize));
    }

    private static Vector3 exp(Vector3 vector)
    {
        return new Vector3(Mathf.Exp(vector.x), Mathf.Exp(vector.y), Mathf.Exp(vector.z));
    }


}
