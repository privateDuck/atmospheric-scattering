using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StormAtmosphere
{
    [CreateAssetMenu(fileName = "Star Field Object", menuName = "Fleet Commander/Star Field Object")]
    public class StarFieldObject : ScriptableObject
    {
        public bool EnableStarField;
        public ComputeShader starCompute;
        public Vector4 seed;
        [Range(1, 50)] public float Radius;
        public int starCount = 1024;
        public Vector2 globalSize;
        public float starBlendFactor = 20f;
        public Gradient color;
        public AnimationCurve twinkleCurve;
        public float twinkleSpeed;
        public Texture2D starTexture;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingSkybox;

        private ComputeBuffer stars, twinklebuffer, colorBuffer;
        private Material starMaterial;
        private Bounds starFieldBounds;
        private Mesh starQuad;

        public void InitStarField(ref RenderTexture skyTexture)
        {
            const int sampleRate = 256;
            starMaterial = new Material(Shader.Find("Hidden/Atmosphere/StarShader"));
            CleanUp();

            stars = new ComputeBuffer(starCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Star)));
            twinklebuffer = new ComputeBuffer(sampleRate, sizeof(float));
            colorBuffer = new ComputeBuffer(sampleRate, sizeof(float) * 3);

            starQuad = StormAtmosphere.StarFieldObject.CreateQuad();

            float[] twinkleArray = new float[sampleRate];
            Vector3[] colorArray = new Vector3[sampleRate];

            for (int i = 0; i < sampleRate; i++)
            {
                twinkleArray[i] = twinkleCurve.Evaluate((float)i / ((float)sampleRate - 1f));
                var col = color.Evaluate((float)i / ((float)sampleRate - 1f));
                colorArray[i] = new Vector3(col.r, col.g, col.b);
            }

            twinklebuffer.SetData(twinkleArray);
            colorBuffer.SetData(colorArray);

            starMaterial.SetBuffer("stars", stars);
            starMaterial.SetTexture("_MainTex", starTexture);
            starMaterial.SetTexture("_Sky", skyTexture);
            starMaterial.SetFloat("starBlendFactor", starBlendFactor);
            starCompute.SetBuffer(0, "stars", stars);
            starCompute.SetBuffer(0, "twinkleBuffer", twinklebuffer);
            starCompute.SetBuffer(0, "colorBuffer", colorBuffer);
            starCompute.SetVector("seed", seed);
            starCompute.SetFloat("radius", Radius);
            starCompute.SetFloat("twinkleSpeed", twinkleSpeed);
            starCompute.SetVector("globalSize", globalSize);
            starMaterial.enableInstancing = true;

        }

        public void UpdateStarField(ref CommandBuffer cmb)
        {
            if (EnableStarField)
            {
                starCompute.SetFloat("time", Time.time);
                starCompute.SetFloat("twinkleSpeed", twinkleSpeed);
                cmb.DispatchCompute(starCompute, 0, starCount / 64, 1, 1);
                starFieldBounds = new Bounds(Camera.main.transform.position, Vector3.one * 50f);
                cmb.DrawMeshInstancedProcedural(starQuad, 0, starMaterial, -1, starCount);
            }
        }

        public void CleanUp()
        {
            stars?.Dispose();
            twinklebuffer?.Dispose();
            colorBuffer?.Dispose();
        }

        public static Mesh CreateQuad()
        {
            Mesh mesh = new Mesh();
            Vector3[] verts = new Vector3[]{
                new Vector3(-0.5f,0.5f),
                new Vector3(0.5f,0.5f),
                new Vector3(0.5f,-0.5f),
                new Vector3(-0.5f,-0.5f),
            };

            int[] tris = new int[]{
                0,1,2,0,2,3
            };

            Vector2[] uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.name = "Quad";
            return mesh;
        }

        public struct Star
        {
            Vector3 position;
            Vector3 color;
            float size;
        }
    }

}