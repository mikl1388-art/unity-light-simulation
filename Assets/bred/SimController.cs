using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;
using UnityEngine;

public enum DebugMode
{
    None,
    NaN,
    All,
    LeftBottom
}

[SuppressMessage("ReSharper", "CheckNamespace")]
public class SimController : MonoBehaviour
{
    [Tooltip("Размер поля")]
    public Vector2Int fieldSize = new Vector2Int(512, 512);
    public float stiffness = 1.0f; 
    [Tooltip("Скорость симуляции")]
    [Range(1f, 30f)]
    public float simSpeed = 1.0f;
    [Header("FPS Protection")]
    [Tooltip("Если реальный FPS упадет ниже этого значения, скорость симуляции сбросится в 1")]
    public float minAllowedFPS = 20f;
    public ComputeShader computeShader;
    public Shader shader;
    public float seed = 1.0f;
    public float cumulativeRatio = 0.002f;
    public Vector4 ColorCoef = new Vector4(0.95f, 1f, 1.05f, 1f);
    public int debugStep = 200; 
    public DebugMode debugMode;

    private const int ThreadSize = 16;

    private ComputeBuffer bufferA;
    private ComputeBuffer bufferB;
    private RenderTexture cumulativeTexture;
    private bool isBufferAInput = true;
    private Material material;

    private int mainKernelID;
    private int inputTexID;
    private int outputTexID;
    private int cumulativeTexID;
    private int fieldSizeID;
    private int stiffnessID;
    private int deltaTimeID;
    private int baseTextureID;
    private int cumulativeRatioID;
    private int сolorCoefID;

    private int threadGroupsX;
    private int threadGroupsY;

    private float fpsTimer = 0f;
    private int frameCount = 0;

    private int debugInt = 0;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct CellData
    {
        public Vector4 pos;
        public Vector4 vel;
        public float mass;
        public float isValid;
    }

    private void Start()
    {
        material = new Material(shader);
        mainKernelID = computeShader.FindKernel("CSMain");
        inputTexID = Shader.PropertyToID("_Input");
        outputTexID = Shader.PropertyToID("_Output");
        cumulativeTexID = Shader.PropertyToID("_Cumulative");
        fieldSizeID = Shader.PropertyToID("_FieldSize");
        stiffnessID = Shader.PropertyToID("_Stiffness");
        deltaTimeID = Shader.PropertyToID("_DeltaTime");
        cumulativeRatioID = Shader.PropertyToID("_CumulativeRatio");
        сolorCoefID = Shader.PropertyToID("_ColorCoef");
        
        baseTextureID = Shader.PropertyToID("_BaseTexture");

        threadGroupsX = Mathf.CeilToInt((float)fieldSize.x / ThreadSize);
        threadGroupsY = Mathf.CeilToInt((float)fieldSize.y / ThreadSize);

        bufferA = CreateBuffer();
        bufferB = CreateBuffer();
        InitializeSimulation(bufferA);
        cumulativeTexture = new RenderTexture(fieldSize.x, fieldSize.y, 0, RenderTextureFormat.ARGBFloat);

        cumulativeTexture.enableRandomWrite = true;

        cumulativeTexture.filterMode = FilterMode.Point;

        cumulativeTexture.Create();

        if (TryGetComponent<MeshRenderer>(out var meshRenderer))
        {
            meshRenderer.material = material;
        }
        else
        {
            Debug.LogWarning("SimController не нашел MeshRenderer! Добавьте ссылку на объект визуализации.");
        }
    }

    private void Update()
    {
        fpsTimer += Time.unscaledDeltaTime; // unscaled защищает от влияния Time.timeScale
        frameCount++;

        if (fpsTimer >= 0.5f)
        {
            float currentFPS = frameCount / fpsTimer;

            if (currentFPS < minAllowedFPS && simSpeed > 1)
            {
                simSpeed -= 1;
                Debug.LogWarning($"[FPS Protection] Симуляция сброшена на скорость {simSpeed:F1}х! Текущий FPS: {currentFPS:F1}");
            }
            fpsTimer = 0f;
            frameCount = 0;
        }
    }

