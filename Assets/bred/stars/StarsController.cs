// ReSharper disable InconsistentNaming
using UnityEngine;

// ReSharper disable once CheckNamespace
public class StarsController : MonoBehaviour
{
    private struct Star
    {
        public Vector4 posVel; // x, y - position; z, w - velocity
        public Vector4 data;   // x - mass (y, z, w - не используются)
    }
    
    private const int threads = 64;
    
    [Header("Simulation Settings")]
    [SerializeField] private ComputeShader starComputeShader;
    [SerializeField] private uint starsCount = 1000;
    [SerializeField] private float softening = 0.1f;
    [SerializeField] private float gravityConstant = 1.0f;
    
    [Header("Render Settings")]
    [SerializeField] private Material starMaterial; // Материал с нашим шейдером из Шага 1
    [SerializeField] private Texture2D starTexture;  // Красивая мягкая текстура звездочки
    [SerializeField] private float starSize = 0.1f;

    private int sizeId;
    private GraphicsBuffer indexBuffer; // Вспомогательный буфер индексов для RenderPrimitivesIndexed
    private MaterialPropertyBlock propBlock;
    
    private ComputeBuffer inputBuffer;
    private Star[] starsArray;

    private int kernelCSMain;
    private int kernelCSOptimised;
    private int inputBufferId;
    private int starsCountId;
    private int softeningId;
    private int gravityConstantId;
    private int deltaTimeId;

    private void Start()
    {
        kernelCSMain = starComputeShader.FindKernel("CSMain");
        kernelCSOptimised = starComputeShader.FindKernel("CSOptimised");
        inputBufferId = Shader.PropertyToID("_Input");
        starsCountId = Shader.PropertyToID("_StarsCount");
        softeningId = Shader.PropertyToID("_Softening");
        gravityConstantId = Shader.PropertyToID("G");
        deltaTimeId = Shader.PropertyToID("_DeltaTime");
        InitBuffers();
        
        inputBufferId = Shader.PropertyToID("_Input");
        sizeId = Shader.PropertyToID("_Size");
        starMaterial.SetTexture("_MainTex", starTexture);
        // Инициализируем индексный буфер для отрисовки квадов (2 треугольника на звезду)
        InitIndexBuffer();
    }

    private void InitBuffers()
    {
        starsArray = new Star[starsCount];
        
        for (int i = 0; i < starsCount; i++)
        {
            Vector2 pos = new Vector2(Random.insideUnitCircle.x * 10f, Random.insideUnitCircle.y * 100f);
            float radius = pos.magnitude;
            if (radius < 0.001f) radius = 0.001f;
            float multiplier = 1.0f; 
            float speed = multiplier * Mathf.Sqrt(radius);
            Vector2 orbitDirection = new Vector2(-pos.y, pos.x) / radius;
            Vector2 vel = orbitDirection * speed;
            starsArray[i].posVel = new Vector4(pos.x, pos.y, vel.x, vel.y);
            starsArray[i].data = new Vector4(Random.Range(1f, 10f), 0f, 0f, 0f); // Масса в x
        }

        // Размер структуры Star: 2 * Vector4 (4 float * 4 байта = 16 байт каждая) -> 16 + 16 = 32 байта
        int stride = sizeof(float) * 4 * 2; 
        inputBuffer = new ComputeBuffer((int)starsCount, stride);
        inputBuffer.SetData(starsArray);

        // Привязываем буфер к ядру (это тоже можно сделать один раз в Start)
        starComputeShader.SetBuffer(kernelCSMain, inputBufferId, inputBuffer);
        starComputeShader.SetBuffer(kernelCSOptimised, inputBufferId, inputBuffer);
    }
    
    private void InitIndexBuffer()
    {
        // Каждой звезде (кваду) нужно 6 индексов: 0-1-2, 2-1-3
        int indexCount = (int) starsCount * 6;
        int[] indices = new int[indexCount];

        for (int i = 0; i < starsCount; i++)
        {
            int vRoot = i * 4; // Индекс первой вершины этого квада
            int iRoot = i * 6; // Индекс в массиве индексов

            indices[iRoot + 0] = vRoot + 0;
            indices[iRoot + 1] = vRoot + 1;
            indices[iRoot + 2] = vRoot + 2;
            
            indices[iRoot + 3] = vRoot + 2;
            indices[iRoot + 4] = vRoot + 1;
            indices[iRoot + 5] = vRoot + 3;
        }

        indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, indexCount, sizeof(int));
        indexBuffer.SetData(indices);
    }

    private void FixedUpdate()
    {
        if (inputBuffer == null) return;

        starComputeShader.SetInt(starsCountId, (int)starsCount);
        starComputeShader.SetFloat(softeningId, softening);
        starComputeShader.SetFloat(gravityConstantId, gravityConstant);
        starComputeShader.SetFloat(deltaTimeId, Time.fixedDeltaTime);

        int threadGroupsX = Mathf.CeilToInt((float)starsCount / threads);
        
        //starComputeShader.Dispatch(kernelCSMain, threadGroupsX, 1, 1);
        starComputeShader.Dispatch(kernelCSOptimised, threadGroupsX, 1, 1);

        // Если тебе нужно прочитать данные обратно на CPU (внимание: это тяжелая операция!):
        // inputBuffer.GetData(starsArray);
    }

    private void LateUpdate()
    {
        if (inputBuffer == null || !starMaterial) return;

        // 1. Передаем буфер напрямую в материал! 🐱
        // Это заставит DX12 правильно привязать SRV (Shader Resource View)
        starMaterial.SetBuffer(inputBufferId, inputBuffer);
        starMaterial.SetFloat(sizeId, starSize);

        // 2. Блок свойств нам больше не нужен для буфера
        RenderParams rp = new RenderParams(starMaterial);
        rp.worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

        // 3. Улетаем в отрисовку!
        Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Triangles, indexBuffer, 6, instanceCount: (int)starsCount);
    }

    private void OnDestroy()
    {
        inputBuffer?.Release();
        inputBuffer = null;
        indexBuffer?.Release();
        indexBuffer = null;
    }
}