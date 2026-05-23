using UnityEngine;

public class SimController : MonoBehaviour
{
    [Tooltip("Размер поля")]
    public Vector2Int fieldSize = new Vector2Int(512, 512);
    public float stiffness = 1.0f; 
    [Tooltip("Скорость симуляции")]
    [Range(0f, 30f)]
    public float simSpeed = 1.0f;
    public ComputeShader computeShader;
    public Shader shader;
    public float seed = 0.0f;

    private readonly int threadSize = 8;

    private RenderTexture textureA;
    private RenderTexture textureB;
    private bool isBufferAInput = true;
    private Material material;

    private int mainKernelID;
    private int inputTexID;
    private int outputTexID;
    private int fieldSizeID;
    private int stiffnessID;
    private int deltaTimeID;
    private int baseTextureID;

    private Vector4 sizeVector;
    private int threadGroupsX;
    private int threadGroupsY;

    //private int debugInt = 0;

    private void Start()
    {
        material = new Material(shader);
        mainKernelID = computeShader.FindKernel("CSMain");
        inputTexID = Shader.PropertyToID("_Input");
        outputTexID = Shader.PropertyToID("_Output");
        fieldSizeID = Shader.PropertyToID("_FieldSize");
        stiffnessID = Shader.PropertyToID("_Stiffness");
        deltaTimeID = Shader.PropertyToID("_DeltaTime");
        baseTextureID = Shader.PropertyToID("_BaseTexture");

        threadGroupsX = Mathf.CeilToInt((float)fieldSize.x / threadSize);
        threadGroupsY = Mathf.CeilToInt((float)fieldSize.y / threadSize);

        textureA = CreateRT();
        textureB = CreateRT();
        sizeVector = new Vector4(fieldSize.x, fieldSize.y, 0, 0);

        if (TryGetComponent<MeshRenderer>(out var meshRenderer))
        {
            meshRenderer.material = material;
        }
        else
        {
            Debug.LogWarning("SimController не нашел MeshRenderer! Добавьте ссылку на объект визуализации.");
        }
    }

    private void FixedUpdate()
    { 
        RenderTexture input = isBufferAInput ? textureA : textureB;
        RenderTexture output = isBufferAInput ? textureB : textureA;
        isBufferAInput = !isBufferAInput;

        computeShader.SetTexture(mainKernelID, inputTexID, input);
        computeShader.SetTexture(mainKernelID, outputTexID, output);
        computeShader.SetVector(fieldSizeID, sizeVector);
        computeShader.SetFloat(stiffnessID, stiffness);
        computeShader.SetFloat(deltaTimeID, Time.fixedDeltaTime * simSpeed);
        computeShader.Dispatch(mainKernelID, threadGroupsX, threadGroupsY, 1);

        /*int[] initialBounds = new int[] { int.MaxValue, int.MinValue };
        boundsBuffer.SetData(initialBounds);
        int kernel = computeShader.FindKernel("CSFindBounds");
        computeShader.SetTexture(kernel, "SourceTexture", rt);
        computeShader.SetBuffer(kernel, "boundsBuffer", boundsBuffer);
        computeShader.Dispatch(kernel, fieldSize.x / 8, fieldSize.y / 8, 1);
        */
        material.SetTexture(baseTextureID, output);

        /*if (debugInt % 200 == 0)
        {
            Debug.Log("===START===");
            Debug.Log("input");
            DebugMinMax(input);
            Debug.Log("output");
            DebugMinMax(output);
            Debug.Log("===END===");
        }
        //Debug.Log($"step {debugInt}");
        debugInt++;*/
    }


    void DebugMinMax(RenderTexture rt)
    {
        // 1. Сохраняем текущую активную RenderTexture Unity
        RenderTexture previousActive = RenderTexture.active;

        // 2. Делаем нашу RenderTexture активной для чтения
        RenderTexture.active = rt;

        // 3. Создаем временную CPU-текстуру нужного формата
        Texture2D tempTex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);

        // 4. Копируем данные из active RenderTexture в Texture2D (вот тут происходит фриз)
        tempTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tempTex.Apply();

        // 5. Восстанавливаем старую активную текстуру
        RenderTexture.active = previousActive;
        float min = float.MaxValue; float max = float.MinValue;
        // 6. Читаем нужный пиксель и забираем R канал
        for (int i = 0; i < fieldSize.x; i++)
        {
            for (int j = 0; j < fieldSize.y; j++)
            {
                Color col = tempTex.GetPixel(i, j);
                float val = col.r;
                //if (i % 128 == 0 || j % 128 == 0 ) 
                //    Debug.Log($"x: {col.r}, v:{col.g}, m:{col.b}");
                max = Mathf.Max(max, val);
                min = Mathf.Min(min, val);
            }
        }

        // Очищаем за собой память CPU
        Destroy(tempTex);

        Debug.Log($"min: {min}, max:{max}");
    }

    private void OnDestroy()
    {
        if (textureA != null)
        {
            textureA.Release();
            Destroy(textureA);
        }

        if (textureB != null)
        {
            textureB.Release();
            Destroy(textureB);
        }
    }

    RenderTexture CreateRT()
    {
        RenderTexture rt = new RenderTexture(fieldSize.x, fieldSize.y, 0, RenderTextureFormat.ARGBFloat);
        rt.enableRandomWrite = true;
        rt.filterMode = FilterMode.Point;
        rt.Create();
        InitializeRTOnGPU(rt);
        //DebugMinMax(rt);
        return rt;
    }

    void InitializeRTOnGPU(RenderTexture rt)
    {
        int initKernel = computeShader.FindKernel("CSInit");

        computeShader.SetTexture(initKernel, "ResultTexture", rt);
        computeShader.SetFloat("Seed", seed);
        computeShader.Dispatch(initKernel, threadGroupsX, threadGroupsY, 1);
    }
}
