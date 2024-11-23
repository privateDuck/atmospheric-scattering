using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using System;
using System.Linq;
using UnityEngine.VFX;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StormAtmosphere
{
    public class Atmosphere : MonoBehaviour
    {
        [SerializeField] public AtmosphereObject atmosphereObject;
        [SerializeField] private StarFieldObject starFieldObject;
        private Material skyBoxMaterial;
        private FogPass m_FogPass;
        private StarPass starPass;
        private Camera renderingCamera;

        public Camera RenderingCamera { get { return renderingCamera; } set { renderingCamera = value; } }

        #region Editor Fields
        [HideInInspector] public bool m_EditorAutoUpdate;
        [HideInInspector] public bool m_EditorSetup;
        [HideInInspector] private Editor_AutoUpdateHelper autoUpdateHelper;
        [HideInInspector] private Material m_EditorMaterial;
        #endregion

        private void OnEnable()
        {

        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            atmosphereObject.Sun = FindSun();
            atmosphereObject.InitAtmosphere();
            atmosphereObject.UpdateAtmosphere(Camera.main);
            RenderPipelineManager.beginCameraRendering += beginRendering;

            m_FogPass ??= new FogPass(atmosphereObject);
            starFieldObject.InitStarField(ref atmosphereObject.skyLUT);
            starPass ??= new StarPass(starFieldObject, ref atmosphereObject.skyLUT);

            skyBoxMaterial ??= new Material(Shader.Find("Hidden/Atmosphere/Skybox"));
            atmosphereObject.InitMaterial(skyBoxMaterial);

            skyBoxMaterial.mainTexture = atmosphereObject.skyLUT;
            skyBoxMaterial.SetFloat("_Exposure", 1f);
            RenderSettings.skybox = skyBoxMaterial;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= beginRendering;
            starFieldObject.CleanUp();
        }

        private void beginRendering(ScriptableRenderContext context, Camera camera)
        {
            atmosphereObject.UpdateAtmosphere(camera);

            var urpData = camera.GetUniversalAdditionalCameraData();
            urpData.scriptableRenderer.EnqueuePass(m_FogPass);
            urpData.scriptableRenderer.EnqueuePass(starPass);
        }

        private void Update()
        {

        }

        private Transform FindSun()
        {
            Light[] lights = GameObject.FindObjectsOfType<Light>(true);
            List<Light> dLights = new List<Light>(capacity: 5);

            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    dLights.Add(light);
                }
            }

            foreach (var dl in dLights)
            {
                if (dl.transform.CompareTag("Sun"))
                {
                    return dl.transform;
                }
            }

            Light mainDirLight = dLights[0];
            mainDirLight.transform.tag = "Sun";
            return mainDirLight.transform;
        }

        public void EditorSetup()
        {
            atmosphereObject.Sun = GameObject.FindGameObjectWithTag("Sun").transform;
            var EditorSkybox = new Material(Shader.Find("Hidden/Atmosphere/Skybox"));

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Atmosphere"))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Atmosphere");
            }

            AssetDatabase.CreateAsset(EditorSkybox, "Assets/Resources/Atmosphere/Editor_Atmosphere.mat");

            m_EditorMaterial = AssetDatabase.LoadAssetAtPath("Assets/Resources/Atmosphere/Editor_Atmosphere.mat", typeof(Material)) as Material;
            RenderSettings.skybox = m_EditorMaterial;


            autoUpdateHelper ??= GetComponent<Editor_AutoUpdateHelper>();
            autoUpdateHelper ??= gameObject.AddComponent<Editor_AutoUpdateHelper>();

            autoUpdateHelper.ReapeatingInvoke += EditorUpdate;


            Debug.Log("Editor Assets Created");
            Debug.Log("Now click the Update button to finalize");
        }

        public void EditorUpdate()
        {
            if (m_EditorAutoUpdate)
            {
                atmosphereObject.Sun ??= GameObject.FindGameObjectWithTag("Sun").transform;
                m_EditorMaterial ??= AssetDatabase.LoadAssetAtPath("Assets/Resources/Atmosphere/Editor_Atmosphere.mat", typeof(Material)) as Material;

                if (atmosphereObject.requiresInitialization) atmosphereObject.InitAtmosphere();
                atmosphereObject.UpdateAtmosphere(Camera.main);

                atmosphereObject.InitMaterial(m_EditorMaterial);
                m_EditorMaterial.mainTexture = atmosphereObject.skyLUT;
                m_EditorMaterial.SetFloat("_Exposure", 1f);
            }
        }
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(Atmosphere))]
    public class AtmosphereEditor : Editor
    {

        Atmosphere atmosphere;
        bool EditorOnlyFoldOut;

        public override void OnInspectorGUI()
        {
            atmosphere = (Atmosphere)target;

            base.OnInspectorGUI();

            EditorGUILayout.BeginFoldoutHeaderGroup(EditorOnlyFoldOut, "Editor Only");

            if (!atmosphere.m_EditorSetup)
            {
            }
            if (GUILayout.Button("Setup Editor"))
            {
                SetupEditor();
            }

            if (atmosphere.m_EditorSetup)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Auto Update Sky", GUILayout.Width(120));
                atmosphere.m_EditorAutoUpdate = EditorGUILayout.Toggle(atmosphere.m_EditorAutoUpdate);
                EditorGUILayout.EndHorizontal();

                if (!atmosphere.m_EditorAutoUpdate)
                {
                    if (GUILayout.Button("Initialize the sky"))
                    {
                        atmosphere.atmosphereObject.InitAtmosphere();
                    }

                    if (GUILayout.Button("Update the sky"))
                    {
                        atmosphere.atmosphereObject.UpdateAtmosphere(Camera.main);
                    }
                }

                if (atmosphere.m_EditorAutoUpdate)
                {
                    EditorGUILayout.HelpBox("May impact the editor performance", MessageType.Warning);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void SetupEditor()
        {
            atmosphere.m_EditorSetup = true;
            atmosphere.EditorSetup();
        }
    }
#endif

}