    private void FixedUpdate()
    {
        computeShader.SetInts(fieldSizeID, fieldSize.x, fieldSize.y);
        computeShader.SetFloat(stiffnessID, stiffness);
        computeShader.SetFloat(cumulativeRatioID, cumulativeRatio);
        computeShader.SetVector(сolorCoefID, ColorCoef);
        computeShader.SetFloat(deltaTimeID, Time.fixedDeltaTime);
        computeShader.SetTexture(mainKernelID, cumulativeTexID, cumulativeTexture);

        for (int i = 0; i < simSpeed; i++)
        {
            ComputeBuffer input = isBufferAInput ? bufferA : bufferB;
            ComputeBuffer output = isBufferAInput ? bufferB : bufferA;
            computeShader.SetBuffer(mainKernelID, inputTexID, input);
            computeShader.SetBuffer(mainKernelID, outputTexID, output);
            computeShader.Dispatch(mainKernelID, threadGroupsX, threadGroupsY, 1);
            isBufferAInput = !isBufferAInput;

            /*if (debugMode != DebugMode.None && debugStep > 0 && debugInt % debugStep == 0)
            {
                Debug.Log($"step {debugInt}");
                Debug.Log("===START===");
                Debug.Log(":::input:::");
                DebugMinMax(input);
                Debug.Log(":::output:::");
                DebugMinMax(output);
                Debug.Log("===END===");
            }
            debugInt++;*/
        }

        /*int[] initialBounds = new int[] { int.MaxValue, int.MinValue };
        boundsBuffer.SetData(initialBounds);
        int kernel = computeShader.FindKernel("CSFindBounds");
        computeShader.SetTexture(kernel, "SourceTexture", rt);
        computeShader.SetBuffer(kernel, "boundsBuffer", boundsBuffer);
        computeShader.Dispatch(kernel, fieldSize.x / 8, fieldSize.y / 8, 1);
        */
        material.SetTexture(baseTextureID, cumulativeTexture);
    }


    void DebugMinMax(ComputeBuffer buffer)
    {
        if (debugMode == DebugMode.None)
            return;
        // 1. Создаем массив на CPU точно такого же размера, как буфер
        int totalCells = fieldSize.x * fieldSize.y;
        CellData[] cpuBuffer = new CellData[totalCells];

        // 2. Скачиваем данные из видеокарты в наш массив (Внимание: эта операция блокирует поток и вызывает фриз)
        buffer.GetData(cpuBuffer);

        float min = float.MaxValue;
        float max = float.MinValue;
        bool flag = false;
        if (debugMode == DebugMode.LeftBottom)
        {
            totalCells = 10;
        }

        // 3. Пробегаемся по плоскому массиву
        for (int i = 0; i < totalCells; i++)
        {
            // Достаем структуру ячейки
            CellData cell = cpuBuffer[i];

            // Берем координату позиции (например, по оси X, как мы настраивали в CSInit)
            float val = cell.pos.x;

            // Ищем минимум и максимум
            max = Mathf.Max(max, val);
            min = Mathf.Min(min, val);

            bool invalid =
                            !float.IsFinite(cell.pos.x) ||
                            !float.IsFinite(cell.pos.y) ||
                            !float.IsFinite(cell.pos.z) ||

                            !float.IsFinite(cell.vel.x) ||
                            !float.IsFinite(cell.vel.y) ||
                            !float.IsFinite(cell.vel.z);

            if (debugMode != DebugMode.NaN)
            {
                invalid = true;
            }
            if (invalid)
            {
                flag = true;
                Debug.Log($"x: {cell.pos}, v:{cell.vel}, m:{cell.mass}");
            }
        }
        if (!flag)
            Debug.Log($"Всё ок");
    }

    private void OnDestroy()
    {
        bufferA?.Release();
        bufferB?.Release();
    }

    private ComputeBuffer CreateBuffer()
    {
        int totalCells = fieldSize.x * fieldSize.y;
        // float4 pos (16 байт) + float4 vel (16 байт) + float mass (4 байта) + float isValid (4 байта) = 40 байта = 10 float.
        int stride = sizeof(float) * 10;
        ComputeBuffer buffer = new ComputeBuffer(totalCells, stride, ComputeBufferType.Structured);
        return buffer;
    }

    void InitializeSimulation(ComputeBuffer buffer)
    {
        int initKernel = computeShader.FindKernel("CSInit");
        computeShader.SetInts(fieldSizeID, fieldSize.x, fieldSize.y);
        computeShader.SetFloat("Seed", seed);
        computeShader.SetBuffer(initKernel, "_OutputBuffer", buffer);
        computeShader.Dispatch(initKernel, threadGroupsX, threadGroupsY, 1);
        DebugMinMax(buffer);
        Debug.Log("~~~~~~");
    }
}
